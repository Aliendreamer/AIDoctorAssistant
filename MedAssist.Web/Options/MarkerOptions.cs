namespace MedAssist.Web.Options;

public sealed class MarkerOptions
{
    public string Endpoint { get; init; } = "http://marker:5002";
    public int TimeoutMinutes { get; init; } = 30;
    public bool UseLlm { get; init; } = false;
}
