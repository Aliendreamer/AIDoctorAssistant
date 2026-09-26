# Golden run — run 3 — tune-query-pipeline (query prep, language-aware rerank, budget)

Run at 2026-09-26 13:43 against <http://localhost:8081>, model `qwen3:8b`.

| Id | Lang | Type | Markers | In range | Language | Leakage | Book sources | Latency (s) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| G1 | BG | Disease | 12 | yes | ok (1.0) | no | 5 | 25.6 |
| G2 | BG | Symptoms | 12 | yes | ok (0.98) | no | 4 | 13.6 |
| G3 | BG | Treatment | 8 | yes | ok (0.98) | no | 5 | 14.2 |
| G4 | BG | GlobalSearch | 24 | yes | ok (0.98) | no | 5 | 46.2 |
| G5 | BG | DifferentialDiagnosis | 8 | yes | ok (0.98) | no | 4 | 15.5 |
| G6 | BG | Treatment | 10 | yes | ok (1.0) | no | 4 | 19.5 |
| G7 | BG | Disease | 6 | yes | ok (0.98) | no | 4 | 16.2 |
| G8 | BG | Symptoms | 8 | yes | ok (1.0) | no | 4 | 13.0 |
| G9 | BG | GlobalSearch | 12 | yes | ok (1.0) | no | 5 | 27.0 |
| G10 | BG | DifferentialDiagnosis | 12 | yes | ok (1.0) | no | 5 | 26.7 |
| G11 | BG | Treatment | 13 | yes | ok (0.95) | no | 5 | 28.4 |
| G12 | BG | Treatment | 16 | yes | ok (0.94) | no | 5 | 31.6 |
| E1 | EN | Disease | 10 | yes | ok (0.0) | no | 5 | 14.7 |
| E2 | EN | Symptoms | 15 | yes | ok (0.0) | no | 5 | 15.2 |
| E3 | EN | Treatment | 9 | yes | ok (0.0) | no | 5 | 16.0 |
| E4 | EN | GlobalSearch | 31 | yes | ok (0.0) | no | 5 | 21.3 |
| E5 | EN | DifferentialDiagnosis | 9 | yes | ok (0.0) | no | 5 | 16.0 |
| E6 | EN | Treatment | 15 | yes | ok (0.0) | no | 5 | 21.5 |

## Summary

- Answers with markers: 18/18
- All markers in range: 18/18
- Correct answer language: 18/18
- List/markdown leakage: 0/18
- Mean latency: 21.2s
