using MedAssist.Shared.Validation;

namespace MedAssist.Tests;

// Guards P1-1: BookId is used to build filesystem paths (uploaded PDF, cached markdown, Marker arg).
// It must be a strict allowlist so a value like "../../etc/passwd" can never escape the books dir.
public sealed class BookIdRulesTests
{
    [Theory]
    [InlineData("harrison-21")]
    [InlineData("a")]
    [InlineData("0")]
    [InlineData("book1")]
    [InlineData("gray-anatomy-42")]
    public void IsValid_AcceptsAllowlistedIds(string id) => Assert.True(BookIdRules.IsValid(id));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("../../etc/passwd")]
    [InlineData("../secret")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("Book")]      // uppercase disallowed
    [InlineData("-lead")]     // must start alphanumeric
    [InlineData("a.b")]       // dots disallowed (blocks "..")
    [InlineData("a b")]       // whitespace disallowed
    [InlineData("book$")]     // symbols disallowed
    public void IsValid_RejectsInvalidIds(string? id) => Assert.False(BookIdRules.IsValid(id));

    [Fact]
    public void IsValid_AcceptsMaxLength64() => Assert.True(BookIdRules.IsValid(new string('a', 64)));

    [Fact]
    public void IsValid_RejectsOver64() => Assert.False(BookIdRules.IsValid(new string('a', 65)));

    [Fact]
    public void ResolveWithin_ValidId_ReturnsPathInsideBaseDir()
    {
        var path = BookIdRules.ResolveWithin("/books/raw", "harrison-21", ".pdf");

        Assert.Equal(Path.GetFullPath(Path.Combine("/books/raw", "harrison-21.pdf")), path);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("a/b")]
    [InlineData("")]
    public void ResolveWithin_InvalidId_Throws(string id)
        => Assert.Throws<ArgumentException>(() => BookIdRules.ResolveWithin("/books/raw", id, ".pdf"));
}
