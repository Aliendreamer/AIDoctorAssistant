namespace MedAssist.Web.Options;

public sealed class ModelsOptions
{
    public string Path { get; init; } = "models";
    public string RerankerPath { get; init; } = "models/ms-marco-MiniLM-L-6-v2";
    public int InitializerTimeoutMinutes { get; init; } = 15;
}
