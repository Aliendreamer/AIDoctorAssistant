using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedAssist.Shared.Models.Evaluation;

/// <summary>JSON format of the snapshot, summary and result columns.</summary>
public static class EvalJson
{
    private static readonly JsonSerializerOptions _options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, _options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, _options)
        ?? throw new InvalidDataException($"Stored evaluation JSON is not a {typeof(T).Name}.");
}
