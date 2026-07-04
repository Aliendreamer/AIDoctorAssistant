using MedAssist.Shared.Validation;

namespace MedAssist.Tests;

// Guards P0-2: in Production the JWT signing key must not be the built-in placeholder or too short.
// The check is a no-op in non-Production so local dev is unaffected.
public sealed class JwtKeyPolicyTests
{
    [Fact]
    public void Placeholder_IsRejectedInProduction()
        => Assert.NotNull(JwtKeyPolicy.Validate(JwtKeyPolicy.PlaceholderKey, isProduction: true));

    [Fact]
    public void Placeholder_IsAllowedOutsideProduction()
        => Assert.Null(JwtKeyPolicy.Validate(JwtKeyPolicy.PlaceholderKey, isProduction: false));

    [Fact]
    public void ShortKey_IsRejectedInProduction()
        => Assert.NotNull(JwtKeyPolicy.Validate("too-short", isProduction: true));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingKey_IsRejectedEverywhere(string? key)
    {
        Assert.NotNull(JwtKeyPolicy.Validate(key, isProduction: true));
        Assert.NotNull(JwtKeyPolicy.Validate(key, isProduction: false));
    }

    [Fact]
    public void StrongKey_IsAcceptedInProduction()
        => Assert.Null(JwtKeyPolicy.Validate(new string('k', 48), isProduction: true));
}
