using System.Net;

namespace CsdbBridge.Services;

/// <summary>
/// Decides whether a caller may receive the shared RomM connection (URL + API token). The bridge holds
/// the operator's <c>ROMM_API_TOKEN</c>; it hands that token only to callers on a trusted LAN so a client
/// (Xbox / desktop) can self-provision without a pairing code or a typed token. Pure and host-free so the
/// subnet decision unit-tests directly.
/// </summary>
public static class RommShareGate
{
    /// <summary>Default trusted ranges: the RFC 1918 private networks plus loopback.</summary>
    public static readonly string[] DefaultCidrs =
    {
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "127.0.0.0/8",
        "::1/128",
    };

    /// <summary>
    /// Whether the caller is on a trusted subnet. Prefers the first <c>X-Forwarded-For</c> hop (the real
    /// LAN client when the bridge sits behind Docker NAT / a proxy), falling back to the socket remote IP.
    /// </summary>
    /// <param name="remoteIp">The socket remote address (may be a Docker gateway under NAT).</param>
    /// <param name="forwardedFor">The raw <c>X-Forwarded-For</c> header value, or <c>null</c>.</param>
    /// <param name="allowedCidrs">The trusted CIDRs; e.g. <see cref="DefaultCidrs"/> or the operator's /24.</param>
    public static bool IsAllowed(IPAddress? remoteIp, string? forwardedFor, IEnumerable<string> allowedCidrs)
    {
        IPAddress? client = ResolveClientIp(remoteIp, forwardedFor);
        if (client is null)
        {
            return false;
        }

        if (client.IsIPv4MappedToIPv6)
        {
            client = client.MapToIPv4();
        }

        foreach (string cidr in allowedCidrs)
        {
            if (InCidr(client, cidr))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the effective client IP: the first X-Forwarded-For hop when present, else the socket IP.</summary>
    internal static IPAddress? ResolveClientIp(IPAddress? remoteIp, string? forwardedFor)
    {
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            string first = forwardedFor.Split(',')[0].Trim();
            if (IPAddress.TryParse(first, out IPAddress? xff))
            {
                return xff;
            }
        }

        return remoteIp;
    }

    /// <summary>Whether <paramref name="ip"/> falls within the CIDR block <paramref name="cidr"/>.</summary>
    internal static bool InCidr(IPAddress ip, string cidr)
    {
        string[] parts = cidr.Split('/', 2);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out IPAddress? network) || !int.TryParse(parts[1], out int prefix))
        {
            return false;
        }

        IPAddress candidate = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
        if (candidate.AddressFamily != network.AddressFamily)
        {
            return false;
        }

        byte[] ipBytes = candidate.GetAddressBytes();
        byte[] netBytes = network.GetAddressBytes();
        if (ipBytes.Length != netBytes.Length || prefix < 0 || prefix > ipBytes.Length * 8)
        {
            return false;
        }

        int fullBytes = prefix / 8;
        int remainingBits = prefix % 8;

        for (int i = 0; i < fullBytes; i++)
        {
            if (ipBytes[i] != netBytes[i])
            {
                return false;
            }
        }

        if (remainingBits > 0)
        {
            int mask = 0xFF << (8 - remainingBits);
            if ((ipBytes[fullBytes] & mask) != (netBytes[fullBytes] & mask))
            {
                return false;
            }
        }

        return true;
    }
}
