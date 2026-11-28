using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace EachOneCoin.Networking
{
    // Versioned invitation; deliberately excludes authentication and reconnect secrets.
    public static class LanInvitation
    {
        private const string Prefix = "eachonecoin-lan:1:";

        public static string Encode(IPAddress address, ushort port)
        {
            if (!IsUsableAddress(address) || port == 0) throw new ArgumentException("A unicast IPv4 address and nonzero port are required.");
            return Prefix + address + ":" + port.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryDecode(string text, out IPAddress address, out ushort port)
        {
            address = null;
            port = 0;
            if (text == null || !text.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            string[] parts = text.Substring(Prefix.Length).Split(':');
            if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var parsed) || !IsUsableAddress(parsed) ||
                !ushort.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPort) || parsedPort == 0) return false;
            address = parsed;
            port = parsedPort;
            return true;
        }

        private static bool IsUsableAddress(IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) return false;
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] != 0 && bytes[0] < 224 && !(bytes[0] == 169 && bytes[1] == 254);
        }
    }
}
