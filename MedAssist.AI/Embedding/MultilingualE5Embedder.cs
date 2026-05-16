using System.Text;
using System.Text.Json;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MedAssist.AI.Embedding;

public sealed class MultilingualE5Embedder : IEmbedder, IDisposable
{
    private readonly InferenceSession _session;
    private readonly UnigramTokenizer _tokenizer;
    private const int _maxTokens = 512;
    private const string _queryPrefix = "query: ";
    private const string _passagePrefix = "passage: ";

    public MultilingualE5Embedder(string modelDirectory)
    {
        var modelPath = Path.Combine(modelDirectory, OnnxConstants.Files.ModelOnnx);
        var tokenizerPath = Path.Combine(modelDirectory, OnnxConstants.Files.TokenizerJson);

        _session = new InferenceSession(modelPath, new SessionOptions());
        _tokenizer = new UnigramTokenizer(tokenizerPath);
    }

    public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(Embed(_queryPrefix + text));

    public Task<float[]> EmbedPassageAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(Embed(_passagePrefix + text));

    private float[] Embed(string text)
    {
        var tokenIds = _tokenizer.Encode(text, _maxTokens);
        var inputIds = Array.ConvertAll(tokenIds, id => (long)id);
        var attentionMask = new long[inputIds.Length];
        Array.Fill(attentionMask, 1L);
        var tokenTypeIds = new long[inputIds.Length];

        var seqLen = inputIds.Length;

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(OnnxConstants.Inputs.InputIds, new DenseTensor<long>(inputIds, [1, seqLen])),
            NamedOnnxValue.CreateFromTensor(OnnxConstants.Inputs.AttentionMask, new DenseTensor<long>(attentionMask, [1, seqLen])),
            NamedOnnxValue.CreateFromTensor(OnnxConstants.Inputs.TokenTypeIds, new DenseTensor<long>(tokenTypeIds, [1, seqLen])),
        };

        using var outputs = _session.Run(inputs);
        var lastHiddenState = outputs.First(o => o.Name == OnnxConstants.Outputs.LastHiddenState).AsEnumerable<float>().ToArray();

        int hiddenSize = lastHiddenState.Length / seqLen;
        var pooled = new float[hiddenSize];
        for (var i = 0; i < seqLen; i++)
        {
            for (var j = 0; j < hiddenSize; j++)
            {
                pooled[j] += lastHiddenState[(i * hiddenSize) + j];
            }
        }

        for (var j = 0; j < hiddenSize; j++)
        {
            pooled[j] /= seqLen;
        }

        return Normalize(pooled);
    }

    private static float[] Normalize(float[] vector)
    {
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        return norm < 1e-8f ? vector : Array.ConvertAll(vector, v => v / norm);
    }

    public void Dispose() => _session.Dispose();

    // SentencePiece Unigram tokenizer loaded from HuggingFace tokenizer.json.
    // Uses Viterbi segmentation to find the highest-score tokenization.
    private sealed class UnigramTokenizer
    {
        private const char _spaceMark = '▁'; // ▁ replaces whitespace
        private const int _maxTokenLen = 32;

        private readonly Dictionary<string, (int Id, float Score)> _vocab;
        private readonly int _unkId;
        private readonly int _clsId;
        private readonly int _sepId;

        internal UnigramTokenizer(string tokenizerJsonPath)
        {
            using var stream = File.OpenRead(tokenizerJsonPath);
            using var doc = JsonDocument.Parse(stream);

            var model = doc.RootElement.GetProperty("model");
            _unkId = model.GetProperty("unk_id").GetInt32();

            var vocabArr = model.GetProperty("vocab");
            _vocab = new Dictionary<string, (int, float)>(vocabArr.GetArrayLength(), StringComparer.Ordinal);

            var id = 0;
            foreach (var pair in vocabArr.EnumerateArray())
            {
                var token = pair[0].GetString()!;
                var score = (float)pair[1].GetDouble();
                _vocab[token] = (id, score);
                id++;
            }

            _clsId = _vocab.TryGetValue("<s>", out var cls) ? cls.Id : 0;
            _sepId = _vocab.TryGetValue("</s>", out var sep) ? sep.Id : 2;
        }

        internal int[] Encode(string text, int maxLength)
        {
            // NFKC normalization approximates the SentencePiece precompiled normalizer
            var normalized = text.Normalize(NormalizationForm.FormKC);

            var sb = new StringBuilder(normalized.Length + 1);
            sb.Append(_spaceMark);
            foreach (var c in normalized)
            {
                sb.Append(c == ' ' ? _spaceMark : c);
            }

            var tokenIds = Viterbi(sb.ToString());

            var maxContent = maxLength - 2;
            if (tokenIds.Count > maxContent)
            {
                tokenIds.RemoveRange(maxContent, tokenIds.Count - maxContent);
            }

            var result = new int[tokenIds.Count + 2];
            result[0] = _clsId;
            for (var i = 0; i < tokenIds.Count; i++)
            {
                result[i + 1] = tokenIds[i];
            }
            result[^1] = _sepId;
            return result;
        }

        private List<int> Viterbi(string text)
        {
            var n = text.Length;
            var dp = new float[n + 1];
            Array.Fill(dp, float.NegativeInfinity);
            dp[0] = 0f;

            var prevPos = new int[n + 1];
            var prevToken = new int[n + 1];
            Array.Fill(prevPos, -1);
            Array.Fill(prevToken, _unkId);

            for (var start = 0; start < n; start++)
            {
                if (dp[start] == float.NegativeInfinity)
                {
                    continue;
                }

                var limit = Math.Min(n, start + _maxTokenLen);
                for (var end = start + 1; end <= limit; end++)
                {
                    if (!_vocab.TryGetValue(text[start..end], out var entry))
                    {
                        continue;
                    }

                    var candidate = dp[start] + entry.Score;
                    if (candidate > dp[end])
                    {
                        dp[end] = candidate;
                        prevPos[end] = start;
                        prevToken[end] = entry.Id;
                    }
                }

                var next = start + 1;
                if (dp[next] == float.NegativeInfinity)
                {
                    dp[next] = dp[start] - 100f;
                    prevPos[next] = start;
                    prevToken[next] = _unkId;
                }
            }

            var ids = new List<int>();
            var pos = n;
            while (pos > 0)
            {
                ids.Add(prevToken[pos]);
                pos = prevPos[pos];
            }
            ids.Reverse();
            return ids;
        }
    }
}
