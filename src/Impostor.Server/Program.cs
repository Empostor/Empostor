using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using Impostor.Api.Config;
using Impostor.Api.Events;
using Impostor.Api.Events.Managers;
using Impostor.Api.Games;
using Impostor.Api.Games.Managers;
using Impostor.Api.Languages;
using Impostor.Api.Net.Custom;
using Impostor.Api.Net.Manager;
using Impostor.Api.Plugins;
using Impostor.Api.Utils;
using Impostor.Server.Commands;
using Impostor.Server.Commands.Commands;
using Impostor.Server.Events;
using Impostor.Server.Http;
using Impostor.Server.Net;
using Impostor.Server.Net.Custom;
using Impostor.Server.Net.Factories;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.Messages;
using Impostor.Server.Plugins;
using Impostor.Server.Recorder;
using Impostor.Server.Service.Admin.Ban;
using Impostor.Server.Service.Admin.Chat;
using Impostor.Server.Service.Admin.Reactor;
using Impostor.Server.Service.Admin.Report;
using Impostor.Server.Service.Api;
using Impostor.Server.Service.Auth;
using Impostor.Server.Service.Stat;
using Impostor.Server.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Next.Hazel.Extensions;
using Serilog;
using Serilog.Events;
using Serilog.Settings.Configuration;

