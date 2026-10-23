using System;
using System.Net;
using System.Net.Sockets;

namespace Galaxy101.Online
{
    /// <summary>
    /// Which server addresses the game may talk to. HTTPS (and WSS) everywhere; plain HTTP only to this machine or a
    /// private network (localhost, 127.0.0.0/8, ::1, 10/8, 172.16/12, 192.168/16, *.local, *.localhost, *.test) — the
    /// local test server. Unity's global "Allow downloads over HTTP" switch is on because UnityWebRequest needs it for
    /// those local addresses; this check is what keeps every public address on TLS.
    /// </summary>
    public static class ServerAddress
    {
        public static bool IsAllowed(string url, out string reason)
        {
            reason = null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) { reason = "not an absolute URL"; return false; }
            if (uri.Scheme == Uri.UriSchemeHttps) return true;
            if (uri.Scheme != Uri.UriSchemeHttp) { reason = "scheme " + uri.Scheme + " is not http(s)"; return false; }
            if (IsLocal(uri.Host)) return true;
            reason = "plain HTTP is only allowed to this machine or a private network; use https://";
            return false;
        }

        public static bool IsLocal(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            host = host.Trim('[', ']').ToLowerInvariant();
            if (host == "localhost" || host.EndsWith(".localhost") || host.EndsWith(".local") || host.EndsWith(".test")) return true;
            if (!IPAddress.TryParse(host, out var ip)) return false;
            if (IPAddress.IsLoopback(ip)) return true;
            if (ip.AddressFamily == AddressFamily.InterNetworkV6) return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
            var b = ip.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
        }
    }
}
