namespace MedAssist.Web.Services;

public enum IngestionJobKind
{
    /// <summary>OCR the PDF if needed, then chunk/embed/index the book.</summary>
    Index,

    /// <summary>OCR the PDF to markdown only (no indexing).</summary>
    Extract
}

/// <summary>
/// A unit of long-running ingestion work, queued by an endpoint and executed by
/// <see cref="IngestionWorker"/> off the request thread (audit P1-8). Carries everything the worker
/// needs so it doesn't depend on request-scoped state.
/// </summary>
public sealed record IngestionJob(
    IngestionJobKind Kind,
    int BookId,
    string BookSlug,
    string Title,
    string Author,
    string Language,
    string Edition,
    string FilePath);
