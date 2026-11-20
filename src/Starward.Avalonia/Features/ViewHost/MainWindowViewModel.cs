using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameLauncher;
using Starward.Features.HoYoPlay;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Starward.Features.ViewHost;

public partial class MainWindowViewModel : ObservableObject
{

    private readonly ILogger<MainWindowViewModel> _logger;

    private readonly HoYoPlayService _hoYoPlayService;

    private readonly LauncherViewModel _launcher;

    private readonly GachaViewModel _gacha;

    private readonly ScreenshotViewModel _screenshots;

    private readonly PlayTimeViewModel _playTime;

    private readonly SettingsViewModel _settings;

    private GameBizIcon? _initialGame;

    public MainWindowViewModel()
    {
        _logger = AppConfig.GetLogger<MainWindowViewModel>();
        _hoYoPlayService = AppConfig.GetService<HoYoPlayService>();
        _launcher = AppConfig.GetService<LauncherViewModel>();
        _gacha = AppConfig.GetService<GachaViewModel>();
        _screenshots = AppConfig.GetService<ScreenshotViewModel>();
        _playTime = AppConfig.GetService<PlayTimeViewModel>();
        _settings = AppConfig.GetService<SettingsViewModel>();
        _settings.RestartRequested += (_, _) => RestartRequested?.Invoke(this, EventArgs.Empty);
        Initialize();
    }


    public MainWindowViewModel(string fatalError)
    {
        _logger = AppConfig.GetLogger<MainWindowViewModel>();
        _hoYoPlayService = AppConfig.GetService<HoYoPlayService>();
        _launcher = AppConfig.GetService<LauncherViewModel>();
        _gacha = AppConfig.GetService<GachaViewModel>();
        _screenshots = AppConfig.GetService<ScreenshotViewModel>();
        _playTime = AppConfig.GetService<PlayTimeViewModel>();
        _settings = AppConfig.GetService<SettingsViewModel>();
        FatalError = fatalError;
    }


    public event EventHandler? RestartRequested;


    public string FatalError { get; } = "";

    public bool HasFatalError => !string.IsNullOrWhiteSpace(FatalError);

    public ObservableCollection<GameBizIcon> Games { get; } = new();

    [ObservableProperty]
    public partial GameBizIcon? CurrentGame { get; set; }

    [ObservableProperty]
    public partial object? CurrentPage { get; set; }

    [ObservableProperty]
    public partial string PageTitle { get; set; } = Localization.Get("MainPage_Launcer");

    [ObservableProperty]
    public partial bool IsLauncherActive { get; set; }

    [ObservableProperty]
    public partial bool IsGachaActive { get; set; }

    [ObservableProperty]
    public partial bool IsScreenshotActive { get; set; }

    [ObservableProperty]
    public partial bool IsPlayTimeActive { get; set; }

    [ObservableProperty]
    public partial bool IsSettingsActive { get; set; }

    [ObservableProperty]
    public partial Bitmap? BackgroundImage { get; set; }

