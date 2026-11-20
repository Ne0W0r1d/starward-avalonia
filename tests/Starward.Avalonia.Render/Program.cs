using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Skia;
using Avalonia.Threading;
using Starward;
using Starward.Features.ViewHost;
using System;
using System.IO;
using System.Threading;

internal static class Program
{

    [STAThread]
    public static void Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "starward-render.png");
        string page = args.Length > 1 ? args[1] : "Launcher";
        bool showGameSelector = page.EndsWith(":Selector", StringComparison.OrdinalIgnoreCase);
        if (showGameSelector)
        {
            page = page[..page.IndexOf(':')];
        }
        if (page.StartsWith("Settings:", StringComparison.OrdinalIgnoreCase))
        {
            page = "Settings";
        }
        AppConfig.Initialize();
        AppBuilder.Configure<App>()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                  .UseSkia()
                  .WithInterFont()
                  .SetupWithoutStarting();

        MainWindowViewModel vm = AppConfig.GetService<MainWindowViewModel>();
        MainWindow window = new(vm);
        window.Show();
        vm.RestoreInitialGame();
        if (args.Length > 1 && args[1].StartsWith("Settings:", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(args[1].Split(':')[1], out int tab))
        {
            vm.SettingsPage.SelectedTabIndex = tab;
        }
        vm.NavigateCommand.Execute(page);
        vm.IsGameSelectorOpen = showGameSelector;
        for (int i = 0; i < 60; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(50);
        }
        window.Measure(new Size(1280, 820));
        window.Arrange(new Rect(0, 0, 1280, 820));
        for (int i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(30);
        }
        RenderTargetBitmap bitmap = new(new PixelSize(1280, 820), new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(output);
        Console.WriteLine(output);
    }

}
