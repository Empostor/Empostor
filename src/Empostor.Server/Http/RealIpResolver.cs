using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;

namespace Empostor.Server.Http;

// Resolves the real client IP of an HTTP (TCP) request.
// Behind a CDN / reverse proxy Connection.RemoteIpAddress is the proxy's
// address, so the forwarding headers are checked first — otherwise rate
// limiting or banning would hit the CDN node instead of the actual user.
public static class RealIpResolver
{
    public static IPAddress? Resolve(HttpContext context)
    {
        var xRealIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xRealIp) && IPAddress.TryParse(xRealIp, out var realIp))
        {
            return Normalize(realIp);
        }

        var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(xForwardedFor))
        {
            var first = xForwardedFor.Split(',')[0].Trim();
            if (IPAddress.TryParse(first, out var forwardedIp))
            {
                return Normalize(forwardedIp);
            }
        }

        return Normalize(context.Connection.RemoteIpAddress);
    }

    private static IPAddress? Normalize(IPAddress? ip)
        => ip?.IsIPv4MappedToIPv6 == true ? ip.MapToIPv4() : ip;
}
