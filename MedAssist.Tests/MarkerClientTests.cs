using System.Net;
using System.Text.Json;
using MedAssist.AI.Ingestion;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedAssist.Tests;

public sealed class MarkerClientTests
{
    private static MarkerClient MakeClient(HttpMessageHandler handler, bool useLlm = false) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://marker:5002") },
            useLlm,
            NullLogger<MarkerClient>.Instance,
            pollInterval: TimeSpan.Zero);

    [Fact]
    public async Task StartConversionAsync_ReturnsJobId()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.Accepted, new { job_id = "abc-123" }));
        var sut = MakeClient(handler);

        var jobId = await sut.StartConversionAsync("/books/raw/test.pdf");

        Assert.Equal("abc-123", jobId);
    }

    [Fact]
    public async Task PollStatusAsync_ReturnsSavePath_WhenStateDone()
    {
        var callCount = 0;
        var handler = new FakeHandler(_ =>
        {
            callCount++;
            return callCount < 2
                ? Json(HttpStatusCode.OK, new { state = "running", elapsed_seconds = 30 })
                : Json(HttpStatusCode.OK, new { state = "done", save_path = "/books/raw/test.md", elapsed_seconds = 60 });
        });
        var sut = MakeClient(handler);

        var savePath = await sut.PollStatusAsync("abc-123");

        Assert.Equal("/books/raw/test.md", savePath);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task PollStatusAsync_ThrowsOnFailedState()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, new { state = "failed", error = "OOM" }));
        var sut = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.PollStatusAsync("abc-123"));

        Assert.Contains("OOM", ex.Message);
    }

    [Fact]
    public async Task PollStatusAsync_RetriesOnTransientHttpError()
    {
        var callCount = 0;
        var handler = new FakeHandler(_ =>
        {
            callCount++;
            if (callCount == 1)
            {
                throw new HttpRequestException("connection reset");
            }

            return Json(HttpStatusCode.OK, new { state = "done", save_path = "/books/raw/test.md", elapsed_seconds = 30 });
        });
        var sut = MakeClient(handler);

        var savePath = await sut.PollStatusAsync("abc-123");

        Assert.Equal("/books/raw/test.md", savePath);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task PollStatusAsync_StopsOnCancellation()
    {
        var handler = new FakeHandler(_ => Json(HttpStatusCode.OK, new { state = "running", elapsed_seconds = 10 }));
        var sut = MakeClient(handler);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.PollStatusAsync("abc-123", cts.Token));
    }

    [Fact]
    public async Task StartConversionAsync_AppendsUseLlmQueryParam_WhenEnabled()
    {
        Uri? capturedUri = null;
        var handler = new FakeHandler(req =>
        {
            capturedUri = req.RequestUri;
            return Json(HttpStatusCode.Accepted, new { job_id = "xyz" });
        });
        var sut = MakeClient(handler, useLlm: true);

        await sut.StartConversionAsync("/books/raw/test.pdf");

        Assert.NotNull(capturedUri);
        Assert.Contains("use_llm=true", capturedUri!.Query);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body) =>
        new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8,
                "application/json")
        };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
