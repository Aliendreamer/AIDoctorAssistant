## 1. Fix Section Expansion Trigger

- [x] 1.1 In `RagPluginBase.ExpandBySectionAsync`, add `.Where(c => c.IsSummary)` to the candidates filter so only summary-chunk hits drive section scroll expansion

## 2. Build and Deploy

- [x] 2.1 Run `dotnet build` — confirm zero errors
- [x] 2.2 Ask user to rebuild and restart the web container

## 3. Verify

- [x] 3.1 Re-run the 13 Bulgarian test queries and record pass/fail/partial
- [x] 3.2 Confirm score >= previous baseline (≥ 4 clear passes)
