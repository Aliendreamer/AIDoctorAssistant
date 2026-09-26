# Golden run — run 2 — new prompts, 8k context, language line last

Run at 2026-09-26 13:19 against <http://localhost:8081>, model `qwen3:8b`.

| Id | Lang | Type | Markers | In range | Language | Leakage | Book sources | Latency (s) |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| G1 | BG | Disease | 10 | yes | ok (0.99) | no | 5 | 43.2 |
| G2 | BG | Symptoms | 13 | yes | ok (0.98) | no | 4 | 24.6 |
| G3 | BG | Treatment | 7 | yes | ok (0.97) | no | 5 | 25.3 |
| G4 | BG | GlobalSearch | 14 | yes | ok (0.98) | no | 5 | 32.6 |
| G5 | BG | DifferentialDiagnosis | 8 | yes | ok (0.97) | no | 4 | 31.6 |
| G6 | BG | Treatment | 5 | yes | ok (0.97) | no | 5 | 38.6 |
| G7 | BG | Disease | 5 | yes | ok (0.92) | no | 3 | 27.2 |
| G8 | BG | Symptoms | 5 | yes | ok (1.0) | no | 5 | 51.0 |
| G9 | BG | GlobalSearch | 11 | yes | ok (1.0) | no | 5 | 39.2 |
| G10 | BG | DifferentialDiagnosis | 15 | yes | ok (1.0) | no | 5 | 49.1 |
| G11 | BG | Treatment | 13 | yes | ok (0.95) | no | 5 | 45.6 |
| G12 | BG | Treatment | 15 | yes | ok (0.95) | no | 5 | 45.3 |

## Summary

- Answers with markers: 12/12
- All markers in range: 12/12
- Correct answer language: 12/12
- List/markdown leakage: 0/12
- Mean latency: 37.8s