    [ObservableProperty]
    public partial Bitmap? GameLogo { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsGameSelectorOpen { get; set; }

    public bool ShowGacha => CurrentGame is not null && GameFeatureConfig.FromGameId(CurrentGame.GameId).SupportGacha;

    public string CurrentGameName => CurrentGame?.GameName ?? "Starward";

    public string CurrentServerName => CurrentGame?.ServerName ?? "";

    public string CompatHint => AppConfig.IsLinux
        ? "Linux · Wine / Proton"
        : OperatingSystem.IsWindows() ? "Windows" : "macOS";

    public SettingsViewModel SettingsPage => _settings;


    private void Initialize()
    {
        foreach (GameBiz biz in GameBiz.AllGameBizs)
        {
            Games.Add(new GameBizIcon(biz));
        }
        GameBiz currentBiz = AppConfig.CurrentGameBiz;
        _initialGame = Games.FirstOrDefault(x => x.GameBiz == currentBiz)
                       ?? Games.FirstOrDefault(x => GameLauncherService.GetGameInstallPath(x.GameBiz) is not null)
                       ?? Games.FirstOrDefault(x => x.GameBiz.Value == GameBiz.hk4e_global)
                       ?? Games.FirstOrDefault();
        CurrentGame = _initialGame;
        Navigate("Launcher");
        _ = LoadGameInfoAsync(CurrentGame);
    }


    /// <summary>
    /// ComboBox 在绑定初始化时会自动选择第一项，窗口显示后恢复用户上次选择的游戏。
    /// </summary>
    public void RestoreInitialGame()
    {
        if (_initialGame is not null && Games.Contains(_initialGame))
        {
            CurrentGame = _initialGame;
        }
    }


    partial void OnCurrentGameChanged(GameBizIcon? value)
    {
        if (value is null)
        {
            return;
        }
        AppConfig.CurrentGameBiz = value.GameBiz;
        foreach (GameBizIcon game in Games)
        {
            game.IsSelected = ReferenceEquals(game, value);
        }
        OnPropertyChanged(nameof(ShowGacha));
        OnPropertyChanged(nameof(CurrentGameName));
        OnPropertyChanged(nameof(CurrentServerName));
        _launcher.UpdateGame(value.GameId);
        _gacha.UpdateGame(value.GameId);
        _screenshots.UpdateGame(value.GameId);
        _playTime.UpdateGame(value.GameId);
        _settings.UpdateCurrentGame(value.GameBiz);
        _ = LoadGameInfoAsync(value);
        if (CurrentPage == _gacha && !ShowGacha)
        {
            Navigate("Launcher");
        }
    }


    [RelayCommand]
    private void Navigate(string page)
    {
        IsLauncherActive = page is "Launcher";
        IsGachaActive = page is "Gacha";
        IsScreenshotActive = page is "Screenshots";
        IsPlayTimeActive = page is "PlayTime";
        IsSettingsActive = page is "Settings";
        CurrentPage = page switch
        {
            "Gacha" => _gacha,
            "Screenshots" => _screenshots,
            "PlayTime" => _playTime,
            "Settings" => _settings,
            _ => _launcher,
        };
        PageTitle = page switch
        {
            "Gacha" => Localization.Get("Avalonia_Nav_GachaRecords"),
            "Screenshots" => Localization.Get("Avalonia_Nav_Screenshots"),
            "PlayTime" => Localization.Get("LauncherPage_PlayTime"),
            "Settings" => Localization.Get("Avalonia_Nav_Settings"),
            _ => Localization.Get("MainPage_Launcer"),
        };
        switch (page)
        {
            case "Gacha":
                _gacha.Activate();
                break;
            case "Screenshots":
                _screenshots.Activate();
                break;
            case "PlayTime":
                _playTime.Activate();
                break;
            case "Settings":
                _settings.Activate();
                break;
            default:
                _launcher.Activate();
                break;
        }
    }


    [RelayCommand]
    private void SelectGame(GameBizIcon? game)
    {
        if (game is not null)
        {
            CurrentGame = game;
        }
        IsGameSelectorOpen = false;
    }


    [RelayCommand]
    private void ToggleGameSelector()
    {
        IsGameSelectorOpen = !IsGameSelectorOpen;
    }


    [RelayCommand]
    private void CloseGameSelector()
    {
        IsGameSelectorOpen = false;
    }


    private async Task LoadGameInfoAsync(GameBizIcon? game)
    {
        if (game is null)
        {
            return;
        }
        IsBusy = true;
        try
        {
            GameInfo? info = await _hoYoPlayService.GetGameInfoAsync(game.GameId);
            if (info == null || CurrentGame != game)
            {
                return;
            }
            string? backgroundUrl = info.Display?.Background?.Url;
            if (string.IsNullOrWhiteSpace(backgroundUrl))
            {
                GameBackgroundInfo? backgrounds = await _hoYoPlayService.GetGameBackgroundAsync(game.GameId);
                backgroundUrl = backgrounds?.Backgrounds?
                                              .FirstOrDefault(x => x.Type != GameBackground.BACKGROUND_TYPE_VIDEO
                                                                   && !string.IsNullOrWhiteSpace(x.Background?.Url))?
                                              .Background?.Url;
            }
            backgroundUrl ??= info.Display?.Thumbnail?.Url;
            if (!string.IsNullOrWhiteSpace(backgroundUrl))
            {
                BackgroundImage = await ImageHelper.LoadRemoteAsync(backgroundUrl);
            }
            if (!string.IsNullOrWhiteSpace(info.Display?.Logo?.Url))
            {
                GameLogo = await ImageHelper.LoadRemoteAsync(info.Display.Logo.Url);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Load game info {biz}", game.GameBiz);
        }
        finally
        {
            IsBusy = false;
        }
    }

}
