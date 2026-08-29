using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Empostor.Server.Service.Shared
{
    public class IpGeolocationService
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
                var response = await client.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var status = doc.RootElement.GetProperty("status").GetString();
                if (status != "success")
                {
                    return string.Empty;
                }

                var country = doc.RootElement.TryGetProperty("country", out var c) ? c.GetString() : string.Empty;
                var region = doc.RootElement.TryGetProperty("regionName", out var r) ? r.GetString() : string.Empty;
                var city = doc.RootElement.TryGetProperty("city", out var ci) ? ci.GetString() : string.Empty;

                return $"{country} {region} {city}".Trim();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "IP geolocation query failed for {Ip}", ip);
                return string.Empty;
            }
        }

        private static bool IsPrivateIp(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip))
            {
                return true;
            }

            if (ip.AddressFamily != AddressFamily.InterNetwork)
            {
                return true;
            }

            var bytes = ip.GetAddressBytes();
            return bytes[0] switch
            {
                10 => true,
                172 => bytes[1] >= 16 && bytes[1] <= 31,
                192 => bytes[1] == 168,
                _ => false,
            };
        }
    }
}
