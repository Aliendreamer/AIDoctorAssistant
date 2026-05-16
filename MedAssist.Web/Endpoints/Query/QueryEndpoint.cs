using FastEndpoints;
using MedAssist.Shared.Models;
using MedAssist.Web.Services;

namespace MedAssist.Web.Endpoints.Query;

public sealed class QueryEndpoint : Endpoint<QueryRequest, QueryResult>
{
    private readonly QueryService _queryService;

    public QueryEndpoint(QueryService queryService)
    {
        _queryService = queryService;
    }

    public override void Configure()
    {
        Post("/api/query");
        Roles("Admin", "Doctor");
    }

    public override async Task HandleAsync(QueryRequest req, CancellationToken ct)
    {
        var result = await _queryService.ExecuteAsync(req, ct);
        await HttpContext.Response.SendAsync(result, cancellation: ct);
    }
}
