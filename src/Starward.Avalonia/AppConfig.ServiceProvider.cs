using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Starward.Core.Gacha.Genshin;
using Starward.Core.Gacha.StarRail;
using Starward.Core.Gacha.ZZZ;
using Starward.Core.HoYoPlay;
using Starward.Features.Compatibility;
using Starward.Features.Database;
using Starward.Features.Gacha;
using Starward.Features.GameLauncher;
using Starward.Features.HoYoPlay;
using Starward.Features.PlayTime;
using Starward.Features.Screenshot;
using System;
using System.IO;
using System.Net;
using System.Net.Http;

namespace Starward;

public static partial class AppConfig
{

    private static IServiceProvider? _serviceProvider;

    private static readonly object _serviceLock = new();


    public static void BuildServiceProvider()
    {
        lock (_serviceLock)
        {
            if (_serviceProvider is not null)
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
            Log.Logger = new LoggerConfiguration()
                         .MinimumLevel.Information()
                         .WriteTo.File(path: LogFile,
                                       shared: true,
                                       outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u4}] {SourceContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}")
                         .Enrich.FromLogContext()
                         .CreateLogger();
            Log.Information("Welcome to Starward v{version}\r\nSystem: {system}\r\nCommand line: {cmd}",
                            AppVersion, Environment.OSVersion, Environment.CommandLine);

            ServiceCollection sc = new();
            sc.AddMemoryCache();
            sc.AddLogging(c => c.AddSerilog(Log.Logger, dispose: true));
            sc.AddHttpClient().ConfigureHttpClientDefaults(ConfigDefaultHttpClient);

            sc.AddSingleton<HoYoPlayClient>();
            sc.AddSingleton<HoYoPlayService>();
            sc.AddSingleton<SteamLibraryService>();
            sc.AddSingleton<CompatLayerService>();
            sc.AddSingleton<GameLauncherService>();
            sc.AddSingleton<PlayTimeStatsService>();
            sc.AddSingleton<PlayTimeRecordService>();
            sc.AddSingleton<ScreenshotService>();

            sc.AddSingleton<GenshinGachaClient>();
            sc.AddSingleton<StarRailGachaClient>();
            sc.AddSingleton<ZZZGachaClient>();
            sc.AddSingleton<GenshinGachaService>();
            sc.AddSingleton<StarRailGachaService>();
            sc.AddSingleton<ZZZGachaService>();
            sc.AddSingleton<GachaServiceFactory>();

            sc.AddSingleton<Features.ViewHost.MainWindowViewModel>();
            sc.AddTransient<Features.ViewHost.LauncherViewModel>();
            sc.AddTransient<Features.ViewHost.GachaViewModel>();
            sc.AddTransient<Features.ViewHost.ScreenshotViewModel>();
            sc.AddTransient<Features.ViewHost.PlayTimeViewModel>();
            sc.AddTransient<Features.ViewHost.SettingsViewModel>();

            _serviceProvider = sc.BuildServiceProvider();
        }
    }


    public static T GetService<T>() where T : notnull
    {
        BuildServiceProvider();
        return _serviceProvider!.GetRequiredService<T>();
    }


    public static ILogger<T> GetLogger<T>()
    {
        BuildServiceProvider();
        return _serviceProvider!.GetRequiredService<ILogger<T>>();
    }


    public static Microsoft.Data.Sqlite.SqliteConnection CreateDatabaseConnection()
    {
        return DatabaseService.CreateConnection();
    }


    private static void ConfigDefaultHttpClient(this IHttpClientBuilder builder)
    {
        builder.RemoveAllLoggers();
        builder.ConfigureHttpClient(client =>
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", $"Starward/{AppVersion}");
            client.DefaultRequestHeaders.Add("X-Sw-Device-Id", DeviceId.ToString());
            client.DefaultRequestHeaders.Add("X-Sw-Session-Id", SessionId.ToString());
            client.DefaultRequestHeaders.Add("X-Sw-App-Version", AppVersion);
            client.DefaultRequestHeaders.Add("X-Sw-App-Type", "Desktop");
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        });
        builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true,
            EnableMultipleHttp3Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        });
    }

}
