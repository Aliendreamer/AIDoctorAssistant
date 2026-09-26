# Golden run — run 4 — tune-query-pipeline, calibrated budget (2 chars/token, 2,048 reserve)

Run at 2026-09-26 13:50 against <http://localhost:8081>, model `qwen3:8b`.

| Id | Lang | Type | Markers | In range | Language | Leakage | Book sources | Latency (s) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| G1 | BG | Disease | 18 | yes | ok (1.0) | no | 4 | 16.9 |
| G2 | BG | Symptoms | 12 | yes | ok (0.99) | no | 4 | 15.7 |
| G3 | BG | Treatment | 11 | yes | ok (0.96) | no | 5 | 14.4 |
| G4 | BG | GlobalSearch | 20 | yes | ok (0.97) | no | 5 | 43.0 |
| G5 | BG | DifferentialDiagnosis | 9 | yes | ok (0.98) | no | 4 | 16.4 |
| G6 | BG | Treatment | 11 | yes | ok (0.97) | no | 3 | 24.7 |
| G7 | BG | Disease | 6 | yes | ok (0.93) | no | 4 | 13.7 |
| G8 | BG | Symptoms | 4 | yes | ok (0.98) | no | 4 | 10.4 |
| G9 | BG | GlobalSearch | 16 | yes | ok (1.0) | no | 5 | 21.8 |
| G10 | BG | DifferentialDiagnosis | 13 | yes | ok (1.0) | no | 5 | 21.6 |
| G11 | BG | Treatment | 11 | yes | ok (0.95) | no | 4 | 19.6 |
| G12 | BG | Treatment | 12 | yes | ok (0.97) | no | 4 | 21.4 |
| E1 | EN | Disease | 8 | yes | ok (0.0) | no | 4 | 11.0 |
| E2 | EN | Symptoms | 14 | yes | ok (0.0) | no | 5 | 11.2 |
| E3 | EN | Treatment | 5 | yes | ok (0.0) | no | 4 | 11.0 |
| E4 | EN | GlobalSearch | 29 | yes | ok (0.0) | no | 5 | 21.1 |
| E5 | EN | DifferentialDiagnosis | 5 | yes | ok (0.0) | no | 4 | 10.2 |
| E6 | EN | Treatment | 8 | yes | ok (0.0) | no | 4 | 12.3 |

## Summary

- Answers with markers: 18/18
- All markers in range: 18/18
- Correct answer language: 18/18
- List/markdown leakage: 0/18
- Mean latency: 17.6s
