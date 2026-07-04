using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Utils
{
    internal class IpGeolocationService
    {
        private const string ApiUrl = "http://ip-api.com/json/{0}?lang=zh-CN&fields=status,country,regionName,city";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

        private readonly ConcurrentDictionary<IPAddress, (string Location, DateTime Expiry)> _cache = new();
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<IpGeolocationService> _logger;

        public IpGeolocationService(IHttpClientFactory httpClientFactory, ILogger<IpGeolocationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async ValueTask<string> GetLocationAsync(IPAddress? ip)
        {
            if (ip == null || IsPrivateIp(ip))
            {
                return string.Empty;
            }

            // Check cache
            if (_cache.TryGetValue(ip, out var cached) && cached.Expiry > DateTime.UtcNow)
            {
                return cached.Location;
            }

            var location = await QueryApiAsync(ip);
            _cache[ip] = (location, DateTime.UtcNow.Add(CacheTtl));
            return location;
        }

        private async Task<string> QueryApiAsync(IPAddress ip)
        {
            try
            {
                var normalizedIp = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
                var url = string.Format(ApiUrl, normalizedIp);

                using var client = _httpClientFactory.CreateClient("ipgeo");
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
                var json = await client.GetStringAsync(url, cts.Token);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("status", out var status) && status.GetString() == "success")
                {
                    var country = root.TryGetProperty("country", out var c) ? c.GetString() ?? string.Empty : string.Empty;
                    var region = root.TryGetProperty("regionName", out var r) ? r.GetString() ?? string.Empty : string.Empty;
                    var city = root.TryGetProperty("city", out var ct) ? ct.GetString() ?? string.Empty : string.Empty;

                    if (string.IsNullOrEmpty(country) && string.IsNullOrEmpty(city))
                    {
                        return string.Empty;
                    }

                    var parts = country != city
                        ? (region != city ? $"{country} {region} {city}" : $"{country} {city}")
                        : country;

                    return parts.Trim();
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "IP geolocation lookup failed for {Ip}", ip);
                return string.Empty;
            }
        }

        private static bool IsPrivateIp(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            if (ip.AddressFamily != AddressFamily.InterNetwork)
            {
                return true; // Don't query non-IPv4 for now
            }

            var bytes = ip.GetAddressBytes();
            if (bytes.Length != 4)
            {
                return true;
            }

            // 127.0.0.0/8
            if (bytes[0] == 127)
            {
                return true;
            }

            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            return false;
        }
    }
}
