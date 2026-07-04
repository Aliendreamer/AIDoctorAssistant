using System.Net;
using System.Net.Sockets;

namespace MedAssist.Shared.Validation;

/// <summary>
/// SSRF guard for outbound web fetches. The web-search domain allowlist is only a search hint, so
/// the URL that is actually dereferenced must be validated separately (audit P1-4): it must be
/// https, its host must be on the allowlist, and it must not resolve to an internal / loopback /
/// link-local / cloud-metadata address.
/// </summary>
public static class WebFetchPolicy
{
    /// <summary>
    /// True for an absolute http/https URL. Used to gate rendering of untrusted web-source links so a
    /// <c>javascript:</c>-scheme URL is shown as text rather than a clickable script link (audit P3-3).
    /// </summary>
    public static bool IsHttpUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Scheme (https) + host-allowlist check on the URL to be fetched.</summary>
    public static bool IsAllowedUrl(string? url, IReadOnlyList<string> allowedDomains, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (!IsHostAllowed(parsed.Host, allowedDomains))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    /// <summary>True when <paramref name="host"/> equals or is a sub-domain of an allowlist entry.</summary>
    public static bool IsHostAllowed(string host, IReadOnlyList<string> allowedDomains)
    {
        var h = host.ToLowerInvariant();

        foreach (var entry in allowedDomains)
        {
            // Entries may carry a path (e.g. "ncbi.nlm.nih.gov/books") — match on the host part only.
            var domain = entry.Trim().TrimStart('.').ToLowerInvariant();
            var slash = domain.IndexOf('/');
            if (slash >= 0)
            {
                domain = domain[..slash];
            }

            if (domain.Length == 0)
            {
                continue;
            }

            if (h == domain || h.EndsWith("." + domain, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True for addresses that must never be fetched: loopback, private (RFC 1918), link-local,
    /// carrier-grade NAT, the unspecified address, and IPv6 unique-local / link-local. IPv4-mapped
    /// IPv6 addresses are unwrapped and re-checked.
    /// </summary>
    public static bool IsBlockedAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
        {
            return true; // 127.0.0.0/8 and ::1
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv4MappedToIPv6)
            {
                return IsBlockedAddress(ip.MapToIPv4());
            }

            if (ip.IsIPv6LinkLocal)
            {
                return true; // fe80::/10
            }

            var v6 = ip.GetAddressBytes();
            return (v6[0] & 0xFE) == 0xFC; // fc00::/7 unique-local
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] switch
            {
                0 => true,                             // 0.0.0.0/8 (this network / unspecified)
                10 => true,                            // 10.0.0.0/8
                127 => true,                           // loopback (also caught above)
                169 when b[1] == 254 => true,          // 169.254.0.0/16 link-local (incl. metadata)
                172 when b[1] is >= 16 and <= 31 => true, // 172.16.0.0/12
                192 when b[1] == 168 => true,          // 192.168.0.0/16
                100 when b[1] is >= 64 and <= 127 => true, // 100.64.0.0/10 CGNAT
                _ => false
            };
        }

        return false;
    }
}
