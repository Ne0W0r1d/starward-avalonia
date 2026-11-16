using Avalonia.Controls;
using System;
using System.Diagnostics;

namespace Starward.Features.ViewHost;

public partial class MainWindow : Window
{

    public MainWindow()
        : this(AppConfig.GetService<MainWindowViewModel>())
    {
    }


    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RestartRequested += OnRestartRequested;
        Opened += (_, _) => viewModel.RestoreInitialGame();
    }


    private void OnRestartRequested(object? sender, EventArgs e)
    {
        try
        {
            Process.Start(AppConfig.CreateSelfStartInfo());
        }
        catch (Exception ex)
        {
            AppConfig.WriteCrashLog("Restart", ex);
        }
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

}
