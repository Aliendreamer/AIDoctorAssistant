using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.RegularExpressions;
using MedAssist.AI.Kernel;
using MedAssist.AI.Plugins;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MedAssist.Web.Services;

public sealed partial class QueryService
{
    private readonly Kernel _kernel;
    private readonly BookCatalogService _bookCatalog;
    private readonly HttpClient _httpClient;
    private readonly WebSearchPlugin _webSearchPlugin;
    private readonly ChatHistoryRepository _chatHistory;

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex CollapseWhitespaceRegex();
    private static readonly Meter _meter = new("MedAssist.Web");
    private static readonly Histogram<double> _queryDuration = _meter.CreateHistogram<double>(
        "query_duration_seconds", unit: "s", description: "Query duration by plugin type");
    private static readonly Counter<long> _qdrantResults = _meter.CreateCounter<long>(
        "qdrant_results_total", description: "Total Qdrant results returned");

    public QueryService(Kernel kernel, BookCatalogService bookCatalog, HttpClient httpClient, IConfiguration configuration, ChatHistoryRepository chatHistory)
    {
        _kernel = kernel;
        _bookCatalog = bookCatalog;
        _httpClient = httpClient;
        _chatHistory = chatHistory;
        var searchEndpoint = configuration["WebSearch:Endpoint"] ?? "http://localhost:8081";
        var allowedDomains = configuration.GetSection("WebSearch:AllowedDomains").Get<List<string>>() ?? [];
        _webSearchPlugin = new WebSearchPlugin(httpClient, searchEndpoint, allowedDomains);
    }

    public async Task<QueryResult> ExecuteAsync(QueryRequest request, string? userId = null, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var pluginType = request.QueryType.ToString();

        try
        {
            var language = request.Language switch
            {
                LanguageFilter.English => LanguageCodes.English,
                LanguageFilter.Bulgarian => LanguageCodes.Bulgarian,
                _ => LanguageCodes.Both
            };

            var bookIds = request.BookIds?.ToArray();
            var query = request.Query;
            var queryTypeKey = request.QueryType.ToString().ToLowerInvariant();

            var allBooks = await _bookCatalog.GetAllBooksAsync(cancellationToken);
            var books = bookIds is { Length: > 0 }
                ? allBooks.Where(b => bookIds.Contains(b.BookId)).ToArray()
                : allBooks.ToArray();

            IReadOnlyList<ChatMessageDto> history = [];
            if (userId is not null)
            {
                var recent = await _chatHistory.GetRecentAsync(userId, queryTypeKey, 10, cancellationToken);
                history = recent.Select(m => new ChatMessageDto(m.Role, m.Content)).ToList();
            }

            QueryResult result = request.QueryType switch
            {
                QueryType.Symptoms => await InvokePluginAsync<SymptomsPlugin>(query, language, bookIds, books, history, cancellationToken),
                QueryType.Disease => await InvokePluginAsync<DiseasePlugin>(query, language, bookIds, books, history, cancellationToken),
                QueryType.Treatment => await InvokePluginAsync<TreatmentPlugin>(query, language, bookIds, books, history, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(request.QueryType))
            };

            _qdrantResults.Add(result.Sources.Count(s => s.SourceType == SourceType.Book));

            if (userId is not null)
            {
                var now = DateTimeOffset.UtcNow;
                await _chatHistory.AddMessageAsync(new() { UserId = userId, QueryType = queryTypeKey, Role = "user", Content = query, CreatedAt = now }, cancellationToken);
                await _chatHistory.AddMessageAsync(new() { UserId = userId, QueryType = queryTypeKey, Role = "assistant", Content = result.Answer, CreatedAt = now.AddMicroseconds(1) }, cancellationToken);
            }

            if (request.WebSearchEnabled)
            {
                try
                {
                    var webResults = await _webSearchPlugin.SearchAsync(query, language, cancellationToken);
                    if (webResults.Count > 0)
                    {
                        result = await EnrichWithWebAsync(result, webResults, query, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
                {
                    // SearXNG timed out or was unreachable — return book results as-is
                }
            }

            return result;
        }
        finally
        {
            sw.Stop();
            _queryDuration.Record(sw.Elapsed.TotalSeconds, new KeyValuePair<string, object?>("plugin_type", pluginType));
        }
    }

    private async Task<QueryResult> InvokePluginAsync<TPlugin>(
        string query,
        string language,
        string[]? bookIds,
        BookInfo[] books,
        IReadOnlyList<ChatMessageDto> conversationHistory,
        CancellationToken cancellationToken)
    {
        var pluginName = KernelFactory.PluginName<TPlugin>();
        var func = _kernel.Plugins[pluginName]["Search"];
        var result = await _kernel.InvokeAsync(func, new KernelArguments
        {
            ["query"] = query,
            ["language"] = language,
            ["bookIds"] = bookIds,
            ["books"] = books,
            ["conversationHistory"] = conversationHistory
        }, cancellationToken);

        return result.GetValue<QueryResult>() ?? new QueryResult { Answer = "No results found." };
    }

    private async Task<QueryResult> EnrichWithWebAsync(
        QueryResult bookResult,
        IReadOnlyList<SourceCitation> webSources,
        string query,
        CancellationToken cancellationToken)
    {
        var fetchTasks = webSources
            .Where(s => !string.IsNullOrEmpty(s.Url))
            .Take(3)
            .Select(s => FetchPageSnippetAsync(s.Url!, cancellationToken));

        var snippets = await Task.WhenAll(fetchTasks);

        var webContext = new StringBuilder();
        for (var i = 0; i < webSources.Count && i < snippets.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(snippets[i]))
            {
                continue;
            }

            webContext.AppendLine($"[{webSources[i].ArticleTitle} — {webSources[i].Url}]");
            webContext.AppendLine(snippets[i]);
            webContext.AppendLine();
        }

        if (webContext.Length == 0)
        {
            return bookResult with { Sources = [.. bookResult.Sources, .. webSources] };
        }

        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are MedAssist, a clinical decision support assistant. " +
            "Synthesize a single cohesive answer from the book-based answer and the web excerpts below. " +
            "Prefer book sources. Cite web sources by article title when you use them. " +
            "Be direct, clinical, and use markdown where it aids clarity.");
        history.AddUserMessage(
            $"Question: {query}\n\n" +
            $"## Answer from indexed books\n\n{bookResult.Answer}\n\n" +
            $"## Additional web excerpts\n\n{webContext}");

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var response = await chat.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
        var enrichedAnswer = response.Content ?? bookResult.Answer;

        return new QueryResult
        {
            Answer = enrichedAnswer,
            Sources = [.. bookResult.Sources, .. webSources]
        };
    }

    private async Task<string?> FetchPageSnippetAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var html = await _httpClient.GetStringAsync(url, cts.Token);
            var text = HtmlTagRegex().Replace(html, " ");
            text = CollapseWhitespaceRegex().Replace(text, " ").Trim();
            return text.Length > 800 ? text[..800] : text;
        }
        catch
        {
            return null;
        }
    }
}
