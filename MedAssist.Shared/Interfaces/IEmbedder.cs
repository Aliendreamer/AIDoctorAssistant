namespace MedAssist.Shared.Interfaces;

public interface IEmbedder
{
    Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default);
    Task<float[]> EmbedPassageAsync(string text, CancellationToken cancellationToken = default);
}
