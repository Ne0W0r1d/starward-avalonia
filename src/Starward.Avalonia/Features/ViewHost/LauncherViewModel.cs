using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.Compatibility;
using Starward.Features.GameLauncher;
using Starward.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Starward.Features.ViewHost;

public partial class LauncherViewModel : ObservableObject
{

    private readonly ILogger<LauncherViewModel> _logger;

    private readonly GameLauncherService _launcherService;

    private readonly CompatLayerService _compatLayerService;

    private readonly DispatcherTimer _runningTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private GameId? _gameId;

    private Process? _gameProcess;


    public LauncherViewModel(
        ILogger<LauncherViewModel> logger,
        GameLauncherService launcherService,
        CompatLayerService compatLayerService)
    {
        _logger = logger;
        _launcherService = launcherService;
        _compatLayerService = compatLayerService;
        _runningTimer.Tick += (_, _) => UpdateRunningTime();
    }


    [ObservableProperty]
    public partial string GameName { get; set; } = "";

    [ObservableProperty]
    public partial string ServerName { get; set; } = "";

    [ObservableProperty]
    public partial Bitmap? GameIcon { get; set; }

    [ObservableProperty]
    public partial string? InstallPath { get; set; }

    [ObservableProperty]
    public partial string LocalVersionText { get; set; } = "-";

    [ObservableProperty]
    public partial string LatestVersionText { get; set; } = "-";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial string PrimaryActionText { get; set; } = "Select install folder";

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string? RunningTime { get; set; }

    [ObservableProperty]
    public partial string CompatLayerText { get; set; } = "Not configured";

    [ObservableProperty]
    public partial string PrefixPath { get; set; } = "";

    [ObservableProperty]
    public partial string GameStateText { get; set; } = "";

    [ObservableProperty]
    public partial Bitmap? Logo { get; set; }


    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));


    public void UpdateGame(GameId gameId)
    {
        _gameId = gameId;
        GameName = gameId.GameBiz.ToGameName();
        ServerName = gameId.GameBiz.ToGameServerName();
        GameIcon = ImageHelper.LoadAsset(gameId.GameBiz.Game switch
        {
            GameBiz.bh3 => "Assets/Image/icon_bh3.jpg",
            GameBiz.hk4e => "Assets/Image/icon_ys.jpg",
            GameBiz.hkrpg => "Assets/Image/icon_sr.jpg",
            GameBiz.nap => "Assets/Image/icon_zzz.jpg",
            _ => "Assets/Image/Transparent.png",
        });
        Error = null;
        InstallPath = GameLauncherService.GetGameInstallPath(gameId);
        PrefixPath = AppConfig.IsLinux ? _compatLayerService.GetPrefixPath(gameId.GameBiz) : "";
        _ = LoadAsync();
    }


    public void Activate()
    {
        if (_gameId is not null)
        {
            _ = LoadAsync();
        }
    }


    private async Task LoadAsync()
    {
        if (_gameId is null)
        {
            return;
        }
        IsBusy = true;
        try
        {
            InstallPath = GameLauncherService.GetGameInstallPath(_gameId);
            Version? local = await _launcherService.GetLocalGameVersionAsync(_gameId);
            bool exeExists = await _launcherService.IsGameExeExistsAsync(_gameId);
            IsInstalled = exeExists && local is not null;
            LocalVersionText = local?.ToString() ?? "-";
            GameStateText = IsInstalled ? "Installed" : "Not installed";
            PrimaryActionText = IsInstalled ? "Start game" : "Select install folder";
            _gameProcess = await _launcherService.GetGameProcessAsync(_gameId);
            IsRunning = _gameProcess is not null;
            if (IsRunning)
            {
                PrimaryActionText = "Game is running";
                _runningTimer.Start();
                UpdateRunningTime();
            }
            else
            {
                _runningTimer.Stop();
                RunningTime = null;
            }
            if (AppConfig.IsLinux)
            {
                CompatLayer? layer = await _compatLayerService.GetSelectedLayerAsync(_gameId.GameBiz);
                CompatLayerText = layer is null ? "Not detected" : layer.DisplayName;
                PrefixPath = _compatLayerService.GetPrefixPath(_gameId.GameBiz);
            }
            (Version? latest, Version? predownload) = await _launcherService.GetLatestGameVersionAsync(_gameId);
            LatestVersionText = latest?.ToString() ?? "-";
            IsUpdateAvailable = IsInstalled && latest is not null && local is not null && latest > local;
            StatusText = IsUpdateAvailable
                ? $"Update available: {latest}"
                : IsInstalled ? "Ready to launch" : "Choose the folder containing the game executable";
            if (IsRunning)
            {
                StatusText = "Game is running";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Load launcher state {gameId}", _gameId.Id);
            StatusText = "Unable to fetch game information";
        }
        finally
        {
            IsBusy = false;
        }
    }


    private void UpdateRunningTime()
    {
        try
        {
            if (_gameProcess is null || _gameProcess.HasExited)
            {
                _runningTimer.Stop();
                IsRunning = false;
                RunningTime = null;
                _gameProcess = null;
                if (_gameId is not null)
                {
                    _ = LoadAsync();
                }
                return;
            }
            TimeSpan time = DateTime.Now - _gameProcess.StartTime;
            RunningTime = $"{time.Days * 24 + time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        }
        catch
        {
            _runningTimer.Stop();
        }
    }


    [RelayCommand]
    private async Task PrimaryActionAsync()
    {
        if (_gameId is null || IsBusy)
        {
            return;
        }
        if (IsRunning)
        {
            return;
        }
        Error = null;
        bool installed = await _launcherService.IsGameExeExistsAsync(_gameId);
        if (!installed)
        {
            await LocateGameAsync();
            return;
        }
        IsBusy = true;
        try
        {
            Process? process = await _launcherService.StartGameAsync(_gameId);
            if (process is not null)
            {
                IsRunning = true;
                PrimaryActionText = "Game is running";
                StatusText = "Game is running";
                _gameProcess = process;
                _runningTimer.Start();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start game {gameId}", _gameId.Id);
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }


    [RelayCommand]
    private async Task LocateGameAsync()
    {
        if (_gameId is null)
        {
            return;
        }
        try
        {
            string? folder = await DialogHelper.PickFolderAsync("Select game installation folder", InstallPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                GameLauncherService.ChangeGameInstallPath(_gameId, folder);
                await LoadAsync();
                if (!IsInstalled)
                {
                    Error = "The selected folder does not contain the expected game executable.";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Locate game {gameId}", _gameId.Id);
            Error = ex.Message;
        }
    }


    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            if (Directory.Exists(InstallPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = InstallPath!,
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }


    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }


    [RelayCommand]
    private async Task AutoDetectAsync()
    {
        IsBusy = true;
        try
        {
            var list = await _launcherService.AutoDetectInstalledGamesAsync();
            if (list.Count == 0)
            {
                Error = "No HoYoverse game installation was found in the scanned folders.";
            }
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }


    [RelayCommand]
    private async Task ShowUpdateInfoAsync()
    {
        await DialogHelper.ShowMessageAsync("Game updates",
            "Version information is checked through the official HoYoPlay API. In-place download/install is not part of this Avalonia migration yet; use the official launcher or your Wine/Proton prefix tools to update the game files.");
    }

}
