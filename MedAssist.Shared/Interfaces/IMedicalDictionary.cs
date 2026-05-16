using MedAssist.Shared.Models;

namespace MedAssist.Shared.Interfaces;

public interface IMedicalDictionary
{
    Task<IReadOnlyList<string>> ExpandQueryAsync(string query, CancellationToken cancellationToken = default);
    Task<IllnessEntry?> GetByIcdAsync(string icdCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IllnessEntry>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