namespace Impostor.Server
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
            try
            {
                Log.Information("Starting Empostor v{0}", DotnetUtils.Version);
                CreateHostBuilder(args).Build().Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Impostor terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IConfiguration CreateConfiguration(string[] args) =>
            new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("config.json", true)
                .AddJsonFile("config.Development.json", true)
                .AddEnvironmentVariables(prefix: "IMPOSTOR_")
                .AddCommandLine(args)
                .Build();

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            var configuration = CreateConfiguration(args);
            var pluginConfig = configuration.GetSection("PluginLoader").Get<PluginConfig>() ?? new PluginConfig();
            var httpConfig = configuration.GetSection(HttpServerConfig.Section).Get<HttpServerConfig>() ?? new HttpServerConfig();

            var hostBuilder = Host.CreateDefaultBuilder(args)
                .UseContentRoot(Directory.GetCurrentDirectory())
#if DEBUG
                .UseEnvironment(Environment.GetEnvironmentVariable("IMPOSTOR_ENV") ?? "Development")
#else
                .UseEnvironment("Production")
#endif
                .ConfigureAppConfiguration(b => b.AddConfiguration(configuration))
                .ConfigureServices((host, services) =>
                {
                    var debug = host.Configuration.GetSection(DebugConfig.Section).Get<DebugConfig>() ?? new DebugConfig();

                    services.AddSingleton<ServerEnvironment>();
                    services.AddSingleton<IServerEnvironment>(p => p.GetRequiredService<ServerEnvironment>());
                    services.AddSingleton<IDateTimeProvider, RealDateTimeProvider>();

                    services.Configure<DebugConfig>(host.Configuration.GetSection(DebugConfig.Section));
                    services.Configure<AntiCheatConfig>(host.Configuration.GetSection(AntiCheatConfig.Section));
                    services.Configure<CompatibilityConfig>(host.Configuration.GetSection(CompatibilityConfig.Section));
                    services.Configure<ServerConfig>(host.Configuration.GetSection(ServerConfig.Section));
                    services.Configure<TimeoutConfig>(host.Configuration.GetSection(TimeoutConfig.Section));
                    services.Configure<HttpServerConfig>(host.Configuration.GetSection(HttpServerConfig.Section));
                    services.Configure<AdminConfig>(host.Configuration.GetSection(AdminConfig.Section));
                    services.Configure<DiscordWebhookConfig>(host.Configuration.GetSection(DiscordWebhookConfig.Section));
                    services.Configure<PlayerStatsConfig>(host.Configuration.GetSection(PlayerStatsConfig.Section));
                    services.Configure<ChatFilterConfig>(host.Configuration.GetSection(ChatFilterConfig.Section));

                    services.AddSingleton<AuthCacheService>();
                    services.AddHttpClient("innersloth", client =>
                    {
                        client.Timeout = TimeSpan.FromSeconds(15);
                        client.DefaultRequestHeaders.Add("User-Agent",
                            "UnityPlayer/2022.3.44f1 (UnityWebRequest/1.0, libcurl/7.84.0-DEV)");
                        client.DefaultRequestHeaders.Add("X-Unity-Version", "2022.3.44f1");
                    });

                    services.AddSingleton<ICompatibilityManager, CompatibilityManager>();
                    services.AddSingleton<ClientManager>();
                    services.AddSingleton<IClientManager>(p => p.GetRequiredService<ClientManager>());

                    if (debug.GameRecorderEnabled)
                    {
                        services.AddSingleton<ObjectPoolProvider>(new DefaultObjectPoolProvider());
                        services.AddSingleton<ObjectPool<PacketSerializationContext>>(sp =>
                            sp.GetRequiredService<ObjectPoolProvider>()
                              .Create(new PacketSerializationContextPooledObjectPolicy()));
                        services.AddSingleton<PacketRecorder>();
                        services.AddHostedService(sp => sp.GetRequiredService<PacketRecorder>());
                        services.AddSingleton<IClientFactory, ClientFactory<ClientRecorder>>();
                    }
                    else
                    {
                        services.AddSingleton<IClientFactory, ClientFactory<Client>>();
                    }

                    services.AddSingleton<GameManager>();
                    services.AddSingleton<IGameManager>(p => p.GetRequiredService<GameManager>());
                    services.AddSingleton<ListingManager>();

                    services.AddEventPools();

                    // Command
                    services.AddSingleton<CommandService>();
                    services.AddHostedService<CommandBootstrapper>();

                    // Admin
                    services.AddSingleton<BanStore>();
                    services.AddSingleton<ReportStore>();
                    services.AddSingleton<IEventListener, BanEnforcementListener>();
                    services.AddSingleton<IEventListener, ReactorHandshakeListener>();
                    services.AddSingleton<PlayerLogStore>();
                    services.AddSingleton<IEventListener, PlayerLogListener>();
                    services.AddSingleton<PlayerStatsStore>();
                    services.AddSingleton<StatCommand>();
                    services.AddSingleton<IEventListener, DiscordWebhookListener>();
                    services.AddSingleton<IEventListener, PlayerStatsListener>();
                    services.AddSingleton<ChatFilterStore>();
                    services.AddSingleton<IEventListener, ChatFilterListener>();

                    // Language
                    services.AddSingleton<LanguageService>();

                    services.AddHazel();
                    services.AddSingleton<ICustomMessageManager<ICustomRootMessage>, CustomMessageManager<ICustomRootMessage>>();
                    services.AddSingleton<ICustomMessageManager<ICustomRpc>, CustomMessageManager<ICustomRpc>>();
                    services.AddSingleton<IMessageWriterProvider, MessageWriterProvider>();
                    services.AddSingleton<IGameCodeFactory, GameCodeFactory>();
                    services.AddSingleton<IEventManager, EventManager>();
                    services.AddSingleton<Matchmaker>();
                    services.AddHostedService<MatchmakerService>();
                })
                .UseSerilog((context, logCfg) =>
                {
#if DEBUG
                    var logLevel = LogEventLevel.Debug;
#else
                    var logLevel = LogEventLevel.Information;
#endif
                    if (args.Contains("--verbose")) logLevel = LogEventLevel.Verbose;
                    else if (args.Contains("--errors-only"))
                    {
                        logLevel = LogEventLevel.Error;
                    }

                    static Assembly? TryLoad(AssemblyLoadContext ctx, AssemblyName name)
                    {
                        foreach (var p in new[] { AppDomain.CurrentDomain.BaseDirectory, Directory.GetCurrentDirectory() })
                        {
                            try { return ctx.LoadFromAssemblyPath(Path.Combine(p, name.Name + ".dll")); }
                            catch (FileNotFoundException) { }
                        }

                        return null;
                    }

                    AssemblyLoadContext.Default.Resolving += TryLoad;
                    logCfg
                        .MinimumLevel.Is(logLevel)
#if DEBUG
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
#else
                        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
#endif
                        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .WriteTo.Console()
                        .ReadFrom.Configuration(context.Configuration,
                            new ConfigurationReaderOptions(ConfigurationAssemblySource.AlwaysScanDllFiles));
                    AssemblyLoadContext.Default.Resolving -= TryLoad;
                })
                .UseConsoleLifetime()
                .UsePluginLoader(pluginConfig);

            if (httpConfig.Enabled)
            {
                hostBuilder.ConfigureWebHostDefaults(builder =>
                {
                    builder.ConfigureServices(s =>
                    {
                        s.AddControllers();
                    });
                    builder.Configure(app =>
                    {
                        foreach (var p in app.ApplicationServices.GetRequiredService<PluginLoaderService>().Plugins)
                            if (p.Startup is IPluginHttpStartup s)
                            {
                                s.ConfigureWebApplication(app);
                            }

                        app.UseRouting();
                        app.UseEndpoints(e => e.MapControllers());
                    });
                    builder.ConfigureKestrel(opt =>
                        opt.Listen(IPAddress.Parse(httpConfig.ListenIp), httpConfig.ListenPort));
                });
            }

            return hostBuilder;
        }
    }
}
