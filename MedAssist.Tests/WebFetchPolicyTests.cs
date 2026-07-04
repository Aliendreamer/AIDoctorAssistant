using System.Net;
using MedAssist.Shared.Validation;

namespace MedAssist.Tests;

// Guards P1-4 (SSRF): before fetching a web-search result the app must enforce https + a host
// allowlist on the URL actually dereferenced, and refuse addresses that resolve to internal /
// loopback / link-local / metadata ranges.
public sealed class WebFetchPolicyTests
{
    private static readonly string[] Allowed = ["who.int", "cdc.gov", "ncbi.nlm.nih.gov/books", "nhs.uk"];

    [Theory]
    [InlineData("https://www.who.int/news/item")]
    [InlineData("https://cdc.gov/page")]
    [InlineData("https://ncbi.nlm.nih.gov/books/NBK1")]
    [InlineData("https://nhs.uk/conditions")]
    public void IsAllowedUrl_AllowsHttpsAllowlistedHosts(string url)
        => Assert.True(WebFetchPolicy.IsAllowedUrl(url, Allowed, out _));

    [Theory]
    [InlineData("http://www.who.int/x")]         // not https
    [InlineData("https://evil.com/x")]           // not on allowlist
    [InlineData("https://who.int.evil.com/x")]   // suffix-spoof attempt
    [InlineData("ftp://who.int/x")]              // wrong scheme
    [InlineData("http://169.254.169.254/latest")]// metadata via http
    [InlineData("not a url")]
    public void IsAllowedUrl_RejectsDisallowed(string url)
        => Assert.False(WebFetchPolicy.IsAllowedUrl(url, Allowed, out _));

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.5.4")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    public void IsBlockedAddress_BlocksInternalAndMetadata(string ip)
        => Assert.True(WebFetchPolicy.IsBlockedAddress(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("https://who.int/x", true)]
    [InlineData("http://example.com", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,x", false)]
    [InlineData("not a url", false)]
    [InlineData(null, false)]
    public void IsHttpUrl_OnlyAllowsHttpSchemes(string? url, bool expected)
        => Assert.Equal(expected, WebFetchPolicy.IsHttpUrl(url));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.15.0.1")]   // just below the private 172.16/12 range
    [InlineData("172.32.0.1")]   // just above it
    [InlineData("2606:4700:4700::1111")]
    public void IsBlockedAddress_AllowsPublic(string ip)
        => Assert.False(WebFetchPolicy.IsBlockedAddress(IPAddress.Parse(ip)));
}
