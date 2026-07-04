using System.Text;

namespace MedAssist.Shared.Validation;

/// <summary>
/// Startup policy for the JWT signing key (audit P0-2). The signing key is symmetric — anyone who
/// knows it can forge tokens — so Production must not run with the self-describing placeholder or a
/// too-short key. The check is deliberately a no-op outside Production so local dev is unaffected.
/// </summary>
public static class JwtKeyPolicy
{
    /// <summary>The built-in placeholder that ships in config; must never be the effective Production key.</summary>
    public const string PlaceholderKey = "medassist-change-me-in-production-must-be-32-chars!!";

    public const int MinKeyBytes = 32;

    /// <summary>Returns null when the key is acceptable for the environment, else the rejection reason.</summary>
    public static string? Validate(string? key, bool isProduction)
    {
        if (string.IsNullOrEmpty(key))
        {
            return "Auth:Jwt:SecretKey is not configured.";
        }

        if (!isProduction)
        {
            return null;
        }

        if (key == PlaceholderKey)
        {
            return "Auth:Jwt:SecretKey is still the built-in placeholder. Set a real secret before running in Production.";
        }

        if (Encoding.UTF8.GetByteCount(key) < MinKeyBytes)
        {
            return $"Auth:Jwt:SecretKey must be at least {MinKeyBytes} bytes in Production.";
        }

        return null;
    }
}
