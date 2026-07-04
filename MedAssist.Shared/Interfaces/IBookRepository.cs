using MedAssist.Shared.Models;

namespace MedAssist.Shared.Interfaces;

/// <summary>
/// Book metadata/status persistence used by the ingestion indexer. Abstracted in Shared so the AI
/// layer can upsert book status and outline without referencing EF (audit P2-13).
/// </summary>
public interface IBookRepository
{
    Task UpsertAsync(BookInfo book, CancellationToken cancellationToken = default);
    Task UpdateOutlineAsync(string bookId, string outline, CancellationToken cancellationToken = default);
}
