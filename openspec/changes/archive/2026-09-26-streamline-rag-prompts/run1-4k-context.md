# Golden run 1 — new prompts, Ollama default 4k context

Run at 2026-09-26 13:04 against <http://localhost:8081>, model `qwen3:8b`.

| Id | Lang | Type | Markers | In range | Language | Leakage | Book sources | Latency (s) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| G1 | EN | Disease | 0 | yes | ok (0.0) | no | 0 | 3.7 |
| G2 | EN | Symptoms | 0 | yes | ok (0.0) | no | 0 | 0.7 |
| G3 | EN | Treatment | 0 | yes | ok (0.0) | no | 0 | 0.6 |
| G4 | EN | GlobalSearch | 0 | yes | ok (0.0) | no | 0 | 0.4 |
| G5 | EN | DifferentialDiagnosis | 0 | yes | ok (0.0) | no | 0 | 1.0 |
| G6 | EN | Treatment | 0 | yes | ok (0.0) | no | 0 | 167.4 |
| G7 | BG | Disease | HTTP 504 | | | | | 174.1 |
| G8 | BG | Symptoms | 6 | yes | ok (1.0) | no | 5 | 29.9 |
| G9 | BG | GlobalSearch | 20 | yes | ok (1.0) | no | 5 | 41.7 |
| G10 | BG | DifferentialDiagnosis | 8 | yes | WRONG (0.0) | no | 5 | 34.2 |
| G11 | BG | Treatment | 20 | yes | WRONG (0.0) | no | 5 | 45.6 |
| G12 | BG | Treatment | 4 | yes | WRONG (0.0) | no | 5 | 59.3 |

## Summary

- Answers with markers: 5/11
- All markers in range: 11/11
- Correct answer language: 8/11
- List/markdown leakage: 0/11
- Mean latency: 35.0s
