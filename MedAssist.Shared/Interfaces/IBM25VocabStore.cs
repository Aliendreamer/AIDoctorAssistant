using MedAssist.Shared.Models;

namespace MedAssist.Shared.Interfaces;

public interface IBM25VocabStore
{
    Task<BM25VocabSnapshot> LoadAsync(CancellationToken cancellationToken = default);
    Task<int> GetTotalDocumentsAsync(CancellationToken cancellationToken = default);
    Task UpsertTermsAsync(IReadOnlyDictionary<string, int> termDfs, int totalDocs, CancellationToken cancellationToken = default);
}
