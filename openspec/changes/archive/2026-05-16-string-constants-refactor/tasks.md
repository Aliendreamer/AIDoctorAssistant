## 1. New Constants in MedAssist.Shared

- [x] 1.1 Create `MedAssist.Shared/Constants/OnnxConstants.cs` with nested `Inputs`, `Outputs`, and `Files` static classes
- [x] 1.2 Create `MedAssist.Shared/Constants/IngestionStatus.cs` with `Pending`, `InProgress`, `Indexed`, `Complete`
- [x] 1.3 Add `EnglishName = "english"` and `BulgarianName = "bulgarian"` to `MedAssist.Shared/Constants/LanguageCodes.cs`

## 2. MedAssist.AI Updates

- [x] 2.1 Replace tensor input/output literals and file name literals in `MedAssist.AI/Embedding/MultilingualE5Embedder.cs`; add private `QueryPrefix`/`PassagePrefix` consts
- [x] 2.2 Replace tensor input/output literals and file name literals in `MedAssist.AI/Reranker/CrossEncoderReranker.cs`
- [x] 2.3 Replace all model file name literals in `MedAssist.AI/Embedding/ModelInitializer.cs`
- [x] 2.4 Replace `"english"` and `"bulgarian"` switch arms in `MedAssist.AI/Plugins/RagPluginBase.cs`

## 3. MedAssist.Data Updates

- [x] 3.1 Replace `"pending"` default in `MedAssist.Data/Entities/BookEntity.cs`
- [x] 3.2 Replace `"in_progress"` default in `MedAssist.Data/Entities/IngestionCheckpointEntity.cs`
- [x] 3.3 Replace `HasDefaultValue("pending")` in `MedAssist.Data/Configuration/BookEntityConfiguration.cs`
- [x] 3.4 Replace `HasDefaultValue("in_progress")` in `MedAssist.Data/Configuration/IngestionCheckpointEntityConfiguration.cs`

## 4. MedAssist.Indexer and MedAssist.Web Updates

- [x] 4.1 Replace `"in_progress"` and `"complete"` literals in `MedAssist.Indexer/Ingestion/BookIndexer.cs`
- [x] 4.2 Replace `"indexed"` literal in `MedAssist.Web/Services/BookCatalogService.cs`

## 5. Verification

- [x] 5.1 Build all projects (`dotnet build`) — zero errors, zero warnings
- [x] 5.2 Run all tests (`dotnet test`) — all pass
