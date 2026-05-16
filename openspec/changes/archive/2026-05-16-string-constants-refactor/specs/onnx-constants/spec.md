## ADDED Requirements

### Requirement: OnnxConstants provides typed names for ONNX tensor inputs
`MedAssist.Shared.Constants.OnnxConstants.Inputs` SHALL expose `const string` fields for every ONNX model input tensor name used in this project: `InputIds` (`"input_ids"`), `AttentionMask` (`"attention_mask"`), `TokenTypeIds` (`"token_type_ids"`).

#### Scenario: Embedder uses constant for input tensor names
- **WHEN** `MultilingualE5Embedder` builds its `NamedOnnxValue` list
- **THEN** each tensor name SHALL reference `OnnxConstants.Inputs.*` rather than a raw string literal

#### Scenario: Reranker uses constant for input tensor names
- **WHEN** `CrossEncoderReranker` builds its `NamedOnnxValue` list
- **THEN** each tensor name SHALL reference `OnnxConstants.Inputs.*` rather than a raw string literal

### Requirement: OnnxConstants provides typed names for ONNX tensor outputs
`MedAssist.Shared.Constants.OnnxConstants.Outputs` SHALL expose `const string` fields for every ONNX model output tensor name: `LastHiddenState` (`"last_hidden_state"`), `Logits` (`"logits"`).

#### Scenario: Embedder uses constant for output tensor name
- **WHEN** `MultilingualE5Embedder` selects the output tensor by name
- **THEN** it SHALL reference `OnnxConstants.Outputs.LastHiddenState`

#### Scenario: Reranker uses constant for output tensor name
- **WHEN** `CrossEncoderReranker` selects the output tensor by name
- **THEN** it SHALL reference `OnnxConstants.Outputs.Logits`

### Requirement: OnnxConstants provides typed names for model file names
`MedAssist.Shared.Constants.OnnxConstants.Files` SHALL expose `const string` fields for every model file name: `ModelOnnx` (`"model.onnx"`), `ModelOnnxData` (`"model.onnx_data"`), `TokenizerJson` (`"tokenizer.json"`), `TokenizerConfigJson` (`"tokenizer_config.json"`), `SpecialTokensMapJson` (`"special_tokens_map.json"`).

#### Scenario: ModelInitializer uses constants for all file names
- **WHEN** `ModelInitializer` declares its embedder and reranker file arrays
- **THEN** every file name entry SHALL reference `OnnxConstants.Files.*`

#### Scenario: Embedder and Reranker constructors use constants for model paths
- **WHEN** `MultilingualE5Embedder` or `CrossEncoderReranker` builds `modelPath` and `tokenizerPath`
- **THEN** the file name segments SHALL reference `OnnxConstants.Files.ModelOnnx` and `OnnxConstants.Files.TokenizerJson`
