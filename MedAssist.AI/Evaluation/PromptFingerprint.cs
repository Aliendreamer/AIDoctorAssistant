using System.Security.Cryptography;
using System.Text;
using MedAssist.AI.Plugins;

namespace MedAssist.AI.Evaluation;

/// <summary>
/// A short hash of the composed prompts. Prompts live in code, so this is what shows a prompt edit
/// between two runs that otherwise have identical settings.
/// </summary>
public static class PromptFingerprint
{
    public static string Compute()
    {
        var text = new StringBuilder();
        foreach (var profile in new[] { PromptProfiles.Default, PromptProfiles.GlobalSearch, PromptProfiles.DifferentialDiagnosis })
        {
            foreach (var query in new[] { "question", "въпрос" })
            {
                foreach (var message in AnswerPromptBuilder.BuildBookAnswer(profile, query, [], null))
                {
                    text.AppendLine(message.Content);
                }
            }
        }

        foreach (var message in QueryPreparation.BuildPrompt("question", null))
        {
            text.AppendLine(message.Content);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..12];
    }
}
