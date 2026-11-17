namespace MedAssist.Shared.Constants;

/// <summary>
/// Bounds for a zip of Word parts uploaded as a book (change ingest-docx-and-page-citations). The Nelson zips hold
/// 33 and 79 parts of under 1 MB each, so these leave ample headroom while stopping a zip bomb. Each can be
/// overridden with the matching <c>Books:Docx…</c> setting.
/// </summary>
public static class DocxUploadLimits
{
    public const int MaxEntries = 500;
    public const long MaxEntryBytes = 50L * 1024 * 1024;
    public const long MaxTotalBytes = 500L * 1024 * 1024;

    public const string DefaultDocxPath = "/books/docx";
}
