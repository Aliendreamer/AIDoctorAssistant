using FastEndpoints;
using MedAssist.Data.Repositories;

namespace MedAssist.Web.Endpoints.Chat;

public sealed class ClearChatHistoryEndpoint(ChatHistoryRepository chatHistory) : EndpointWithoutRequest
{
    private static readonly HashSet<string> _validTypes = ["disease", "symptoms", "treatment"];

    public override void Configure()
    {
        Delete("/api/chat/history/{queryType}");
        Roles("Admin", "Doctor");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var queryType = Route<string>("queryType")?.ToLowerInvariant();

        if (queryType is null || !_validTypes.Contains(queryType))
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var userId = User.Identity?.Name;
        if (userId is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await chatHistory.ClearAsync(userId, queryType, ct);
        await Send.OkAsync(ct);
    }
}
