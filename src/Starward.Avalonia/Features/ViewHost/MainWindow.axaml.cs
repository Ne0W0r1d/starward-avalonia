using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Starward.Features.GameLauncher;
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


    private void GameTile_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel
            && sender is Button { DataContext: GameBizIcon game })
        {
            viewModel.SelectGameCommand.Execute(game);
        }
    }


    private void GameSelectorBackground_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender) && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CloseGameSelectorCommand.Execute(null);
        }
    }

}
