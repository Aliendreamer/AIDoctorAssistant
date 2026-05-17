using FastEndpoints;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.Web.Endpoints.Dictionary;

public sealed class SearchDictionaryRequest
{
    public string Q { get; set; } = string.Empty;
}

public sealed class SearchDictionaryEndpoint : Endpoint<SearchDictionaryRequest, IReadOnlyList<IllnessEntry>>
{
    private readonly IMedicalDictionary _dictionary;

    public SearchDictionaryEndpoint(IMedicalDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    public override void Configure()
    {
        Get("/api/dictionary/search");
        Roles("Admin", "Doctor");
    }

    public override async Task HandleAsync(SearchDictionaryRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Q))
        {
            AddError(r => r.Q, "Query parameter 'q' is required");
            ThrowIfAnyErrors();
        }

        var results = await _dictionary.SearchAsync(req.Q, ct);
        await Send.OkAsync(results, ct);
    }
}
