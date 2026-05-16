using MedAssist.Shared.Models;

namespace MedAssist.Shared.Interfaces;

public interface IBM25VocabStore
{
    Task<BM25VocabSnapshot> LoadAsync(CancellationToken cancellationToken = default);
}
