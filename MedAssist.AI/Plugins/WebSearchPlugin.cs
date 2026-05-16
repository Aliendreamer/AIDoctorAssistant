using MedAssist.Shared.Models;
using Microsoft.SemanticKernel;
using System.Net.Http.Json;
using System.Xml.Linq;

namespace MedAssist.AI.Plugins;

public sealed class WebSearchPlugin
{
    private readonly HttpClient _httpClient;
    private const string _eSearchBase = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi";
    private const string _eFetchBase = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi";

    public WebSearchPlugin(HttpClient httpClient) => _httpClient = httpClient;

    [KernelFunction, System.ComponentModel.Description("Search PubMed for medical literature when book sources are insufficient.")]
    public async Task<IReadOnlyList<SourceCitation>> SearchAsync(
        [System.ComponentModel.Description("Medical query to search")] string query,
        [System.ComponentModel.Description("Language preference: en, bg, both")] string language = "en",
        CancellationToken cancellationToken = default)
    {
        var pmids = await SearchPubMedAsync(query, cancellationToken);
        if (pmids.Count == 0)
        {
            return [];
        }

        return await FetchSummariesAsync(pmids, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> SearchPubMedAsync(string query, CancellationToken cancellationToken)
    {
        var url = $"{_eSearchBase}?db=pubmed&term={Uri.EscapeDataString(query)}&retmax=5&retmode=xml";
        var response = await _httpClient.GetStringAsync(url, cancellationToken);
        var xml = XDocument.Parse(response);
        return xml.Descendants("Id").Select(e => e.Value).ToList();
    }

    private async Task<IReadOnlyList<SourceCitation>> FetchSummariesAsync(
        IReadOnlyList<string> pmids,
        CancellationToken cancellationToken)
    {
        var ids = string.Join(",", pmids);
        var url = $"{_eFetchBase}?db=pubmed&id={ids}&rettype=abstract&retmode=xml";
        var response = await _httpClient.GetStringAsync(url, cancellationToken);
        var xml = XDocument.Parse(response);

        return xml.Descendants("PubmedArticle").Select(article =>
        {
            var pmid = article.Descendants("PMID").FirstOrDefault()?.Value ?? string.Empty;
            var title = article.Descendants("ArticleTitle").FirstOrDefault()?.Value ?? "Untitled";

            return new SourceCitation
            {
                SourceType = SourceType.Web,
                SourceName = "PubMed",
                ArticleTitle = title,
                Url = $"https://pubmed.ncbi.nlm.nih.gov/{pmid}/"
            };
        }).ToList();
    }
}
