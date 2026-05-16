using MedAssist.AI.Kernel;
using MedAssist.AI.Plugins;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MedAssist.Web.Services;

public sealed class QueryService
{
    private readonly Kernel _kernel;
    private readonly WebSearchPlugin _webSearchPlugin;
    private static readonly Meter _meter = new("MedAssist.Web");
    private static readonly Histogram<double> _queryDuration = _meter.CreateHistogram<double>(
        "query_duration_seconds", unit: "s", description: "Query duration by plugin type");
    private static readonly Counter<long> _qdrantResults = _meter.CreateCounter<long>(
        "qdrant_results_total", description: "Total Qdrant results returned");

    public QueryService(Kernel kernel, HttpClient httpClient, IConfiguration configuration)
    {
        _kernel = kernel;
        var searchEndpoint = configuration["WebSearch:Endpoint"] ?? "http://localhost:8081";
        var allowedDomains = configuration.GetSection("WebSearch:AllowedDomains").Get<List<string>>() ?? [];
        _webSearchPlugin = new WebSearchPlugin(httpClient, searchEndpoint, allowedDomains);
    }

    public async Task<QueryResult> ExecuteAsync(QueryRequest request, CancellationToken cancellationToken = default)
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

            QueryResult result = request.QueryType switch
            {
                QueryType.Symptoms => await InvokePluginAsync<SymptomsPlugin>(query, language, bookIds, cancellationToken),
                QueryType.Disease => await InvokePluginAsync<DiseasePlugin>(query, language, bookIds, cancellationToken),
                QueryType.Treatment => await InvokePluginAsync<TreatmentPlugin>(query, language, bookIds, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(request.QueryType))
            };

            _qdrantResults.Add(result.Sources.Count(s => s.SourceType == SourceType.Book));

            if (request.WebSearchEnabled && result.Sources.Count == 0)
            {
                try
                {
                    var webResults = await _webSearchPlugin.SearchAsync(query, language, cancellationToken);
                    result = result with { Sources = [.. result.Sources, .. webResults] };
                }
                catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException)
                {
                    // PubMed timed out or was unreachable — return RAG results as-is
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
        CancellationToken cancellationToken)
    {
        var pluginName = KernelFactory.PluginName<TPlugin>();
        var func = _kernel.Plugins[pluginName][nameof(SymptomsPlugin.SearchAsync)];
        var result = await _kernel.InvokeAsync(func, new KernelArguments
        {
            ["query"] = query,
            ["language"] = language,
            ["bookIds"] = bookIds
        }, cancellationToken);

        return result.GetValue<QueryResult>() ?? new QueryResult { Answer = "No results found." };
    }
}
