namespace MedAssist.Web.Options;

public sealed class OtelOptions
{
    public string ServiceName { get; init; } = "medassist-web";
    public string Endpoint { get; init; } = "http://localhost:4317";
}
