using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Starward.Features.ViewHost;
using System;

namespace Starward;

public partial class App : Application
{

    public static new App Current => (App)Application.Current!;


    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }


    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    AppConfig.WriteCrashLog("AppDomain", ex);
                }
            };
            try
            {
                AppConfig.Initialize();
                desktop.MainWindow = new MainWindow();
            }
            catch (Exception ex)
            {
                AppConfig.WriteCrashLog("Startup", ex);
                desktop.MainWindow = new MainWindow(new MainWindowViewModel(fatalError: ex.Message));
            }
        }
        base.OnFrameworkInitializationCompleted();
    }

}
