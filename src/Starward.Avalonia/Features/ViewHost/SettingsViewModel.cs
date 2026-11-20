using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Features.Compatibility;
using Starward.Features.Database;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Features.ViewHost;

public partial class SettingsViewModel : ObservableObject
{

    private readonly ILogger<SettingsViewModel> _logger;

    private readonly CompatLayerService _compatLayerService;

    private bool _loadingCompat;

    private bool _loadingGameSettings;

    private bool _initializing = true;

    private GameBiz _currentGameBiz = AppConfig.CurrentGameBiz;


    public SettingsViewModel(ILogger<SettingsViewModel> logger, CompatLayerService compatLayerService)
    {
        _logger = logger;
        _compatLayerService = compatLayerService;
        Languages = new ObservableCollection<LanguageOption>(
            new[] { new LanguageOption(Localization.Get("Avalonia_Settings_FollowSystem"), "") }
                .Concat(Localization.LanguageList.Select(x => new LanguageOption(x.Title, x.LangCode))));
        SelectedLanguage = Languages.FirstOrDefault(x => x.Code == AppConfig.Language) ?? Languages[0];
        AutoDetectGames = AppConfig.AutoDetectInstalledGames;
        EnablePreviewRelease = AppConfig.EnablePreviewRelease;
        ScreenshotFolder = AppConfig.ScreenshotFolder ?? Path.Combine(AppConfig.UserDataFolder!, "Screenshots");
        CompatScanPaths = AppConfig.CompatScanPaths ?? "";
        WinePrefixRoot = AppConfig.WinePrefixRoot ?? Path.Combine(AppConfig.CacheFolder, "prefixes");
        CompatExtraArguments = AppConfig.CompatExtraArguments ?? "";
        CompatDisableMenuBuilder = AppConfig.CompatDisableMenuBuilder;
        CompatEnableMangoHud = AppConfig.CompatEnableMangoHud;
        CompatEnableSdlInput = AppConfig.CompatEnableSdlInput;
        SpeedLimitKBPerSecond = AppConfig.SpeedLimitKBPerSecond;
        _initializing = false;
    }


    public event EventHandler? RestartRequested;


    public ObservableCollection<LanguageOption> Languages { get; }

