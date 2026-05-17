## ADDED Requirements

### Requirement: SQLite database stores illness dictionary with ICD-10 codes
The `medassist.db` SQLite database SHALL contain an `illnesses` table with columns: `id` (uuid), `icd_code` (string, ICD-10 code, unique), `name_en` (string), `name_bg` (string), `created_at`. An `illness_aliases` table SHALL store additional names with columns: `id`, `illness_id` (FK), `alias` (string), `language` (`en` | `bg`).

#### Scenario: Illness lookup by English name returns ICD code
- **WHEN** the dictionary is queried with `name_en = "Down Syndrome"`
- **THEN** the record with `icd_code = "Q90"` is returned

#### Scenario: Illness lookup by Bulgarian name returns same record
- **WHEN** the dictionary is queried with `name_bg = "Синдром на Down"`
- **THEN** the same record with `icd_code = "Q90"` is returned

### Requirement: Indexer tags chunks with matching ICD codes at ingest time
During ingestion, the Indexer SHALL scan each chunk's text against the illness dictionary (name_en, name_bg, and all aliases). Any matching ICD codes SHALL be added to the `icd_codes` array in the Qdrant payload.

#### Scenario: Chunk mentioning a known illness is tagged
- **WHEN** a chunk contains the text "Синдром на Down"
- **THEN** the upserted Qdrant payload includes `icd_codes: ["Q90"]`

### Requirement: Query time expansion uses ICD dictionary
When a plugin receives a query, it SHALL check the illness dictionary for matching terms. If found, the search SHALL include both BG and EN names plus all aliases as additional query terms sent to Qdrant.

#### Scenario: Query expansion adds aliases to search
- **WHEN** query is `"trisomy 21"`
- **THEN** the Qdrant search also includes `"Down Syndrome"`, `"Синдром на Down"`, and `"Тризомия 21"`

### Requirement: Dictionary entries can be added via Indexer CLI
The Indexer SHALL expose a CLI command `medassist-indexer dictionary add --icd <code> --en <name> --bg <name>` to add new illness entries without writing SQL directly.

#### Scenario: New illness added via CLI appears in dictionary
- **WHEN** CLI command is run with valid ICD code, EN name, and BG name
- **THEN** the illness appears in the `illnesses` table and is available for query expansion
