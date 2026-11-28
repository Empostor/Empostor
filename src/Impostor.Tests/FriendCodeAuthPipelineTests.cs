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
    [Fact]
    public async Task TokenEndpoint_ResolvesFriendCodeAndReturnsCompatibleTokenEnvelope()
    {
        var puid = Guid.NewGuid().ToString("N");
        var calls = new List<string>();
        var factory = new StubHttpClientFactory(request =>
        {
            calls.Add(request.RequestUri!.AbsolutePath);
            if (request.RequestUri.AbsolutePath == "/api/verify")
            {
                return JsonResponse(HttpStatusCode.OK,
                    $"{{\"VerifyStatus\":\"Verified\",\"FriendCode\":\"Player#1234\",\"ProductUserId\":\"{puid}\"}}");
            }

            throw new InvalidOperationException("Successful Ume lookup should short-circuit provider fallback.");
        });
        using var cache = new AuthCacheService(NullLogger<AuthCacheService>.Instance);
        var server = new ServerConfig { DeltaPortStart = 0, DeltaPortEnd = 0 };
        using var pool = new PortPoolService(NullLogger<PortPoolService>.Instance, Options.Create(server));
        var controller = CreateController(factory, cache, pool, server);

        var result = await controller.GetToken(new TokenController.TokenRequest
        {
            Puid = puid,
            Username = "Player",
            ClientVersion = 123,
        }, CreateBearer(puid));

        var ok = Assert.IsType<OkObjectResult>(result);
        var encoded = Assert.IsType<string>(ok.Value);
        using var json = JsonDocument.Parse(Convert.FromBase64String(encoded));
        Assert.Equal("Player#1234", json.RootElement.GetProperty("Content").GetProperty("FriendCode").GetString());
        Assert.Equal(puid, json.RootElement.GetProperty("Content").GetProperty("Puid").GetString());
        Assert.Equal(123, json.RootElement.GetProperty("Content").GetProperty("ClientVersion").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("Hash", out _));
        Assert.Equal(0, json.RootElement.GetProperty("Port").GetInt32());
        Assert.Equal(new[] { "/api/verify" }, calls);
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
        ServerConfig serverConfig)
    {
        var controller = new TokenController(
            NullLogger<TokenController>.Instance,
            clientFactory,
            cache,
            Options.Create(new AuthApiConfig
            {
                Mode = AuthApiMode.Both,
                UmeApiBaseUrl = "https://ume.example",
                UmeApiKey = "test-key",
            }),
            Options.Create(serverConfig),
            pool,
            new StubDeltaListenerManager());
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
        public HttpClient CreateClient(string name) => new(new StubHandler(_handler), disposeHandler: true);
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
        public ValueTask<bool> StartDeltaListenerAsync(int port) => ValueTask.FromResult(true);
        public ValueTask StopDeltaListenerAsync(int port) => ValueTask.CompletedTask;
    }
}
