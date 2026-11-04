using System.Net;
using System.Text;
using System.Text.Json;
using MedAssist.AI.Plugins;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MedAssist.Tests;

// The overrides must reach Ollama for real, or a "model comparison" would silently test the default
// model. These run the actual SK Ollama connector against a fake HTTP endpoint and read the wire body.
public sealed class RagRunScopeTests
{
    [Fact]
    public void WithoutScope_ProductionSettingsApply()
    {
        var settings = AnswerPromptBuilder.ExecutionSettings;

        Assert.Null(settings.ModelId);
        Assert.Equal(PromptConstants.ContextWindowTokens, settings.ExtensionData!["num_ctx"]);
        Assert.False(settings.ExtensionData.ContainsKey("temperature"));
        Assert.Equal(PromptConstants.MaxExcerpts, RagRunScope.MaxExcerpts);
        Assert.Equal(AnswerPromptBuilder.PromptBudgetTokens, RagRunScope.PromptBudgetTokens);
    }

    [Fact]
    public void Scope_ReplacesSettings_AndDisposeRestoresThem()
    {
        using (RagRunScope.Begin(new RagRunOverrides
        {
            ModelId = "other:7b",
            ContextWindowTokens = 16_384,
            AnswerReserveTokens = 1_000,
            MaxExcerpts = 8,
            Temperature = 0,
        }))
        {
            Assert.Equal("other:7b", AnswerPromptBuilder.ExecutionSettings.ModelId);
            Assert.Equal("other:7b", QueryPreparation.Settings.ModelId);
            Assert.Equal(16_384, AnswerPromptBuilder.ExecutionSettings.ExtensionData!["num_ctx"]);
            Assert.Equal(16_384, QueryPreparation.Settings.ExtensionData!["num_ctx"]);
            Assert.Equal(15_384, RagRunScope.PromptBudgetTokens);
            Assert.Equal(8, RagRunScope.MaxExcerpts);
        }

        Assert.Null(RagRunScope.Current);
        Assert.Null(AnswerPromptBuilder.ExecutionSettings.ModelId);
    }

    [Fact]
    public async Task Overrides_ReachTheOllamaRequest()
    {
        var handler = new CapturingHandler();
        var chat = BuildChat(handler);

        using (RagRunScope.Begin(new RagRunOverrides { ModelId = "other:7b", ContextWindowTokens = 16_384, Temperature = 0.2 }))
        {
            await chat.GetChatMessageContentAsync(new ChatHistory("hi"), AnswerPromptBuilder.ExecutionSettings);
        }

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("other:7b", body.RootElement.GetProperty("model").GetString());
        var options = body.RootElement.GetProperty("options");
        Assert.Equal(16_384, options.GetProperty("num_ctx").GetInt32());
        Assert.Equal(0.2, options.GetProperty("temperature").GetDouble(), 3);
    }

    [Fact]
    public async Task WithoutScope_TheConfiguredModelIsSent()
    {
        var handler = new CapturingHandler();
        var chat = BuildChat(handler);

        await chat.GetChatMessageContentAsync(new ChatHistory("hi"), AnswerPromptBuilder.ExecutionSettings);

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("default:8b", body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task ResponseFormatJson_ReachesTheOllamaRequest()
    {
        var handler = new CapturingHandler();
        var chat = BuildChat(handler);
        var settings = new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object> { ["response_format"] = "json_object" },
        };

        await chat.GetChatMessageContentAsync(new ChatHistory("hi"), settings);

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("json", body.RootElement.GetProperty("format").GetString());
    }

    private static IChatCompletionService BuildChat(HttpMessageHandler handler)
    {
#pragma warning disable SKEXP0070
        var kernel = Microsoft.SemanticKernel.Kernel.CreateBuilder()
            .AddOllamaChatCompletion("default:8b", new HttpClient(handler) { BaseAddress = new Uri("http://ollama.test/") })
            .Build();
#pragma warning restore SKEXP0070
        return kernel.GetRequiredService<IChatCompletionService>();
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            const string Reply = """{"model":"m","created_at":"2026-01-01T00:00:00Z","message":{"role":"assistant","content":"ok"},"done":true,"done_reason":"stop"}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Reply + "\n", Encoding.UTF8, "application/x-ndjson"),
            };
        }
    }
}
