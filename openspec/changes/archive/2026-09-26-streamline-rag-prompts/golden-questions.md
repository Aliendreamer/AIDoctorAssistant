# Golden questions

Run order matters: G12 is a follow-up to G11 (same query type, so it sees G11 in history). The corpus
is ten Bulgarian paediatrics books. Run 1 used English for G1–G6, but the reranker scores English
questions below `MinRetryScore` against this corpus, so they fall back to web search and never
reach the answer prompt; from run 2 on, G1–G6 are Bulgarian. Each run uses its own fresh Doctor
account, so history starts empty (the history-clear endpoint is currently broken).

| Id | Lang | Type | Question |
| --- | --- | --- | --- |
| G1 | BG | Disease | Какво е болест на Кавазаки? |
| G2 | BG | Symptoms | Дете с лаеща кашлица, дрезгав глас и инспираторен стридор |
| G3 | BG | Treatment | Лечение на остър среден отит при деца |
| G4 | BG | GlobalSearch | Бронхиолит при кърмачета |
| G5 | BG | DifferentialDiagnosis | Дете с температура от 5 дни, ягодов език, обрив и конюнктивит |
| G6 | BG | Treatment | Лечение на отравяне с парацетамол при дете |
| G7 | BG | Disease | Какво е муковисцидоза? |
| G8 | BG | Symptoms | Дете с висока температура и петехиален обрив |
| G9 | BG | GlobalSearch | Фебрилни гърчове при деца |
| G10 | BG | DifferentialDiagnosis | Дете с болка в корема, повръщане и температура |
| G11 | BG | Treatment | Лечение на остър гастроентерит с дехидратация при деца |
| G12 | BG | Treatment | А при кърмачета? *(follow-up to G11)* |

## Recorded per answer

- **Markers** — contains at least one `[n]` citation marker.
- **In range** — every marker number is ≤ the number of book sources returned.
- **Language** — answer script matches the question (Cyrillic share > 50% for BG, < 10% for EN).
- **Leakage** — any line starting with a list bullet, number, heading, or containing `**`.
- **Latency** — wall-clock seconds for the `/api/query` call.