    public ObservableCollection<CompatLayer> CompatLayers { get; } = new();

    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    [ObservableProperty]
    public partial LanguageOption SelectedLanguage { get; set; }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (_initializing || value is null)
        {
            return;
        }
        AppConfig.SetLanguage(value.Code);
        AppConfig.SaveConfiguration();
        NeedsRestart = true;
    }

    [ObservableProperty]
    public partial bool NeedsRestart { get; set; }

    [ObservableProperty]
    public partial bool AutoDetectGames { get; set; }

    partial void OnAutoDetectGamesChanged(bool value) => AppConfig.AutoDetectInstalledGames = value;

    [ObservableProperty]
    public partial bool EnablePreviewRelease { get; set; }

    partial void OnEnablePreviewReleaseChanged(bool value) => AppConfig.EnablePreviewRelease = value;

    [ObservableProperty]
    public partial string ScreenshotFolder { get; set; }

    [ObservableProperty]
    public partial int SpeedLimitKBPerSecond { get; set; }

    partial void OnSpeedLimitKBPerSecondChanged(int value) => AppConfig.SpeedLimitKBPerSecond = value;

    [ObservableProperty]
    public partial CompatLayer? SelectedCompatLayer { get; set; }

    partial void OnSelectedCompatLayerChanged(CompatLayer? value)
    {
        if (_loadingCompat || value is null)
        {
            return;
        }
        AppConfig.CompatLayerId = value.Id;
        if (GameCompatFollowGlobal)
        {
            _loadingGameSettings = true;
            GameCompatLayer = value;
            _loadingGameSettings = false;
        }
    }

    [ObservableProperty]
    public partial CompatLayer? GameCompatLayer { get; set; }

    partial void OnGameCompatLayerChanged(CompatLayer? value)
    {
        if (_loadingGameSettings || GameCompatFollowGlobal || value is null)
        {
            return;
        }
        AppConfig.SetCompatLayerId(_currentGameBiz, value.Id);
    }

    [ObservableProperty]
    public partial bool GameCompatFollowGlobal { get; set; }

    partial void OnGameCompatFollowGlobalChanged(bool value)
    {
        if (_loadingGameSettings)
        {
            return;
        }
        if (value)
        {
            AppConfig.SetCompatLayerId(_currentGameBiz, null);
            _loadingGameSettings = true;
            GameCompatLayer = SelectedCompatLayer;
            _loadingGameSettings = false;
        }
        else
        {
            GameCompatLayer ??= SelectedCompatLayer;
            if (GameCompatLayer is not null)
            {
                AppConfig.SetCompatLayerId(_currentGameBiz, GameCompatLayer.Id);
            }
        }
    }

    [ObservableProperty]
    public partial bool GameArgsFollowGlobal { get; set; }

    partial void OnGameArgsFollowGlobalChanged(bool value)
    {
        if (_loadingGameSettings)
        {
            return;
        }
        AppConfig.SetCompatArgsFollowGlobal(_currentGameBiz, value);
        if (value)
        {
            _loadingGameSettings = true;
            GameExtraArguments = AppConfig.CompatExtraArguments ?? "";
            _loadingGameSettings = false;
        }
        else
        {
            AppConfig.SetCompatExtraArguments(_currentGameBiz, GameExtraArguments);
        }
    }

    [ObservableProperty]
    public partial string GameExtraArguments { get; set; } = "";

    partial void OnGameExtraArgumentsChanged(string value)
    {
        if (_loadingGameSettings || GameArgsFollowGlobal)
        {
            return;
        }
        AppConfig.SetCompatExtraArguments(_currentGameBiz, value);
    }

    [ObservableProperty]
    public partial bool GameMangoHudFollowGlobal { get; set; }

    partial void OnGameMangoHudFollowGlobalChanged(bool value)
    {
        if (_loadingGameSettings)
        {
            return;
        }
        AppConfig.SetCompatMangoHudFollowGlobal(_currentGameBiz, value);
        if (value)
        {
            _loadingGameSettings = true;
            GameMangoHudEnabled = AppConfig.CompatEnableMangoHud;
            _loadingGameSettings = false;
        }
        else
        {
            AppConfig.SetCompatMangoHud(_currentGameBiz, GameMangoHudEnabled);
        }
    }

    [ObservableProperty]
    public partial bool GameMangoHudEnabled { get; set; }

    partial void OnGameMangoHudEnabledChanged(bool value)
    {
        if (_loadingGameSettings || GameMangoHudFollowGlobal)
        {
            return;
        }
        AppConfig.SetCompatMangoHud(_currentGameBiz, value);
    }

    [ObservableProperty]
    public partial bool GameSdlInputFollowGlobal { get; set; }

    partial void OnGameSdlInputFollowGlobalChanged(bool value)
    {
        if (_loadingGameSettings)
        {
            return;
        }
        AppConfig.SetCompatSdlInputFollowGlobal(_currentGameBiz, value);
        if (value)
        {
            _loadingGameSettings = true;
            GameSdlInputEnabled = AppConfig.CompatEnableSdlInput;
            _loadingGameSettings = false;
        }
        else
        {
            AppConfig.SetCompatSdlInput(_currentGameBiz, GameSdlInputEnabled);
        }
    }

    [ObservableProperty]
    public partial bool GameSdlInputEnabled { get; set; }

    partial void OnGameSdlInputEnabledChanged(bool value)
    {
        if (_loadingGameSettings || GameSdlInputFollowGlobal)
        {
            return;
        }
        AppConfig.SetCompatSdlInput(_currentGameBiz, value);
    }

    [ObservableProperty]
    public partial string CompatScanPaths { get; set; }

    partial void OnCompatScanPathsChanged(string value) => AppConfig.CompatScanPaths = value;

    [ObservableProperty]
    public partial string WinePrefixRoot { get; set; }

    partial void OnWinePrefixRootChanged(string value) => AppConfig.WinePrefixRoot = value;

    [ObservableProperty]
    public partial string CompatExtraArguments { get; set; }

    partial void OnCompatExtraArgumentsChanged(string value)
    {
        AppConfig.CompatExtraArguments = value;
        if (GameArgsFollowGlobal)
        {
            _loadingGameSettings = true;
            GameExtraArguments = value;
            _loadingGameSettings = false;
        }
    }

    [ObservableProperty]
    public partial bool CompatDisableMenuBuilder { get; set; }

    partial void OnCompatDisableMenuBuilderChanged(bool value) => AppConfig.CompatDisableMenuBuilder = value;

    [ObservableProperty]
    public partial bool CompatEnableMangoHud { get; set; }

    partial void OnCompatEnableMangoHudChanged(bool value)
    {
        AppConfig.CompatEnableMangoHud = value;
        if (GameMangoHudFollowGlobal)
        {
            _loadingGameSettings = true;
            GameMangoHudEnabled = value;
            _loadingGameSettings = false;
        }
    }

    [ObservableProperty]
    public partial bool CompatEnableSdlInput { get; set; }

    partial void OnCompatEnableSdlInputChanged(bool value)
    {
        AppConfig.CompatEnableSdlInput = value;
        if (GameSdlInputFollowGlobal)
        {
            _loadingGameSettings = true;
            GameSdlInputEnabled = value;
            _loadingGameSettings = false;
        }
    }

    [ObservableProperty]
    public partial string CompatStatusText { get; set; } = Localization.Get("Avalonia_Settings_Scanning");

    [ObservableProperty]
    public partial string CurrentGameName { get; set; } = "";

    [ObservableProperty]
    public partial string CurrentGamePrefix { get; set; } = "";

    public bool IsLinux => AppConfig.IsLinux;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial string DataFolder { get; set; } = AppConfig.UserDataFolder ?? "";

    public string CacheFolder => AppConfig.CacheFolder;

    public string LogFile => AppConfig.LogFile;

    public string Version => AppConfig.AppVersion;

    public string DotnetVersion => Environment.Version.ToString();


    public void UpdateCurrentGame(GameBiz biz)
    {
        _currentGameBiz = biz;
        CurrentGameName = $"{biz.ToGameName()} · {biz.ToGameServerName()}";
        CurrentGamePrefix = _compatLayerService.GetPrefixPath(biz);
        LoadGameSettings();
        _ = LoadCompatLayersAsync(false);
    }


    public void Activate()
    {
        _ = LoadCompatLayersAsync(false);
    }


    private async Task LoadCompatLayersAsync(bool forceRefresh)
    {
        if (!AppConfig.IsLinux)
        {
            CompatStatusText = Localization.Get("Avalonia_Settings_CompatLinuxOnly");
            return;
        }
        IsBusy = true;
        Error = null;
        try
        {
            var layers = await _compatLayerService.DetectAsync(forceRefresh);
            _loadingCompat = true;
            CompatLayers.Clear();
            foreach (CompatLayer layer in layers)
            {
                CompatLayers.Add(layer);
            }
            string? id = AppConfig.CompatLayerId;
            SelectedCompatLayer = CompatLayers.FirstOrDefault(x => x.MatchesId(id))
                                  ?? CompatLayers.FirstOrDefault(x => x.IsProton)
                                  ?? CompatLayers.FirstOrDefault();
            _loadingCompat = false;
            LoadGameSettings();
            CompatStatusText = CompatLayers.Count == 0
                ? Localization.Get("Avalonia_Settings_NoWineProton")
                : Localization.Format("Avalonia_Settings_LayersFormat",
                    CompatLayers.Count,
                    CompatLayers.Count(x => x.IsProton),
                    CompatLayers.Count(x => !x.IsProton));
            CurrentGamePrefix = _compatLayerService.GetPrefixPath(_currentGameBiz);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan compatibility layers");
            CompatStatusText = Localization.Get("Avalonia_Settings_CompatScanFailed");
            Error = ex.Message;
        }
        finally
        {
            _loadingCompat = false;
            IsBusy = false;
        }
    }


    private void LoadGameSettings()
    {
        _loadingGameSettings = true;
        try
        {
            string? gameLayerId = AppConfig.GetCompatLayerId(_currentGameBiz);
            CompatLayer? gameLayer = gameLayerId is null
                ? null
                : CompatLayers.FirstOrDefault(x => x.MatchesId(gameLayerId));
            if (gameLayerId is not null && CompatLayers.Count > 0
                && (gameLayer is null || SelectedCompatLayer?.MatchesId(gameLayerId) is true))
            {
                // Older builds wrote the global selection into every game. Treat that
                // as "follow global". A stale ID that no longer exists is also cleared.
                AppConfig.SetCompatLayerId(_currentGameBiz, null);
                gameLayerId = null;
                gameLayer = null;
            }
            GameCompatFollowGlobal = string.IsNullOrWhiteSpace(gameLayerId);
            GameCompatLayer = GameCompatFollowGlobal
                ? SelectedCompatLayer
                : gameLayer ?? SelectedCompatLayer;

            GameArgsFollowGlobal = AppConfig.GetCompatArgsFollowGlobal(_currentGameBiz);
            GameExtraArguments = GameArgsFollowGlobal
                ? AppConfig.CompatExtraArguments ?? ""
                : AppConfig.GetCompatExtraArguments(_currentGameBiz) ?? "";

            GameMangoHudFollowGlobal = AppConfig.GetCompatMangoHudFollowGlobal(_currentGameBiz);
            GameMangoHudEnabled = AppConfig.GetEffectiveCompatMangoHud(_currentGameBiz);

            GameSdlInputFollowGlobal = AppConfig.GetCompatSdlInputFollowGlobal(_currentGameBiz);
            GameSdlInputEnabled = AppConfig.GetEffectiveCompatSdlInput(_currentGameBiz);
        }
        finally
        {
            _loadingGameSettings = false;
        }
    }


    [RelayCommand]
    private async Task RefreshCompatAsync()
    {
        await LoadCompatLayersAsync(true);
    }


    [RelayCommand]
    private async Task BrowseScreenshotFolderAsync()
    {
        string? folder = await DialogHelper.PickFolderAsync(Localization.Get("Avalonia_Settings_PickScreenshotTitle"), ScreenshotFolder);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            ScreenshotFolder = folder;
            AppConfig.ScreenshotFolder = folder;
        }
    }


    [RelayCommand]
    private async Task BrowsePrefixRootAsync()
    {
        string? folder = await DialogHelper.PickFolderAsync(Localization.Get("Avalonia_Settings_PickPrefixRootTitle"), WinePrefixRoot);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            WinePrefixRoot = folder;
            AppConfig.WinePrefixRoot = folder;
            CurrentGamePrefix = _compatLayerService.GetPrefixPath(_currentGameBiz);
        }
    }


    [RelayCommand]
    private async Task BrowseGamePrefixAsync()
    {
        string? folder = await DialogHelper.PickFolderAsync(Localization.Get("Avalonia_Settings_PickGamePrefixTitle"), CurrentGamePrefix);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            AppConfig.SetWinePrefix(_currentGameBiz, folder);
            CurrentGamePrefix = folder;
        }
    }


    [RelayCommand]
    private void ResetGamePrefix()
    {
        AppConfig.SetWinePrefix(_currentGameBiz, null);
        CurrentGamePrefix = _compatLayerService.GetPrefixPath(_currentGameBiz);
    }


    [RelayCommand]
    private async Task OpenDataFolderAsync()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            Process.Start(new ProcessStartInfo { FileName = DataFolder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }


    [RelayCommand]
    private async Task ChangeDataFolderAsync()
    {
        string? folder = await DialogHelper.PickFolderAsync(Localization.Get("Avalonia_Settings_PickDataFolderTitle"), DataFolder);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }
        try
        {
            AppConfig.ChangeUserDataFolder(folder);
            DataFolder = folder;
            await DialogHelper.ShowMessageAsync(Localization.Get("Avalonia_Settings_DataFolderChangedTitle"),
                Localization.Get("Avalonia_Settings_DataFolderChangedMessage"));
            NeedsRestart = true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }


    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        string? file = await DialogHelper.SaveFileAsync(Localization.Get("Avalonia_Settings_BackupDatabase"),
            $"StarwardDatabase_{DateTime.Now:yyyyMMdd_HHmmss}.db", "*.db");
        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }
        try
        {
            DatabaseService.BackupDatabase(file);
            await DialogHelper.ShowMessageAsync(Localization.Get("Avalonia_Settings_BackupComplete"), file);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }


    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        bool ok = await DialogHelper.ShowConfirmAsync(Localization.Get("Avalonia_Settings_ResetSettings"),
            Localization.Get("Avalonia_Settings_ResetMessage"));
        if (ok)
        {
            AppConfig.DeleteAllSettings();
            NeedsRestart = true;
        }
    }


    [RelayCommand]
    private void OpenLog()
    {
        try
        {
            if (File.Exists(LogFile))
            {
                Process.Start(new ProcessStartInfo { FileName = LogFile, UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }


    [RelayCommand]
    private async Task RestartAsync()
    {
        bool ok = await DialogHelper.ShowConfirmAsync(Localization.Get("Avalonia_Settings_RestartTitle"),
            Localization.Get("Avalonia_Settings_RestartMessage"));
        if (ok)
        {
            AppConfig.SaveConfiguration();
            RestartRequested?.Invoke(this, EventArgs.Empty);
        }
    }


    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // ignored
        }
    }

}


public sealed class LanguageOption
{

    public string Title { get; }

    public string Code { get; }


    public LanguageOption(string title, string code)
    {
        Title = title;
        Code = code;
    }


    public override string ToString() => Title;

}
