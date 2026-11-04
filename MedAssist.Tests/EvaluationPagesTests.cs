using System.Net;
using System.Security.Claims;
using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;
using MedAssist.Web.Components.Pages.Admin.Evaluation;
using MedAssist.Web.Options;
using MedAssist.Web.Services;
using MedAssist.Web.Services.Evaluation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedAssist.Tests;

// Renders the real Razor pages with real services over SQLite: markup and binding mistakes throw here.
public sealed class EvaluationPagesTests : IDisposable
{
    private readonly EvalTestDb _testDb = new();
    private readonly ServiceProvider _services;
    private readonly EvaluationCoordinator _coordinator = new();

    public EvaluationPagesTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AI:Ollama:ModelName"] = "qwen3:8b" })
            .Build());
        services.AddOptions<RagOptions>();
        services.AddOptions<ModelsOptions>();
        services.AddScoped<MedAssistDbContext>(_ => _testDb.CreateContext());
        services.AddScoped<BookCatalogService>();
        services.AddTransient<EvaluationRepository>();
        services.AddSingleton<IHttpClientFactory>(new TagsClientFactory());
        services.AddTransient<OllamaModelCatalog>();
        services.AddScoped<EvaluationSnapshotFactory>();
        services.AddSingleton(new GoldenSet(
            [new EvalCase { Id = "G1", Language = "EN", Question = "q", RelevantTerms = [["x"]], RequiredFacts = ["f"] }], "hash"));
        services.AddSingleton(_coordinator);
        services.AddScoped<AdminEvaluationService>();
        services.AddSingleton<AuthenticationStateProvider, AdminAuthState>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        _services = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        _testDb.Dispose();
    }

    private sealed class TagsClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new TagsHandler()) { BaseAddress = new Uri("http://ollama.test/") };

        private sealed class TagsHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"models":[{"name":"qwen3:8b"},{"name":"medgemma:4b"}]}""", System.Text.Encoding.UTF8, "application/json"),
                });
        }
    }

    private sealed class AdminAuthState : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "test"))));
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://app.test/", "http://app.test/admin/evaluation");

        protected override void NavigateToCore(string uri, NavigationOptions options) => Uri = ToAbsoluteUri(uri).ToString();
    }

    // HtmlRenderer rejects components declaring a render mode, so this renderer ignores the mode.
    private sealed class PageRenderer(IServiceProvider services, ILoggerFactory loggers) : StaticHtmlRenderer(services, loggers)
    {
        protected override IComponent ResolveComponentForRenderMode(
            Type componentType, int? parentComponentId, IComponentActivator componentActivator, IComponentRenderMode renderMode) =>
            componentActivator.CreateInstance(componentType);

        public Task<string> RenderAsync<T>(ParameterView parameters) where T : IComponent =>
            Dispatcher.InvokeAsync(async () =>
            {
                var content = BeginRenderingComponent(typeof(T), parameters);
                await content.QuiescenceTask;
                using var writer = new StringWriter();
                content.WriteHtmlTo(writer);
                return writer.ToString();
            });
    }

    private async Task<string> RenderAsync<T>(Dictionary<string, object?>? parameters = null) where T : IComponent
    {
        await using var renderer = new PageRenderer(_services, NullLoggerFactory.Instance);
        return await renderer.RenderAsync<T>(parameters is null ? ParameterView.Empty : ParameterView.FromDictionary(parameters));
    }

    private async Task<Guid> SeedRunAsync(
        string note, EvalRunStatus status, EvalRunSummary? summary, EvalRunSnapshot? snapshot = null, int minutesAgo = 0, EvalCaseResult? result = null)
    {
        await using var db = _testDb.CreateContext();
        var repository = new EvaluationRepository(db);
        var id = Guid.NewGuid();
        await repository.AddRunAsync(new EvaluationRunEntity
        {
            Id = id,
            Note = note,
            Status = status,
            Phase = status == EvalRunStatus.Running ? EvaluationRunner.AnsweringPhase : string.Empty,
            Done = 1,
            Total = 2,
            StartedBy = "admin",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-minutesAgo),
            Snapshot = EvalJson.Serialize(snapshot ?? new EvalRunSnapshot { Model = "qwen3:8b", ContextWindowTokens = 8_192, TotalCases = 1 }),
            Summary = summary is null ? null : EvalJson.Serialize(summary),
        });
        if (result is not null)
        {
            await repository.SaveResultAsync(id, result.CaseId, 0, EvalJson.Serialize(result));
        }

        return id;
    }

    private static EvalCaseResult Result() => new()
    {
        CaseId = "G1",
        Question = "Какво е болест на Кавазаки?",
        Answer = "Васкулит [1].",
        Outcome = RagOutcome.Answered,
        FailureReasons = ["No citation markers"],
        Facts = [new FactCheck { Fact = "Reference fact", InExcerpts = true, InAnswer = false }],
        Claims = [new ClaimCheck { Claim = "Unsupported sentence", Supported = false }],
        Excerpts = [new EvalExcerpt { Number = 1, Source = "Book — Section", Relevant = true, Text = "Excerpt body" }],
    };

    [Fact]
    public async Task ListPage_EmptyShowsTheFormAndInstalledModels()
    {
        var html = await RenderAsync<Evaluation>();

        Assert.Contains("Run evaluation", html);
        Assert.Contains("medgemma:4b", html);
        Assert.Contains("No runs yet", html);
        Assert.Contains("1 of 1 reference answers are unreviewed", html);
    }

    [Fact]
    public async Task ListPage_ShowsCompletedRunsAndVerdicts()
    {
        await SeedRunAsync("baseline run", EvalRunStatus.Completed, new EvalRunSummary { Cases = 20, Passed = 18, PassRate = 0.9 }, minutesAgo: 10);
        await SeedRunAsync("bad change", EvalRunStatus.Completed, new EvalRunSummary { Cases = 20, Passed = 10, PassRate = 0.5 });

        var html = await RenderAsync<Evaluation>();

        Assert.Contains("baseline run", html);
        Assert.Contains("10/20", html);
        Assert.Contains("Regressed", html);
        Assert.Contains("No baseline", html);
    }

    [Fact]
    public async Task ListPage_ActiveRunReplacesTheFormWithProgress()
    {
        _coordinator.TryReserve(await SeedRunAsync("in flight", EvalRunStatus.Running, null));

        var html = await RenderAsync<Evaluation>();

        Assert.Contains("Cancel run", html);
        Assert.Contains("width:50%", html);
        Assert.DoesNotContain("Run evaluation", html);
    }

    [Fact]
    public async Task ListPage_StaleActiveRowDoesNotLockTheForm()
    {
        await SeedRunAsync("orphaned", EvalRunStatus.Running, null);

        var html = await RenderAsync<Evaluation>();

        Assert.Contains("Run evaluation", html);
        Assert.DoesNotContain("Cancel run", html);
    }

    [Fact]
    public async Task RunPage_JudgeOffAgainstAJudgedBaselineExplainsInconclusive()
    {
        await SeedRunAsync("judged", EvalRunStatus.Completed, new EvalRunSummary { Cases = 1, PassRate = 1, FactRecall = 0.9 }, minutesAgo: 10);
        var id = await SeedRunAsync("unjudged", EvalRunStatus.Completed, new EvalRunSummary { Cases = 1, PassRate = 1 });

        var html = await RenderAsync<EvaluationRun>(new() { [nameof(EvaluationRun.Id)] = id });

        Assert.Contains("Inconclusive", html);
        Assert.Contains("cannot rule out a regression", html);
        Assert.Contains("not measured", html);
    }

    [Fact]
    public async Task RunPage_ShowsComparisonAndQuestionDetail()
    {
        await SeedRunAsync("old", EvalRunStatus.Completed, new EvalRunSummary { Cases = 1, PassRate = 1 },
            new EvalRunSnapshot { Model = "qwen3:8b", ContextWindowTokens = 8_192 }, minutesAgo: 10);
        var id = await SeedRunAsync("new", EvalRunStatus.Completed, new EvalRunSummary { Cases = 1, PassRate = 0 },
            new EvalRunSnapshot { Model = "medgemma:4b", ContextWindowTokens = 16_384 }, result: Result());

        var html = await RenderAsync<EvaluationRun>(new() { [nameof(EvaluationRun.Id)] = id });

        Assert.Contains("Regressed", html);
        Assert.Contains("What changed", html);
        Assert.Contains("medgemma:4b", html);
        Assert.Contains("Questions passed", html);
        Assert.Contains("-100 pts", html);
        Assert.Contains("Reference fact", html);
        Assert.Contains("Unsupported sentence", html);
        Assert.Contains("Excerpt body", html);
    }

    [Fact]
    public async Task RunPage_RunningRunShowsProgressAndPartialAnswers()
    {
        var id = await SeedRunAsync("running", EvalRunStatus.Running, null, result: Result());

        var html = await RenderAsync<EvaluationRun>(new() { [nameof(EvaluationRun.Id)] = id });

        Assert.Contains("Answering", html);
        Assert.Contains("Какво е болест", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("Delete run", html);
    }

    [Fact]
    public async Task RunPage_FailedRunShowsItsError()
    {
        await using (var db = _testDb.CreateContext())
        {
            var id = await SeedRunAsync("failed", EvalRunStatus.Running, null);
            await new EvaluationRepository(db).FinishAsync(id, EvalRunStatus.Failed, null, "ollama down");

            var html = await RenderAsync<EvaluationRun>(new() { [nameof(EvaluationRun.Id)] = id });

            Assert.Contains("ollama down", html);
            Assert.Contains("Delete run", html);
        }
    }

    [Fact]
    public async Task RunPage_UnknownRun()
    {
        var html = await RenderAsync<EvaluationRun>(new() { [nameof(EvaluationRun.Id)] = Guid.NewGuid() });

        Assert.Contains("no longer exists", html);
    }

    [Fact]
    public async Task Start_RejectsAModelOllamaDoesNotHave()
    {
        using var scope = _services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AdminEvaluationService>();

        var (success, message, _) = await sut.StartAsync(
            new EvalRunRequest { Overrides = new RagRunOverrides { ModelId = "typo:1b" } }, "admin");

        Assert.False(success);
        Assert.Contains("typo:1b", message);
        Assert.Null(_coordinator.ActiveRunId);
    }

    [Fact]
    public async Task Start_RecordsResolvedSettingsAndBlocksASecondRun()
    {
        using var scope = _services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<AdminEvaluationService>();

        var first = await sut.StartAsync(
            new EvalRunRequest { Note = "  more context ", Overrides = new RagRunOverrides { ContextWindowTokens = 16_384 } }, "admin");
        var second = await sut.StartAsync(new EvalRunRequest(), "admin");

        Assert.True(first.Success);
        Assert.False(second.Success);
        var view = (await sut.ListAsync()).Single();
        Assert.Equal("more context", view.Note);
        Assert.Equal(16_384, view.Snapshot.ContextWindowTokens);
        Assert.Equal("qwen3:8b", view.Snapshot.Model);
        Assert.Equal("qwen3:8b", view.Snapshot.JudgeModel);
        Assert.True(view.Snapshot.Overrides.BookOnly);
    }
}
