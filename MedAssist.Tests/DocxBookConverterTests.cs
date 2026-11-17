using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MedAssist.AI.Ingestion;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedAssist.Tests;

/// <summary>Word-part conversion for the Nelson translation (change ingest-docx-and-page-citations).</summary>
public sealed class DocxBookConverterTests
{
    // ---------- page lines: one case per variant found in the 112 real parts ----------

    [Theory]
    [InlineData("PDF стр. 426 · печатна стр. 268 · начало на раздел VI, глава 44", 426, "268")]
    [InlineData("Оригинал: PDF 2635 · отпечатана стр. 1804", 2635, "1804")]
    [InlineData("ОРИГИНАЛ: PDF 2641 · отпечатана стр. 1807.e2. Библиографските записи се запазват на английски.", 2641, "1807.e2")]
    [InlineData("PDF страница 2557 · Отпечатана страница 1770", 2557, "1770")]
    [InlineData("PDF страница 2600 · 1780.e1", 2600, "1780.e1")]
    [InlineData("PDF стр. 480 · печатна стр. 316.e1 · самостоятелна библиография към глава 47", 480, "316.e1")]
    [InlineData("Оригинал: PDF стр. 2671, отпечатана стр. 1821. Таблица 336-2.", 2671, "1821")]
    [InlineData("PDF 2631 и 2633 · отпечатани стр. 1803–1804 · текстът е подреден последователно", 2631, "1803")]
    [InlineData("PDF стр. 2776 · оригинална печатна страница 1899", 2776, "1899")]
    [InlineData("PDF страница 2816 · продължение на глава 353, отпечатана стр. 1917; няма видим номер", 2816, "1917")]
    [InlineData("Оригинал: PDF 2691 · печатна 1836", 2691, "1836")]
    [InlineData("PDF стр. 486 · без видим собствен печатен номер (след 320) · глава 48", 486, null)]
    [InlineData("PDF страница 2557 · Отпечатана страница няма отпечатан номер", 2557, null)]
    [InlineData("ОРИГИНАЛ: PDF 2719 · отпечатана стр. продължение на 1853; без отпечатан номер", 2719, null)]
    [InlineData("PDF стр. 2760 · Продължение и край на глава 342", 2760, null)]
    [InlineData("Nelson Textbook of Pediatrics, 20-о изд. · PDF 2738 · печатна стр. 1871 · редактируема реконструкция на блок-схема", 2738, "1871")]
    public void PageLine_IsRecognised(string line, int pdf, string? printed)
    {
        Assert.True(DocxConventions.TryParsePageLine(line, out var page));
        Assert.Equal(new PageRef(pdf, printed), page);
    }

    [Theory]
    [InlineData("Таблица 44-3 · Диетични референтни стойности: макронутриенти (начало; PDF 427)")]
    [InlineData("Част 1 — PDF страници 426–430")]
    [InlineData("Дивертикулите на Meckel се срещат при приблизително 2% от общата популация.")]
    public void NonPageLine_IsNotRecognised(string line)
    {
        Assert.False(DocxConventions.TryParsePageLine(line, out _));
    }

    [Theory]
    [InlineData("ЧАСТ XVIII — ХРАНОСМИЛАТЕЛНА СИСТЕМА")]
    [InlineData("Част 1 — PDF страници 426–430")]
    [InlineData("Част 40 · PDF страници 2731–2735 · Отпечатани стр. 1864–1868")]
    [InlineData("Раздел XVIII · Храносмилателна система Част 49 — PDF страници 2776–2780")]
    [InlineData("Раздел VI · Хранене")]
    [InlineData("Последователен пълен медицински превод от Nelson Textbook of Pediatrics, 20-о издание · PDF страници 2636–2640")]
    [InlineData("Nelson Textbook of Pediatrics, 20-о издание · Част XVIII")]
    [InlineData("Глава 44 · Хранителни потребности; таблици 44-1, 44-2, 44-3")]
    [InlineData("Глава 55 · Библиография  |  Глава 56 · Поддържаща и заместителна инфузионна терапия")]
    public void Banner_IsRecognised_AtStartOfPart(string line)
    {
        Assert.True(DocxConventions.IsBanner(line, atStartOfPart: true));
    }

    [Theory]
    [InlineData("Част XVIII — Храносмилателна система Раздел 1 — Клинични прояви на заболяванията", true)]
    [InlineData("Част XVIII — Храносмилателна система. Превод на PDF стр. 2541–2545 (отпечатани стр. 1758–1762)", true)]
    [InlineData("Nelson Textbook of Pediatrics, 20-о издание · Раздел XVIII „Храносмилателна система“", true)]
    [InlineData("Последователен медицински превод от Nelson Textbook of Pediatrics", true)]
    [InlineData("Раздел VI · Хранене", false)]
    [InlineData("Част 1 — PDF страници 426–430", false)]
    [InlineData("Глава 44 · Хранителни потребности; таблици 44-1", false)]
    [InlineData("Източник: Nelson Textbook of Pediatrics, 20th edition, Figure 331-1.", false)]
    public void Banner_AfterContent_OnlyStrongBannersAreRecognised(string line, bool expected)
    {
        Assert.Equal(expected, DocxConventions.IsBanner(line, atStartOfPart: false));
    }

