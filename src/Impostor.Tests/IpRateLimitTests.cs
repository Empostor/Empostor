using System;
using System.Net;
using Impostor.Api.Config;
using Impostor.Server.Service.Ip;
using Microsoft.Extensions.Options;
using Xunit;

namespace Impostor.Tests;

public sealed class IpRateLimitTests
{
    [Fact]
    public void TryRegister_AllowsThirtyRequestsAndRejectsTheNext()
    {
        var config = new AntiCheatConfig { EnableRateLimits = true };
        var service = new IpRateLimitService(Options.Create(config));
        var ip = IPAddress.Parse("192.0.2.10");

        for (var i = 0; i < IpRateLimitService.MaxRequestsPerWindow; i++)
        {
            Assert.True(service.TryRegister(ip, out _));
        }

        Assert.False(service.TryRegister(ip, out var retryAfter));
        Assert.True(retryAfter > TimeSpan.Zero);
    }

    [Fact]
    public void TryRegister_IsDisabledWithAntiCheatRateLimits()
    {
        var config = new AntiCheatConfig { EnableRateLimits = false };
        var service = new IpRateLimitService(Options.Create(config));
        var ip = IPAddress.Parse("192.0.2.11");

        for (var i = 0; i < IpRateLimitService.MaxRequestsPerWindow + 1; i++)
        {
            Assert.True(service.TryRegister(ip, out var retryAfter));
            Assert.Equal(TimeSpan.Zero, retryAfter);
        }
    }
}

