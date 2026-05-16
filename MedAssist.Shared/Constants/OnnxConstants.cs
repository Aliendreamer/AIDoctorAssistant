namespace MedAssist.Shared.Constants;

public static class OnnxConstants
{
    public static class Inputs
    {
        public const string InputIds = "input_ids";
        public const string AttentionMask = "attention_mask";
        public const string TokenTypeIds = "token_type_ids";
    }

    public static class Outputs
    {
        public const string LastHiddenState = "last_hidden_state";
        public const string Logits = "logits";
    }

    public static class Files
    {
        public const string ModelOnnx = "model.onnx";
        public const string ModelOnnxData = "model.onnx_data";
        public const string TokenizerJson = "tokenizer.json";
        public const string TokenizerConfigJson = "tokenizer_config.json";
        public const string SpecialTokensMapJson = "special_tokens_map.json";
    }
}
