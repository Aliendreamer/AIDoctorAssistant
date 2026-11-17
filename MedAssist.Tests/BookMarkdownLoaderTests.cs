using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MedAssist.AI.Ingestion;
using MedAssist.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedAssist.Tests;

/// <summary>Where an index job gets its markdown from (change ingest-docx-and-page-citations).</summary>
public sealed class BookMarkdownLoaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "md-loader-" + Guid.NewGuid().ToString("N"));
    private readonly DocxBookConverter _docx = new(NullLogger<DocxBookConverter>.Instance);

    public BookMarkdownLoaderTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static MinerUClient NoMinerU() => throw new InvalidOperationException("MinerU must not be called.");

    [Fact]
    public async Task WordBook_RegeneratesMarkdownOverStaleCache_WithoutMinerU()
    {
        var partsDir = Directory.CreateDirectory(Path.Combine(_root, "nelson-20-bg")).FullName;
        WriteDocx(Path.Combine(partsDir, "1-Nelson_XVIII_BG_PDF10-14.docx"), "Абзац от глава 305.");
        var markdownPath = Path.Combine(_root, "nelson-20-bg.md");
        await File.WriteAllTextAsync(markdownPath, "stale markdown from an earlier upload");

        var markdown = await BookMarkdownLoader.LoadAsync(partsDir, markdownPath, NoMinerU, _docx, NullLogger.Instance);

        Assert.Equal("<!-- page pdf=10 -->\n\nАбзац от глава 305.", markdown);
        Assert.Equal(markdown, await File.ReadAllTextAsync(markdownPath));
    }

    [Fact]
    public async Task PdfBook_UsesCachedMarkdown()
    {
        var pdfPath = Path.Combine(_root, "book.pdf");
        await File.WriteAllTextAsync(pdfPath, "%PDF-1.4");
        var markdownPath = Path.Combine(_root, "book.md");
        await File.WriteAllTextAsync(markdownPath, "# cached");

        var markdown = await BookMarkdownLoader.LoadAsync(pdfPath, markdownPath, NoMinerU, _docx, NullLogger.Instance);

        Assert.Equal("# cached", markdown);
    }

    private static void WriteDocx(string path, string text)
    {
        using var doc = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        doc.AddMainDocumentPart().Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
    }
}
