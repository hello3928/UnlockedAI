using System.Net;
using System.Net.Sockets;

namespace UnlockedAI.Core.Web;

/// <summary>
/// Recognises addresses that point at this PC or the local network rather than the public web.
/// A request to one of those can reach a router, a NAS or a local service, so it is treated as
/// something that can change things, not as browsing.
/// </summary>
public static class LocalAddress
{
    private static readonly string[] LocalSuffixes = [".localhost", ".local", ".lan", ".internal", ".home.arpa"];

    public static bool IsLocal(Uri uri)
    {
        var host = uri.IdnHost.TrimEnd('.');

        if (IPAddress.TryParse(host.Trim('[', ']'), out var address))
        {
            return IsLocal(address);
        }

        // A name with no dot ("router", "nas") can only resolve on the local network.
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || !host.Contains('.')
            || LocalSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsLocal(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal || address.Equals(IPAddress.IPv6Any);
        }

        Span<byte> bytes = stackalloc byte[4];
        address.TryWriteBytes(bytes, out _);
        return bytes[0] switch
        {
            0 or 10 or 127 => true,
            169 => bytes[1] == 254,
            172 => bytes[1] is >= 16 and <= 31,
            192 => bytes[1] == 168,
            100 => bytes[1] is >= 64 and <= 127,
            _ => false,
        };
    }
}
