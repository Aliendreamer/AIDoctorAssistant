import os
import uuid
import time
import logging
import threading
from concurrent.futures import ThreadPoolExecutor
from contextlib import asynccontextmanager
from pydantic import BaseModel
from fastapi import FastAPI, UploadFile, File, Query, HTTPException
from fastapi.responses import JSONResponse
from marker.converters.pdf import PdfConverter
from marker.models import create_model_dict
from marker.config.parser import ConfigParser
from marker.output import text_from_rendered
import tempfile

logger = logging.getLogger("marker-service")

artifact_dict = None

LLM_ENDPOINT = os.getenv("MARKER_LLM_ENDPOINT", "http://ollama:11434/v1")
LLM_MODEL    = os.getenv("MARKER_LLM_MODEL", "qwen2.5vl:7b")
MD_PATH      = os.getenv("BOOKS_MD_PATH", "/books/mdfiles")

# Single-worker pool — one conversion at a time
_executor = ThreadPoolExecutor(max_workers=1)
_jobs: dict[str, dict] = {}
_jobs_lock = threading.Lock()


@asynccontextmanager
async def lifespan(app: FastAPI):
    global artifact_dict
    logger.info("Loading Marker models...")
    artifact_dict = create_model_dict()
    logger.info("Marker models loaded.")
    yield
    _executor.shutdown(wait=False)

app = FastAPI(title="Marker OCR Service", lifespan=lifespan)

def _build_config(use_llm: bool) -> dict:
    cfg = {"output_format": "markdown", "langs": "bg,en"}
    if use_llm:
        cfg["use_llm"] = True
        cfg["llm_model"] = f"openai/{LLM_MODEL}"
        os.environ["OPENAI_API_BASE"] = LLM_ENDPOINT
        os.environ["OPENAI_API_KEY"]  = "ollama"
    return cfg

def _convert(filepath: str, use_llm: bool) -> str:
    config_parser = ConfigParser(_build_config(use_llm))
    converter = PdfConverter(
        config=config_parser.generate_config_dict(),
        artifact_dict=artifact_dict,
        processor_list=config_parser.get_processors(),
        renderer=config_parser.get_renderer(),
    )
    rendered = converter(filepath)
    text, _, _ = text_from_rendered(rendered)
    return text

def _run_job(job_id: str, file_path: str, use_llm: bool, save_path: str | None):
    try:
        logger.info("Job %s: starting conversion for %s", job_id, file_path)
        markdown = _convert(file_path, use_llm)

        # Save to disk as protection — survives .NET polling failures
        if save_path:
            try:
                with open(save_path, "w", encoding="utf-8") as f:
                    f.write(markdown)
                logger.info("Job %s: markdown saved to %s", job_id, save_path)
            except Exception as save_err:
                logger.warning("Job %s: could not save markdown to disk: %s", job_id, save_err)

        with _jobs_lock:
            _jobs[job_id] = {"state": "done", "save_path": save_path}
        logger.info("Job %s: done", job_id)
    except Exception as exc:
        logger.exception("Job %s: conversion failed", job_id)
        with _jobs_lock:
            _jobs[job_id] = {"state": "failed", "error": str(exc)}


@app.get("/health")
def health():
    return {"status": "ok", "models_loaded": artifact_dict is not None}


@app.post("/convert")
async def convert(
    file: UploadFile = File(...),
    use_llm: bool = Query(False)
):
    if artifact_dict is None:
        raise HTTPException(status_code=503, detail="Models not loaded yet")

    content = await file.read()
    if not content:
        raise HTTPException(status_code=400, detail="Empty file")

    with tempfile.NamedTemporaryFile(suffix=".pdf", delete=False) as tmp:
        tmp.write(content)
        tmp_path = tmp.name

    try:
        return {"markdown": _convert(tmp_path, use_llm)}
    except Exception as exc:
        logger.exception("Conversion failed for %s", file.filename)
        raise HTTPException(status_code=500, detail=str(exc)) from exc
    finally:
        os.unlink(tmp_path)


class ConvertByPathRequest(BaseModel):
    file_path: str
    use_llm: bool = False


@app.post("/convert-by-path", status_code=202)
async def convert_by_path(req: ConvertByPathRequest):
    if artifact_dict is None:
        raise HTTPException(status_code=503, detail="Models not loaded yet")

    if not os.path.isfile(req.file_path):
        raise HTTPException(status_code=404, detail=f"File not found: {req.file_path}")

    job_id = str(uuid.uuid4())
    os.makedirs(MD_PATH, exist_ok=True)
    save_path = os.path.join(MD_PATH, os.path.splitext(os.path.basename(req.file_path))[0] + ".md")

    with _jobs_lock:
        _jobs[job_id] = {"state": "running", "started_at": time.time()}

    _executor.submit(_run_job, job_id, req.file_path, req.use_llm, save_path)
    logger.info("Job %s: queued for %s", job_id, req.file_path)

    return JSONResponse(status_code=202, content={"job_id": job_id})


@app.get("/status/{job_id}")
async def job_status(job_id: str):
    with _jobs_lock:
        job = _jobs.get(job_id)

    if job is None:
        raise HTTPException(status_code=404, detail=f"Unknown job: {job_id}")

    result = dict(job)
    if "started_at" in result:
        result["elapsed_seconds"] = int(time.time() - result.pop("started_at"))
    return result
