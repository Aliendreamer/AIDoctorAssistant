using System.Net;
using System.Text;
using MedAssist.AI.Ingestion;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedAssist.Tests;

public sealed class MinerUClientTests : IDisposable
{
    private readonly string _pdfPath;

    public MinerUClientTests()
    {
        _pdfPath = Path.Combine(Path.GetTempPath(), "mineru-test-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllText(_pdfPath, "%PDF-1.4 test");
    }

    public void Dispose()
    {
        if (File.Exists(_pdfPath))
        {
            File.Delete(_pdfPath);
        }
    }

    private static MinerUClient MakeClient(FakeHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://mineru:8000") },
            backend: "pipeline",
            parseMethod: "ocr",
            NullLogger<MinerUClient>.Instance);

    [Fact]
    public async Task ConvertToMarkdown_PostsToFileParse_WithMarkdownRequested()
    {
        var handler = new FakeHandler(Ok("# Heading\n\nbody"));
        var sut = MakeClient(handler);

        await sut.ConvertToMarkdownAsync(_pdfPath);

        Assert.EndsWith("/file_parse", handler.LastUri!.AbsolutePath);
        Assert.Contains("return_md", handler.LastBody);
        Assert.Contains("pipeline", handler.LastBody);   // backend
        Assert.Contains("ocr", handler.LastBody);        // parse_method
    }

    [Fact]
    public async Task ConvertToMarkdown_ReturnsMarkdownFromResultsFirstKey()
    {
        var handler = new FakeHandler(Ok("# Pediatrics\n\nchapter one"));
        var sut = MakeClient(handler);

        var md = await sut.ConvertToMarkdownAsync(_pdfPath);

        Assert.Equal("# Pediatrics\n\nchapter one", md);
    }

    [Fact]
    public async Task ConvertToMarkdown_ThrowsOnNonSuccessStatus()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("boom")
        });
        var sut = MakeClient(handler);

        await Assert.ThrowsAnyAsync<HttpRequestException>(() => sut.ConvertToMarkdownAsync(_pdfPath));
    }

    [Fact]
    public async Task ConvertToMarkdown_ThrowsWhenMarkdownMissing()
    {
        // 200 OK but the first result has no "md" field.
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK,
            """{ "results": { "test.pdf": { "content_list": "[]" } } }"""));
        var sut = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ConvertToMarkdownAsync(_pdfPath));
        Assert.Contains(Path.GetFileName(_pdfPath), ex.Message);
    }

    // MinerU's /file_parse response envelope: results is keyed by the uploaded file; each entry
    // carries the requested output ("md" here).
    private static Func<HttpRequestMessage, HttpResponseMessage> Ok(string markdown) =>
        _ => Json(HttpStatusCode.OK,
            $$"""{ "results": { "test.pdf": { "md": {{System.Text.Json.JsonSerializer.Serialize(markdown)}} } } }""");

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return respond(request);
        }
    }
}
