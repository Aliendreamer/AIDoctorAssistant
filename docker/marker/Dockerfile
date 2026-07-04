FROM python:3.11-slim

RUN apt-get update && apt-get install -y \
    libgl1 \
    libglib2.0-0 \
    libgomp1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# The cu128 torch install pulls multi-hundred-MB nvidia-* CUDA wheels from pypi.nvidia.com, whose
# CDN is frequently slow. pip's default 15s socket timeout trips a ReadTimeoutError mid-download, so
# give every pip invocation a generous per-read timeout and automatic retries.
ENV PIP_DEFAULT_TIMEOUT=300 \
    PIP_RETRIES=10

# Install torch first so marker-pdf's resolver doesn't replace it.
# Stable cu128 (not nightly): torch/torchvision ship as a matched set here, avoiding the
# nightly date-mismatch resolver conflict. cu128 supports Blackwell (RTX 50xx).
ARG TORCH_INDEX=https://download.pytorch.org/whl/cu128
RUN pip install --no-cache-dir torch torchvision --index-url ${TORCH_INDEX}

RUN pip install --no-cache-dir \
    "marker-pdf>=1.6.0" \
    "fastapi>=0.111.0" \
    "uvicorn>=0.29.0" \
    python-multipart

COPY app.py .

EXPOSE 5002
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "5002"]
