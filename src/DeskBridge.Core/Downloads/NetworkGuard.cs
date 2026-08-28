using System.Net;
using System.Net.Sockets;
using DeskBridge.Core.Actions;
using DeskBridge.Core.Models;

namespace DeskBridge.Core.Downloads;

public class NetworkGuard
{
    public virtual async Task<IReadOnlyList<IPAddress>> ResolvePublicAsync(string host, CancellationToken cancellationToken)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new DeskBridgeActionException(ErrorCodes.PrivateNetworkBlocked, "Localhost destinations are blocked.");
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException exception)
        {
            throw new DeskBridgeActionException(ErrorCodes.InvalidUrl, $"Host could not be resolved: {exception.Message}");
        }

        if (addresses.Length == 0 || addresses.Any(IsPrivateOrReserved))
        {
            throw new DeskBridgeActionException(ErrorCodes.PrivateNetworkBlocked,
                "The destination resolves to a private or reserved network.");
        }

        return addresses;
    }

    public static bool IsPrivateOrReserved(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] is 0 or 10 or 127 ||
                   bytes[0] >= 224 ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                   (bytes[0] == 198 && bytes[1] is 18 or 19);
        }

        return address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal ||
               (bytes[0] & 0xFE) == 0xFC ||
               bytes.Take(12).All(value => value == 0);
    }
}
