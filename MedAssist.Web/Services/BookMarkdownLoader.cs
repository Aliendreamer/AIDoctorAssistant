using MedAssist.AI.Ingestion;

namespace MedAssist.Web.Services;

/// <summary>
/// Where an index job gets its markdown from. A Word book (its source is a parts directory) is always regenerated
/// from all its parts without OCR, overwriting the cache, so a newly added Part can never be missed by a stale cache
/// (change ingest-docx-and-page-citations). A PDF book uses the cached markdown if present, otherwise MinerU.
/// </summary>
public static class BookMarkdownLoader
{
    public static async Task<string> LoadAsync(
        string sourcePath,
        string markdownPath,
        Func<MinerUClient> mineru,
        DocxBookConverter docx,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(sourcePath))
        {
            logger.LogInformation("Converting Word parts in {Path} (no OCR)", sourcePath);
            var converted = docx.ConvertDirectory(sourcePath).Markdown;
            await File.WriteAllTextAsync(markdownPath, converted, cancellationToken);
            return converted;
        }

        if (File.Exists(markdownPath))
        {
            logger.LogInformation("Using cached markdown at {Path}", markdownPath);
            return await File.ReadAllTextAsync(markdownPath, cancellationToken);
        }

        logger.LogInformation("Converting {Path} via MinerU", sourcePath);
        var markdown = await mineru().ConvertToMarkdownAsync(sourcePath, cancellationToken);
        await File.WriteAllTextAsync(markdownPath, markdown, cancellationToken);
        return markdown;
    }
}
