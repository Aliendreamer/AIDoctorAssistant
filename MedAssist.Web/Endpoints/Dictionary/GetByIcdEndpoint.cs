using FastEndpoints;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.Web.Endpoints.Dictionary;

public sealed class GetByIcdRequest
{
    public string Icd { get; set; } = string.Empty;
}

public sealed class GetByIcdEndpoint : Endpoint<GetByIcdRequest, IllnessEntry>
{
    private readonly IMedicalDictionary _dictionary;

    public GetByIcdEndpoint(IMedicalDictionary dictionary)
    {
        _dictionary = dictionary;
    }

    public override void Configure()
    {
        Get("/api/dictionary/{icd}");
        Roles("Admin", "Doctor");
    }

    public override async Task HandleAsync(GetByIcdRequest req, CancellationToken ct)
    {
        var entry = await _dictionary.GetByIcdAsync(req.Icd, ct);
        if (entry is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(entry, ct);
    }
}
