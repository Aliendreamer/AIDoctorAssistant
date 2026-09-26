#!/usr/bin/env python3
"""Run the RAG golden question set against the running app and write a markdown report.

Usage: scripts/golden_run.py <label> <out.md> [<answers.json>] [<eval-username>]

Each run should use a fresh eval username so chat history starts empty. Checks per answer: [n]
citation markers present and in range, answer language, list/markdown leakage, latency. For prompt
sizes and truncation, read the Ollama log afterwards:
  docker logs --since <start> personalcommandcenter-ollama-1 | grep -E "task.n_tokens|truncated ="
See openspec/changes/archive/*tune-query-pipeline/comparison.md for past results.
"""
import json, os, re, sys, time, urllib.request

BASE = os.environ.get("MEDASSIST_URL", "http://localhost:8081")
EVAL_USER, EVAL_PWD = sys.argv[4] if len(sys.argv) > 4 else "golden-eval", "golden-eval-123"
QUESTIONS = [
    ("G1", "BG", "Disease", "Какво е болест на Кавазаки?"),
    ("G2", "BG", "Symptoms", "Дете с лаеща кашлица, дрезгав глас и инспираторен стридор"),
    ("G3", "BG", "Treatment", "Лечение на остър среден отит при деца"),
    ("G4", "BG", "GlobalSearch", "Бронхиолит при кърмачета"),
    ("G5", "BG", "DifferentialDiagnosis", "Дете с температура от 5 дни, ягодов език, обрив и конюнктивит"),
    ("G6", "BG", "Treatment", "Лечение на отравяне с парацетамол при дете"),
    ("G7", "BG", "Disease", "Какво е муковисцидоза?"),
    ("G8", "BG", "Symptoms", "Дете с висока температура и петехиален обрив"),
    ("G9", "BG", "GlobalSearch", "Фебрилни гърчове при деца"),
    ("G10", "BG", "DifferentialDiagnosis", "Дете с болка в корема, повръщане и температура"),
    ("G11", "BG", "Treatment", "Лечение на остър гастроентерит с дехидратация при деца"),
    ("G12", "BG", "Treatment", "А при кърмачета?"),
    ("E1", "EN", "Disease", "What is Kawasaki disease?"),
    ("E2", "EN", "Symptoms", "Child with barking cough, hoarse voice and inspiratory stridor"),
    ("E3", "EN", "Treatment", "Treatment of acute otitis media in children"),
    ("E4", "EN", "GlobalSearch", "Bronchiolitis in infants"),
    ("E5", "EN", "DifferentialDiagnosis", "Child with fever for 5 days, strawberry tongue, rash and conjunctival injection"),
    ("E6", "EN", "Treatment", "Management of paracetamol poisoning in a child"),
]
QT = {"Symptoms": 0, "Disease": 1, "Treatment": 2, "GlobalSearch": 3, "DifferentialDiagnosis": 4}
TYPES = ["disease", "symptoms", "treatment", "globalsearch", "differentialdiagnosis"]


def call(method, path, body=None, token=None, timeout=600):
    req = urllib.request.Request(BASE + path, method=method,
                                 data=json.dumps(body).encode() if body is not None else None)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            raw = r.read()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")


def login(user, pwd):
    status, body = call("POST", "/api/auth/login", {"username": user, "password": pwd})
    return body["token"] if status == 200 else None


def ensure_eval_user():
    token = login(EVAL_USER, EVAL_PWD)
    if token:
        return token
    admin = login("admin", "medassist123")
    status, body = call("POST", "/api/admin/users",
                        {"username": EVAL_USER, "role": "Doctor", "password": EVAL_PWD}, admin)
    if status >= 300:
        sys.exit(f"could not create eval user: {status} {body}")
    return login(EVAL_USER, EVAL_PWD)


MARKER = re.compile(r"\[\s*(\d+(?:\s*,\s*\d+)*)\s*\]")
LEAK = re.compile(r"^\s*([-*•]|\d+[.)]|#{1,6})\s", re.M)


def score(lang, answer, book_sources):
    nums = [int(n) for g in MARKER.findall(answer) for n in g.split(",")]
    letters = [c for c in answer if c.isalpha()]
    cyr = sum(1 for c in letters if "Ѐ" <= c <= "ӿ") / max(1, len(letters))
    lang_ok = cyr > 0.5 if lang == "BG" else cyr < 0.1
    return {
        "markers": len(nums),
        "has_markers": bool(nums),
        "in_range": all(1 <= n <= book_sources for n in nums),
        "lang_ok": lang_ok,
        "leak": bool(LEAK.search(answer)) or "**" in answer,
        "cyr": round(cyr, 2),
    }


def main():
    label, out = sys.argv[1], sys.argv[2]
    answers_path = sys.argv[3] if len(sys.argv) > 3 else None
    token = ensure_eval_user()
    for t in TYPES:
        call("DELETE", f"/api/chat/history/{t}", token=token)

    rows, answers = [], {}
    for qid, lang, qtype, q in QUESTIONS:
        t0 = time.time()
        status, body = call("POST", "/api/query",
                            {"query": q, "queryType": QT[qtype], "language": 0, "webSearchEnabled": False}, token)
        dt = time.time() - t0
        if status != 200:
            rows.append((qid, lang, qtype, None, dt, f"HTTP {status}"))
            print(f"{qid}: HTTP {status} {str(body)[:200]}", file=sys.stderr)
            continue
        answer = body.get("answer", "")
        books = sum(1 for s in body.get("sources", []) if str(s.get("sourceType")) in ("Book", "0"))
        s = score(lang, answer, books)
        s["books"] = books
        answers[qid] = answer
        rows.append((qid, lang, qtype, s, dt, ""))
        print(f"{qid} {dt:5.1f}s markers={s['markers']} inrange={s['in_range']} lang={s['lang_ok']} "
              f"leak={s['leak']} books={books}", file=sys.stderr)

    ok = [r for r in rows if r[3]]
    lines = [f"# Golden run — {label}", "",
             f"Run at {time.strftime('%Y-%m-%d %H:%M')} against <{BASE}>, model `qwen3:8b`.", "",
             "| Id | Lang | Type | Markers | In range | Language | Leakage | Book sources | Latency (s) |",
             "| --- | --- | --- | --- | --- | --- | --- | --- | --- |"]
    for qid, lang, qtype, s, dt, err in rows:
        if s is None:
            lines.append(f"| {qid} | {lang} | {qtype} | {err} | | | | | {dt:.1f} |")
        else:
            lines.append(f"| {qid} | {lang} | {qtype} | {s['markers']} | {'yes' if s['in_range'] else 'NO'} | "
                         f"{'ok' if s['lang_ok'] else 'WRONG'} ({s['cyr']}) | {'YES' if s['leak'] else 'no'} | "
                         f"{s['books']} | {dt:.1f} |")
    n = max(1, len(ok))
    lines += ["", "## Summary", "",
              f"- Answers with markers: {sum(r[3]['has_markers'] for r in ok)}/{len(ok)}",
              f"- All markers in range: {sum(r[3]['in_range'] for r in ok)}/{len(ok)}",
              f"- Correct answer language: {sum(r[3]['lang_ok'] for r in ok)}/{len(ok)}",
              f"- List/markdown leakage: {sum(r[3]['leak'] for r in ok)}/{len(ok)}",
              f"- Mean latency: {sum(r[4] for r in ok) / n:.1f}s", ""]
    open(out, "w").write("\n".join(lines))
    if answers_path:
        json.dump(answers, open(answers_path, "w"), ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