    [Theory]
    [InlineData("Глава 340. Остър гастроентерит при деца")]
    [InlineData("Глава 331 — Чревни дупликации, дивертикул на Meckel")]
    [InlineData("ДЕХИДРАТАЦИЯ")]
    public void ChapterTitle_IsNotBanner(string line)
    {
        Assert.False(DocxConventions.IsBanner(line, atStartOfPart: true));
    }

    [Theory]
    [InlineData("Контролна граница: Обработени са PDF 431–435 (печатни 273–277).", true)]
    [InlineData("Контролна граница на превода: Последното изречение.", true)]
    [InlineData("Бележка за продължението: Последното английско изречение.", true)]
    [InlineData("Бележка за продължение: следващото изречение.", true)]
    [InlineData("Бележка за границата: На PDF 2645 следва изречение.", true)]
    [InlineData("Бележка за печатния оригинал: скобите и знаците.", true)]
    [InlineData("Забележка за верността на оригинала: В PDF стр. 2735.", true)]
    [InlineData("[Библиографията е пропусната съгласно указанията.]", true)]
    [InlineData("Забележка: Стойностите, отбелязани със звездичка, са AI.", false)]
    [InlineData("Източник: Black RE, Victora CG, Walker SP, et al.", false)]
    [InlineData("Адаптирано от Food and Nutrition Board, Institute of Medicine.", false)]
    [InlineData("Превод на означенията във фигура 340-1: 0–11 месеца.", false)]
    public void MetaNote_IsRecognised(string line, bool expected)
    {
        Assert.Equal(expected, DocxConventions.IsMetaNote(line));
    }

    // ---------- ordering ----------

    [Fact]
    public void Parts_AreOrderedByPartThenPrefix()
    {
        string[] names =
        [
            "42-Nelson_XVIII_BG_PDF2746-2750.docx",
            "2-Nelson_VI_BG_PDF431-435.docx",
            "41.2-Nelson_XVIII_fig340-6_340-7_коригирана_таблица.docx",
            "1-Nelson_XVIII_305_BG.docx",
            "22-Nelson_VII_BG_PDF530-534.docx",
            "41-Nelson_XVIII_BG_PDF2736-2740.docx",
            "10-Nelson_VI_BG_PDF471-475.docx",
            "other_PDF10-12.docx",
        ];

        var ordered = DocxConventions.OrderParts(names);

        Assert.Equal(
        [
            "2-Nelson_VI_BG_PDF431-435.docx",
            "10-Nelson_VI_BG_PDF471-475.docx",
            "22-Nelson_VII_BG_PDF530-534.docx",
            "1-Nelson_XVIII_305_BG.docx",
            "41-Nelson_XVIII_BG_PDF2736-2740.docx",
            "41.2-Nelson_XVIII_fig340-6_340-7_коригирана_таблица.docx",
            "42-Nelson_XVIII_BG_PDF2746-2750.docx",
            "other_PDF10-12.docx",
        ], ordered);
    }

    // ---------- conversion of synthetic parts ----------

    private static DocxPart Part(string name, params OpenXmlElement[] body) => new(name, () => BuildDocx(body));

    [Fact]
    public void Convert_MapsLocaleHeadingStyles_AndDropsBannersAndNotes()
    {
        var part = Part("21-Nelson_XVIII_BG_PDF2636-2640.docx",
            P("Title", "ЧАСТ XVIII — ХРАНОСМИЛАТЕЛНА СИСТЕМА"),
            P(null, "Последователен пълен медицински превод от Nelson Textbook of Pediatrics, 20-о издание · PDF страници 2636–2640"),
            P("1", "Глава 331 — Чревни дупликации"),
            P("21", "331.2 Дивертикул на Meckel"),
            P("PDFmarker", "PDF 2635–2636 · отпечатани стр. 1804–1805 · изречението е оставено изцяло"),
            P(null, "Дивертикулите на Meckel се срещат при 2% от популацията."),
            P(null, "Контролна граница: Обработени са PDF 2636–2640."),
            P("31", "КЛИНИЧНИ ПРОЯВИ"),
            P(null, "Източник: Black RE."));

        var result = Converter().Convert([part]);

        var expected = string.Join("\n\n",
            "<!-- page pdf=2636 -->",
            "# Глава 331 — Чревни дупликации",
            "## 331.2 Дивертикул на Meckel",
            // The part covers PDF 2636–2640 but notes only 2635, so the text after the note runs on to 2640.
            "<!-- page pdf=2635 printed=1804 until=2640 -->",
            "Дивертикулите на Meckel се срещат при 2% от популацията.",
            "### КЛИНИЧНИ ПРОЯВИ",
            "Източник: Black RE.");
        Assert.Equal(expected, result.Markdown);
        Assert.Empty(result.UnrecognisedPageLines);
    }

