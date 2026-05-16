using MedAssist.AI.Plugins;
using MedAssist.Shared.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;

namespace MedAssist.AI.Kernel;

public static class KernelFactory
{
    private const string _pluginSuffix = "Plugin";

    public static string PluginName<T>() => typeof(T).Name.Replace(_pluginSuffix, string.Empty);

    public static Microsoft.SemanticKernel.Kernel Build(
        IConfiguration configuration,
        IMedicalDictionary dictionary,
        IVectorStore vectorStore,
        IEmbedder embedder,
        ISparseVectorizer sparseVectorizer,
        ICrossEncoderReranker reranker)
    {
        var provider = configuration["AI:ModelProvider"]
            ?? throw new InvalidOperationException("AI:ModelProvider configuration is required.");

        var builder = Microsoft.SemanticKernel.Kernel.CreateBuilder();

        if (provider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = configuration["AI:Ollama:Endpoint"]
                ?? throw new InvalidOperationException("AI:Ollama:Endpoint configuration is required.");
            var modelName = configuration["AI:Ollama:ModelName"]
                ?? throw new InvalidOperationException("AI:Ollama:ModelName configuration is required.");

#pragma warning disable SKEXP0070
            builder.AddOllamaChatCompletion(modelName, new Uri(endpoint));
#pragma warning restore SKEXP0070
        }
        else
        {
            throw new InvalidOperationException($"Unsupported AI provider: '{provider}'. Supported: ollama");
        }

        var kernel = builder.Build();

        kernel.Plugins.AddFromObject(new SymptomsPlugin(dictionary, vectorStore, embedder, sparseVectorizer, reranker), PluginName<SymptomsPlugin>());
        kernel.Plugins.AddFromObject(new DiseasePlugin(dictionary, vectorStore, embedder, sparseVectorizer, reranker), PluginName<DiseasePlugin>());
        kernel.Plugins.AddFromObject(new TreatmentPlugin(dictionary, vectorStore, embedder, sparseVectorizer, reranker), PluginName<TreatmentPlugin>());

        return kernel;
    }
}
