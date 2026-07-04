using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Data.Repositories;

public sealed class ChatHistoryRepository(MedAssistDbContext db)
{
    public async Task<IReadOnlyList<ChatMessageEntity>> GetRecentAsync(
        string userId, string queryType, int limit = 10, CancellationToken ct = default)
    {
        // Tie-break equal timestamps by the monotonic identity Id so ordering is deterministic
        // (a user + assistant pair written together share CreatedAt) — audit P3-8. Id is the
        // insertion sequence, so this needs no separate sequence column.
        return await db.ChatMessages
            .Where(m => m.UserId == userId && m.QueryType == queryType)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(limit)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .ToListAsync(ct);
    }

    public async Task AddMessageAsync(ChatMessageEntity message, CancellationToken ct = default)
    {
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Persists several messages in a single round-trip (audit P2-19).</summary>
    public async Task AddMessagesAsync(IEnumerable<ChatMessageEntity> messages, CancellationToken ct = default)
    {
        db.ChatMessages.AddRange(messages);
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearAsync(string userId, string queryType, CancellationToken ct = default)
    {
        await db.ChatMessages
            .Where(m => m.UserId == userId && m.QueryType == queryType)
            .ExecuteDeleteAsync(ct);
    }
}