    [Fact]
    public void Convert_RendersTables_AndSkipsEmptyParagraphs()
    {
        var part = Part("5-Nelson_VI_BG_PDF446-450.docx",
            P("Figurecaption", "Таблица 44-1 · Енергия"),
            Table(["Възраст", "kcal"], ["0–6 mo", "550 | 600"]),
            P(null, "   "));

        var result = Converter().Convert([part]);

        var expected = string.Join("\n\n",
            "<!-- page pdf=446 -->",
            "Таблица 44-1 · Енергия",
            "| Възраст | kcal |\n| --- | --- |\n| 0–6 mo | 550 \\| 600 |");
        Assert.Equal(expected, result.Markdown);
    }

    [Fact]
    public void Convert_UnrangedPart_InheritsPreviousPage()
    {
        var first = Part("40-Nelson_XVIII_BG_PDF2731-2735.docx",
            P(null, "Оригинал: PDF 2735 · отпечатана стр. 1868"),
            P(null, "Текст на глава 340."));
        var unranged = Part("41.2-Nelson_XVIII_fig340-6_коригирана_таблица.docx",
            P(null, "Коригирана таблица."));

        var result = Converter().Convert([unranged, first]);

        var expected = string.Join("\n\n",
            "<!-- page pdf=2731 -->",
            "<!-- page pdf=2735 printed=1868 -->",
            "Текст на глава 340.",
            "Коригирана таблица.");
        Assert.Equal(expected, result.Markdown);
    }

    [Fact]
    public void Convert_MarksPagesTheTranslatorSkipped_AsUncertain()
    {
        // Real case: 21-Nelson_XVIII_BG_PDF2636-2640.docx notes PDF 2635 and 2637 only.
        var part = Part("21-Nelson_XVIII_BG_PDF2636-2640.docx",
            P(null, "PDF 2635–2636 · отпечатани стр. 1804–1805"),
            P(null, "Дивертикул на Meckel."),
            P(null, "Оригинал: PDF 2637 · отпечатана стр. 1805.e1"),
            P(null, "Глава 332."));
        var next = Part("22-Nelson_XVIII_BG_PDF2641-2645.docx",
            P(null, "Оригинал: PDF 2641 · отпечатана стр. 1809"),
            P(null, "Глава 333."));

        var markers = Converter().Convert([part, next]).Markdown.Split("\n\n")
            .Where(b => b.StartsWith("<!--", StringComparison.Ordinal)).ToList();

        Assert.Equal(
        [
            "<!-- page pdf=2636 -->",
            "<!-- page pdf=2635 printed=1804 until=2636 -->",
            "<!-- page pdf=2637 printed=1805.e1 until=2640 -->",
            "<!-- page pdf=2641 -->",
            "<!-- page pdf=2641 printed=1809 until=2645 -->",
        ], markers);
    }

    [Fact]
    public void Convert_ReportsUnrecognisedPageLikeLines()
    {
        var part = Part("1-Nelson_XVIII_BG_PDF10-14.docx", P(null, "PDF: две страници са разменени"));

        var result = Converter().Convert([part]);

        Assert.Single(result.UnrecognisedPageLines);
    }

    [Fact]
    public void Convert_Output_ChunksWithPrintedPages()
    {
        // A one-page part whose page is noted: no skipped pages, so the printed page is trusted.
        var part = Part("21-Nelson_XVIII_BG_PDF2636-2636.docx",
            P("1", "Глава 331"),
            P(null, "Оригинал: PDF 2636 · отпечатана стр. 1805"),
            P(null, string.Join(' ', Enumerable.Repeat("Дивертикулът е вроден остатък.", 10))));

        var chunk = Assert.Single(new MarkdownChunker().Chunk(Converter().Convert([part]).Markdown));

        Assert.Equal("1805", chunk.Pages.PrintedStart);
        Assert.Equal(2636, chunk.Pages.PdfStart);
    }

    private static DocxBookConverter Converter() => new(NullLogger<DocxBookConverter>.Instance);

    // ---------- docx builders ----------

    private static Paragraph P(string? styleId, string text)
    {
        var paragraph = new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        if (styleId is not null)
        {
            paragraph.PrependChild(new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
        }

        return paragraph;
    }

    private static Table Table(params string[][] rows) =>
        new(rows.Select(cells => new TableRow(cells.Select(c => new TableCell(P(null, c))))));

    private static MemoryStream BuildDocx(OpenXmlElement[] body)
    {
        var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(body.Select(e => e.CloneNode(true))));
            var styles = main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles = new Styles(
                Style("Title", "Title"),
                Style("1", "heading 1"),
                Style("21", "heading 2"),
                Style("31", "heading 3"),
                Style("PDFmarker", "PDF marker"),
                Style("Figurecaption", "Figure caption"));
        }

        stream.Position = 0;
        return stream;
    }

    private static Style Style(string id, string name) =>
        new(new StyleName { Val = name }) { Type = StyleValues.Paragraph, StyleId = id };
}
