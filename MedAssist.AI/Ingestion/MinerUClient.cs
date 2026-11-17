using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MedAssist.AI.Ingestion;

/// <summary>
/// Client for the shared MinerU OCR service. Converts a PDF to markdown with a single synchronous
/// <c>POST {ServiceUrl}/file_parse</c> (multipart: <c>files</c>, <c>backend</c>, <c>parse_method</c>,
/// <c>return_md=true</c>, <c>return_content_list=true</c>). The markdown is built from
/// <c>results.&lt;firstKey&gt;.content_list</c> with a page marker wherever a block's <c>page_idx</c> changes
/// (change ingest-docx-and-page-citations); without a usable content list it falls back to the page-less
/// <c>results.&lt;firstKey&gt;.md</c>.
/// </summary>
public sealed class MinerUClient
{
    private readonly HttpClient _httpClient;
    private readonly string _backend;
    private readonly string _parseMethod;
    private readonly ILogger<MinerUClient> _logger;

    public MinerUClient(HttpClient httpClient, string backend, string parseMethod, ILogger<MinerUClient> logger)
    {
        _httpClient = httpClient;
        _backend = backend;
        _parseMethod = parseMethod;
        _logger = logger;
    }

    public async Task<string> ConvertToMarkdownAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(filePath);
        _logger.LogInformation("MinerU conversion started for {FileName} (backend={Backend}, method={Method})",
            fileName, _backend, _parseMethod);

        using var form = new MultipartFormDataContent();
        await using var pdfStream = File.OpenRead(filePath);
        var pdfContent = new StreamContent(pdfStream);
        pdfContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdfContent, "files", fileName);
        form.Add(new StringContent(_backend), "backend");
        form.Add(new StringContent(_parseMethod), "parse_method");
        form.Add(new StringContent("true"), "return_md");
        form.Add(new StringContent("true"), "return_content_list");

        using var response = await _httpClient.PostAsync("/file_parse", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        // MinerU's /file_parse returns { "results": { "<file>": { "md": "..." } } }.
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        if (!payload.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"MinerU response for '{fileName}' has no 'results' object.");
        }

        var firstResult = results.EnumerateObject().FirstOrDefault();
        if (firstResult.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"MinerU response for '{fileName}' contains an empty 'results' object.");
        }

        if (TryRenderContentList(firstResult.Value, out var rendered))
        {
            _logger.LogInformation("MinerU converted {FileName}: {Chars} markdown chars from the content list", fileName, rendered.Length);
            return rendered;
        }

        if (!firstResult.Value.TryGetProperty("md", out var mdEl) || mdEl.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"MinerU response for '{fileName}' is missing markdown ('md').");
        }

        var markdown = mdEl.GetString() ?? string.Empty;
        _logger.LogWarning("MinerU returned no usable content list for {FileName}; using page-less markdown ({Chars} chars)",
            fileName, markdown.Length);
        return markdown;
    }

    /// <summary>
    /// Builds markdown from MinerU's content list, writing a page marker for the 1-based PDF page whenever
    /// <c>page_idx</c> changes. MinerU returns the list either as an array or as a JSON-encoded string.
    /// </summary>
    internal static bool TryRenderContentList(JsonElement result, out string markdown)
    {
        markdown = string.Empty;
        if (!result.TryGetProperty("content_list", out var listEl))
        {
            return false;
        }

        JsonDocument? owned = null;
        try
        {
            if (listEl.ValueKind == JsonValueKind.String)
            {
                owned = JsonDocument.Parse(listEl.GetString() ?? "[]");
                listEl = owned.RootElement;
            }

            if (listEl.ValueKind != JsonValueKind.Array || listEl.GetArrayLength() == 0)
            {
                return false;
            }

            var parts = new List<string>();
            int? currentPage = null;
            foreach (var block in listEl.EnumerateArray())
            {
                var rendered = RenderBlock(block);
                if (rendered.Count == 0)
                {
                    continue;
                }

                if (block.TryGetProperty("page_idx", out var pageEl) && pageEl.TryGetInt32(out var pageIdx)
                    && pageIdx + 1 != currentPage)
                {
                    currentPage = pageIdx + 1;
                    parts.Add(PageMarker.Format(new PageRef(currentPage.Value, null)));
                }

                parts.AddRange(rendered);
            }

            markdown = string.Join("\n\n", parts);
            return parts.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
        finally
        {
            owned?.Dispose();
        }
    }

    private static List<string> RenderBlock(JsonElement block)
    {
        var type = block.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
        var parts = new List<string>();
        switch (type)
        {
            case "text":
                var text = Str(block, "text");
                if (text.Length > 0)
                {
                    var level = block.TryGetProperty("text_level", out var levelEl) && levelEl.TryGetInt32(out var l) ? l : 0;
                    parts.Add(level > 0 ? $"{new string('#', Math.Min(level, 6))} {text}" : text);
                }

                break;
            case "list":
                var items = Strings(block, "list_items");
                if (items.Count > 0)
                {
                    parts.Add(string.Join("\n", items));
                }

                break;
            case "image":
                parts.AddRange(Strings(block, "image_caption"));
                parts.AddRange(Strings(block, "img_caption"));
                parts.AddRange(Strings(block, "image_footnote"));
                break;
            case "table":
                parts.AddRange(Strings(block, "table_caption"));
                if (Str(block, "table_body") is { Length: > 0 } body)
                {
                    parts.Add(body);
                }

                parts.AddRange(Strings(block, "table_footnote"));
                break;
            case "equation":
                if (Str(block, "text") is { Length: > 0 } equation)
                {
                    parts.Add(equation);
                }

                break;
            // header, footer, page_number, discarded and unknown blocks carry no book content.
        }

        return parts;
    }

    private static string Str(JsonElement block, string name) =>
        block.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? (el.GetString() ?? string.Empty).Trim() : string.Empty;

    private static List<string> Strings(JsonElement block, string name) =>
        block.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Array
            ? el.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => (e.GetString() ?? string.Empty).Trim())
                .Where(t => t.Length > 0)
                .ToList()
            : [];
}
