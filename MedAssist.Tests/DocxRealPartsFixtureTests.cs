using System.IO.Compression;
using MedAssist.AI.Ingestion;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace MedAssist.Tests;

/// <summary>
/// Runs the Word converter over the real Nelson zips in <c>books/</c> (change ingest-docx-and-page-citations). The
/// zips are git-crypt encrypted at rest, so on a checkout without them (or still encrypted) the test passes without
/// asserting anything.
/// </summary>
public sealed class DocxRealPartsFixtureTests(ITestOutputHelper output)
{
    private static readonly byte[] _zipSignature = [0x50, 0x4B, 0x03, 0x04];

    [Fact]
    public void RealNelsonZips_ConvertCleanly()
    {
        var zips = FindZips();
        if (zips.Count == 0)
        {
            output.WriteLine("No readable Nelson zips in books/ — skipping.");
            return;
        }

        var parts = zips.SelectMany(ReadParts).ToList();
        var converter = new DocxBookConverter(NullLogger<DocxBookConverter>.Instance);

        var result = converter.Convert(parts);
        var markers = result.Markdown.Split("\n\n").Count(b => PageMarker.TryParse(b, out _));
        var printed = result.Markdown.Split("\n\n").Count(b => PageMarker.TryParse(b, out var p) && p.PrintedPage is not null);
        var chunks = new MarkdownChunker().Chunk(result.Markdown);
        output.WriteLine($"{parts.Count} parts, {result.Markdown.Length} chars, {markers} markers ({printed} with printed page), {chunks.Count} chunks, " +
            $"{chunks.Count(c => c.Pages.PrintedStart is not null)} chunks with printed pages");

        Assert.Empty(result.UnrecognisedPageLines);
        string[] translatorText = ["Nelson Textbook of Pediatrics", "Контролна граница", "Последователен пълен медицински превод"];
        // "Източник: Nelson Textbook of Pediatrics, …" credits a figure or table and is kept on purpose.
        var leftovers = result.Markdown.Split("\n\n")
            .Where(b => !b.StartsWith("Източник", StringComparison.Ordinal))
            .Where(b => translatorText.Any(t => b.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .Select(b => b.Length > 160 ? b[..160] : b)
            .ToList();
        leftovers.ForEach(l => output.WriteLine("LEFTOVER: " + l));
        Assert.Empty(leftovers);
        Assert.All(chunks, c => Assert.DoesNotContain("<!-- page", c.Text));
        // 89% at the time of writing: the rest cite a PDF range where the translator skipped pages, or are on pages
        // with no printed number.
        Assert.True(chunks.Count(c => c.Pages.PrintedStart is not null) > chunks.Count * 0.85,
            "Expected printed pages on more than 85% of chunks.");

        foreach (var part in parts)
        {
            var single = converter.Convert([part]).Markdown;
            Assert.True(PageMarker.Strip(single).Trim().Length > 0, $"Part '{part.FileName}' produced no text.");
        }
    }

    private static List<string> FindZips()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MedAssist.slnx")))
        {
            dir = dir.Parent;
        }

        var books = dir is null ? null : Path.Combine(dir.FullName, "books");
        if (books is null || !Directory.Exists(books))
        {
            return [];
        }

        return Directory.EnumerateFiles(books, "*.zip").Where(IsPlainZip).ToList();
    }

    private static bool IsPlainZip(string path)
    {
        Span<byte> header = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        return stream.Read(header) == 4 && header.SequenceEqual(_zipSignature);
    }

    private static IEnumerable<DocxPart> ReadParts(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries.Where(e => e.Name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)))
        {
            using var source = entry.Open();
            var bytes = new MemoryStream();
            source.CopyTo(bytes);
            var content = bytes.ToArray();
            yield return new DocxPart(entry.Name, () => new MemoryStream(content));
        }
    }
}
