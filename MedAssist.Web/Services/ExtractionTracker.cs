using System.Collections.Concurrent;

namespace MedAssist.Web.Services;

public enum ExtractionState { Running, Done, Failed }

public sealed record ExtractionEntry(
    ExtractionState State,
    string BookId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error);

public sealed class ExtractionTracker
{
    private readonly ConcurrentDictionary<int, ExtractionEntry> _entries = new();

    public bool TryStart(int bookId, string bookSlug, out ExtractionEntry existing)
    {
        var entry = new ExtractionEntry(ExtractionState.Running, bookSlug, DateTimeOffset.UtcNow, null, null);
        if (_entries.TryAdd(bookId, entry))
        {
            existing = entry;
            return true;
        }

        existing = _entries[bookId];
        return false;
    }

    public void MarkDone(int bookId) =>
        _entries.AddOrUpdate(bookId,
            id => throw new InvalidOperationException($"Book {id} was not started."),
            (_, prev) => prev with { State = ExtractionState.Done, CompletedAt = DateTimeOffset.UtcNow });

    public void MarkFailed(int bookId, string error) =>
        _entries.AddOrUpdate(bookId,
            id => throw new InvalidOperationException($"Book {id} was not started."),
            (_, prev) => prev with { State = ExtractionState.Failed, CompletedAt = DateTimeOffset.UtcNow, Error = error });

    public void Reset(int bookId) => _entries.TryRemove(bookId, out _);

    public bool IsRunning(int bookId) =>
        _entries.TryGetValue(bookId, out var e) && e.State == ExtractionState.Running;

    public IReadOnlyDictionary<int, ExtractionEntry> GetAll() => _entries;

    public ExtractionEntry? Get(int bookId) =>
        _entries.TryGetValue(bookId, out var e) ? e : null;
}
