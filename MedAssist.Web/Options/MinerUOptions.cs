namespace MedAssist.Web.Options;

/// <summary>
/// Configuration for the shared MinerU PDF→Markdown service (audit change: migrate-marker-to-mineru).
/// The app POSTs each PDF to <see cref="ServiceUrl"/> <c>/file_parse</c> and reads the markdown from
/// the response, replacing the removed self-hosted Marker container.
/// </summary>
public sealed class MinerUOptions
{
    /// <summary>Base URL of the shared MinerU HTTP service (reachable as <c>mineru:8000</c> on the PCC network).</summary>
    public string ServiceUrl { get; init; } = "http://mineru:8000";

    /// <summary>MinerU parsing backend (e.g. <c>pipeline</c>).</summary>
    public string Backend { get; init; } = "pipeline";

    /// <summary>Parse method passed to MinerU (e.g. <c>ocr</c>).</summary>
    public string Method { get; init; } = "ocr";

    /// <summary>HTTP timeout, in minutes, for a single conversion (the whole OCR runs in one request).</summary>
    public int ConversionTimeoutMinutes { get; init; } = 120;
}
