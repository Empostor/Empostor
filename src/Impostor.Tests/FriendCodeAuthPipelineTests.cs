using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Config;
using Impostor.Api.Games;
using Impostor.Server.Http;
using Impostor.Server.Net;
using Impostor.Server.Service.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Impostor.Tests;

public sealed class FriendCodeAuthPipelineTests
{
    [Theory]
    [InlineData(ServerVerificationMode.DeltaPort, 26000)]
    [InlineData(ServerVerificationMode.GameCode, 0)]
    public async Task TokenEndpoint_ResolvesFriendCodeWithInnersloth(ServerVerificationMode mode, int expectedPort)
    {
        var puid = Guid.NewGuid().ToString("N");
        var bearer = CreateBearer(puid);
        var calls = new List<(HttpMethod Method, Uri Uri, string? Authorization)>();
        var factory = new StubHttpClientFactory(request =>
        {
            calls.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.ToString()));
            return JsonResponse(HttpStatusCode.OK, """{"data":{"attributes":{"username":"Player","discriminator":"1234"}}}""");
        });
        using var cache = new AuthCacheService(NullLogger<AuthCacheService>.Instance);
        var server = new ServerConfig
        {
            VerificationMode = mode,
            DeltaPortStart = 26000,
            DeltaPortEnd = 26000,
            DeltaPortLowWaterMark = 0,
        };
        using var pool = new PortPoolService(NullLogger<PortPoolService>.Instance, Options.Create(server));
        var listener = new StubDeltaListenerManager();
        var controller = CreateController(factory, cache, pool, server, listener);

        var result = await controller.GetToken(new TokenController.TokenRequest
        {
            Puid = puid,
            Username = "Player",
            ClientVersion = 123,
        }, bearer);

        var ok = Assert.IsType<OkObjectResult>(result);
        var encoded = Assert.IsType<string>(ok.Value);
        using var json = JsonDocument.Parse(Convert.FromBase64String(encoded));
        Assert.Equal("Player#1234", json.RootElement.GetProperty("Content").GetProperty("FriendCode").GetString());
        Assert.Equal(puid, json.RootElement.GetProperty("Content").GetProperty("Puid").GetString());
        Assert.Equal(123, json.RootElement.GetProperty("Content").GetProperty("ClientVersion").GetInt32());
        var hash = json.RootElement.GetProperty("Hash").GetString();
        Assert.Equal("Player#1234", cache.FindByToken(hash)?.FriendCode);
        Assert.Equal(expectedPort, json.RootElement.GetProperty("Port").GetInt32());
        Assert.Equal(expectedPort == 0 ? Array.Empty<int>() : new[] { expectedPort }, listener.StartedPorts);
        var call = Assert.Single(calls);
        Assert.Equal(HttpMethod.Get, call.Method);
        Assert.Equal("backend.innersloth.proxy.amongusclub.cn", call.Uri.Host);
        Assert.Equal("/api/user/username", call.Uri.AbsolutePath);
        Assert.Equal(bearer, call.Authorization);
    }

    [Fact]
    public async Task DisableMode_ReturnsLegacyTokenWithoutAuthorizationOrVerification()
    {
        var factory = new StubHttpClientFactory(_ => throw new InvalidOperationException("Disable mode must not use an auth provider."));
        using var cache = new AuthCacheService(NullLogger<AuthCacheService>.Instance);
        var server = new ServerConfig
        {
            VerificationMode = ServerVerificationMode.Disable,
            DeltaPortStart = 26000,
            DeltaPortEnd = 26100,
        };
        using var pool = new PortPoolService(NullLogger<PortPoolService>.Instance, Options.Create(server));
        var listener = new StubDeltaListenerManager();
        var controller = CreateController(factory, cache, pool, server, listener);

        var result = await controller.GetToken(new TokenController.TokenRequest
        {
            Puid = "legacy-puid",
            ClientVersion = 123,
        }, authorization: null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var encoded = Assert.IsType<string>(ok.Value);
        using var json = JsonDocument.Parse(Convert.FromBase64String(encoded));
        var content = json.RootElement.GetProperty("Content");
        Assert.Equal("legacy-puid", content.GetProperty("Puid").GetString());
        Assert.Equal(123, content.GetProperty("ClientVersion").GetInt32());
        Assert.Equal(new DateTime(2012, 12, 21), content.GetProperty("ExpiresAt").GetDateTime());
        Assert.Equal("impostor_was_here", json.RootElement.GetProperty("Hash").GetString());
        Assert.False(content.TryGetProperty("FriendCode", out _));
        Assert.False(json.RootElement.TryGetProperty("Port", out _));
        Assert.Equal(0, factory.ClientsCreated);
        Assert.Null(cache.FindByToken("impostor_was_here"));
        Assert.Empty(listener.StartedPorts);
        Assert.False(pool.IsEnabled);
        Assert.Equal(0, pool.AllocatePort("legacy-puid"));
    }

    [Fact]
    public void GameCodeChallenge_IsOneUseAndResolvesToRealGame()
    {
        using var auth = CreateGameCodeAuth();
        var target = GameCode.Create();
        var user = new UserAuthInfo { ProductUserId = "puid", MatchmakerToken = "session" };
        var challenge = auth.CreateChallenge(target, user, _ => false);

        Assert.True(auth.TryGetTarget(challenge, out var mapped));
        Assert.Equal(target, mapped);
        Assert.True(auth.TryResolveChallenge(challenge, 8, out var matchedUser, out mapped, out _));
        Assert.Same(user, matchedUser);
        Assert.Equal(target, mapped);
        Assert.False(auth.TryResolveChallenge(challenge, 9, out _, out _, out _));
    }

    [Fact]
    public void GameCodeLegacyJoin_ConsumesMatchingPendingIdentityOnlyOnce()
    {
        using var auth = CreateGameCodeAuth();
        var target = GameCode.Create();
        var user = new UserAuthInfo
        {
            ProductUserId = "puid",
            MatchmakerToken = "session",
            Name = "Player",
            ClientVersion = 4321,
            ClientIp = "192.0.2.20",
        };
        auth.RegisterPendingJoin(target, user);

        Assert.True(auth.TryConsumePendingJoin(target, "::ffff:192.0.2.20", "Player", 4321, out var matched, out _));
        Assert.Same(user, matched);
        Assert.False(auth.TryConsumePendingJoin(target, "192.0.2.20", "Player", 4321, out _, out _));
    }

    [Fact]
    public void DeltaPortPool_ReservesLowWaterAndWaitsForListenerDrainBeforeReuse()
    {
        var config = new ServerConfig
        {
            VerificationMode = ServerVerificationMode.DeltaPort,
            DeltaPortStart = 26000,
            DeltaPortEnd = 26001,
            DeltaPortLowWaterMark = 1,
        };
        using var pool = new PortPoolService(NullLogger<PortPoolService>.Instance, Options.Create(config));
        var issued = new ConcurrentBag<int>();

        Parallel.For(0, 32, i =>
        {
            var port = pool.AllocatePort($"puid-{i}");
            if (port > 0) issued.Add(port);
        });
        Assert.Single(issued);

        var port = Assert.Single(issued);
        pool.ReturnPort(port);
        Assert.Equal(0, pool.AllocatePort("before-drain"));
        pool.CompletePortReturn(port);
        Assert.Equal(port, pool.AllocatePort("after-drain"));
    }

    [Fact]
    public void GameCodeMode_DisablesDynamicPortAllocation()
    {
        var config = new ServerConfig
        {
            VerificationMode = ServerVerificationMode.GameCode,
            DeltaPortStart = 26000,
            DeltaPortEnd = 26100,
        };
        using var pool = new PortPoolService(NullLogger<PortPoolService>.Instance, Options.Create(config));

        Assert.False(pool.IsEnabled);
        Assert.Equal(0, pool.AllocatePort("puid"));
    }


    private static TokenController CreateController(
        IHttpClientFactory clientFactory,
        AuthCacheService cache,
        PortPoolService pool,
        ServerConfig serverConfig,
        IDeltaListenerManager listener)
    {
        var controller = new TokenController(
            NullLogger<TokenController>.Instance,
            clientFactory,
            cache,
            Options.Create(serverConfig),
            pool,
            listener);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        return controller;
    }

    private static string CreateBearer(string puid)
    {
        var token = new JwtSecurityToken(claims: new[] { new Claim("sub", puid) });
        return "Bearer " + new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static GameCodeAuthManager CreateGameCodeAuth() => new(
        NullLogger<GameCodeAuthManager>.Instance,
        Options.Create(new ServerConfig { TemporaryGameCodeLifetimeSeconds = 60 }));

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
        public int ClientsCreated { get; private set; }

        public HttpClient CreateClient(string name)
        {
            ClientsCreated++;
            return new HttpClient(new StubHandler(_handler), disposeHandler: true);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    private sealed class StubDeltaListenerManager : IDeltaListenerManager
    {
        public List<int> StartedPorts { get; } = new();

        public ValueTask<bool> StartDeltaListenerAsync(int port)
        {
            StartedPorts.Add(port);
            return ValueTask.FromResult(true);
        }
        public ValueTask StopDeltaListenerAsync(int port) => ValueTask.CompletedTask;
    }
}
