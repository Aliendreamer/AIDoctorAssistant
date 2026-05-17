namespace MedAssist.Web.Options;

public sealed class DoclingOptions
{
    public string Endpoint { get; init; } = "http://docling:5001";
    public int TimeoutMinutes { get; init; } = 30;
}
