## 1. MinerU client + options

- [x] 1.1 Add `MinerUOptions` (MedAssist.Web/Options): `ServiceUrl=http://mineru:8000`, `Backend=pipeline`, `Method=ocr`, `ConversionTimeoutMinutes=120`
- [x] 1.2 Add `MinerUClient` (MedAssist.AI/Ingestion) with `Task<string> ConvertToMarkdownAsync(string filePath, CancellationToken)`: multipart `POST {ServiceUrl}/file_parse` (`files`, `backend`, `parse_method`, `return_md=true`, `return_content_list=false`); parse `results.<firstKey>.md`; throw on non-2xx or missing md (name the file)

## 2. Tests (TDD)

- [x] 2.1 Add `MinerUClientTests` (replaces `MarkerClientTests`): request posts to `/file_parse` with `return_md=true` + `backend`/`parse_method`; response `results.<key>.md` is returned; non-2xx throws; missing-`md` throws — RED→GREEN, 4 tests
- [x] 2.2 Delete `MarkerClientTests.cs`

## 3. Wiring

- [x] 3.1 DI (`ServiceCollectionExtensions`): replaced `Configure<MarkerOptions>` + `AddHttpClient("marker")` + `AddTransient<MarkerClient>` with `Configure<MinerUOptions>` + `AddHttpClient("mineru")` (BaseAddress `ServiceUrl`, `Timeout = ConversionTimeoutMinutes`) + `AddTransient<MinerUClient>`
- [x] 3.2 `IngestionWorker.RunIndexAsync`: on cache miss, `markdown = await mineru.ConvertToMarkdownAsync(job.FilePath, ct)` then write to the `Books:MdPath` `.md` cache (fixes the prior path-as-markdown write)
- [x] 3.3 `IngestionWorker.RunExtractAsync`: `markdown = await mineru.ConvertToMarkdownAsync(job.FilePath, ct)`, write to the resolved `.md` path, then mark done
- [x] 3.4 Config: replaced the `Marker` section in `config/appsettings.shared.json` with a `MinerU` section (`ServiceUrl`, `Backend`, `Method`, `ConversionTimeoutMinutes`)

## 4. Remove Marker

- [x] 4.1 Deleted `MarkerClient.cs` and `MarkerOptions.cs` (no remaining code references)
- [x] 4.2 `docker-compose.yml`: removed the `marker` service, `web`'s `depends_on: marker`, and the `marker-models` volume

## 5. Verify

- [x] 5.1 `dotnet build MedAssist.slnx` — 0 warnings/0 errors
- [x] 5.2 `dotnet test MedAssist.Tests` — all green (163 passed; −6 Marker, +4 MinerU)
- [x] 5.3 `openspec validate migrate-marker-to-mineru --strict` — valid
- [ ] 5.4 (stack) `docker compose build web` succeeds with no marker; `docker compose up -d web`; run one query end-to-end
