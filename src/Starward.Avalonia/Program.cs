using Avalonia;
using Starward;
using System;
using System.Threading.Tasks;

namespace Starward;

internal static class Program
{

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && CliRunner.IsCliCommand(args[0]))
        {
            return CliRunner.RunAsync(args).GetAwaiter().GetResult();
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppConfig.WriteCrashLog("Program", ex);
            return 1;
        }
        return 0;
    }


    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
                         .UsePlatformDetect()
                         .WithInterFont()
                         .LogToTrace();
    }

}


internal static class CliRunner
{

    private static readonly string[] Commands = ["log-playtime", "startgame"];


    public static bool IsCliCommand(string arg)
    {
        return Array.Exists(Commands, x => string.Equals(x, arg, StringComparison.OrdinalIgnoreCase));
    }


    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            AppConfig.Initialize();
            switch (args[0].ToLowerInvariant())
            {
                case "log-playtime":
                    if (args.Length >= 3 && int.TryParse(args[2], out int pid))
                    {
                        await AppConfig.GetService<Features.PlayTime.PlayTimeRecordService>()
                                        .LogPlayTimeAsync(args[1], pid);
                    }
                    break;
                case "startgame":
                    if (args.Length >= 2)
                    {
                        var gameId = Core.HoYoPlay.GameId.FromGameBiz(args[1]);
                        if (gameId is not null)
                        {
                            await AppConfig.GetService<Features.GameLauncher.GameLauncherService>()
                                            .StartGameAsync(gameId);
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            AppConfig.WriteCrashLog("CLI", ex);
            return 1;
        }
        return 0;
    }

}
