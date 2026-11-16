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

    private bool _initializing = true;

    private GameBiz _currentGameBiz = AppConfig.CurrentGameBiz;


    public SettingsViewModel(ILogger<SettingsViewModel> logger, CompatLayerService compatLayerService)
    {
        _logger = logger;
        _compatLayerService = compatLayerService;
        Languages = new ObservableCollection<LanguageOption>(
            new[] { new LanguageOption("Follow system", "") }
                .Concat(Localization.LanguageList.Select(x => new LanguageOption(x.Title, x.LangCode))));
        SelectedLanguage = Languages.FirstOrDefault(x => x.Code == AppConfig.Language) ?? Languages[0];
        AutoDetectGames = AppConfig.AutoDetectInstalledGames;
        EnablePreviewRelease = AppConfig.EnablePreviewRelease;
        ScreenshotFolder = AppConfig.ScreenshotFolder ?? Path.Combine(AppConfig.UserDataFolder!, "Screenshots");
        CompatScanPaths = AppConfig.CompatScanPaths ?? "";
        WinePrefixRoot = AppConfig.WinePrefixRoot ?? Path.Combine(AppConfig.CacheFolder, "prefixes");
        CompatExtraArguments = AppConfig.CompatExtraArguments ?? "";
        CompatDisableMenuBuilder = AppConfig.CompatDisableMenuBuilder;
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
        AppConfig.SetCompatLayerId(_currentGameBiz, value.Id);
    }

    [ObservableProperty]
    public partial string CompatScanPaths { get; set; }

    partial void OnCompatScanPathsChanged(string value) => AppConfig.CompatScanPaths = value;

    [ObservableProperty]
    public partial string WinePrefixRoot { get; set; }

    partial void OnWinePrefixRootChanged(string value) => AppConfig.WinePrefixRoot = value;

    [ObservableProperty]
    public partial string CompatExtraArguments { get; set; }

    partial void OnCompatExtraArgumentsChanged(string value) => AppConfig.CompatExtraArguments = value;

    [ObservableProperty]
    public partial bool CompatDisableMenuBuilder { get; set; }

    partial void OnCompatDisableMenuBuilderChanged(bool value) => AppConfig.CompatDisableMenuBuilder = value;

    [ObservableProperty]
    public partial string CompatStatusText { get; set; } = "Scanning…";

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
            CompatStatusText = "Compatibility layers are only used on Linux.";
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
            string? id = AppConfig.GetCompatLayerId(_currentGameBiz) ?? AppConfig.CompatLayerId;
            SelectedCompatLayer = CompatLayers.FirstOrDefault(x => x.Id == id)
                                  ?? CompatLayers.FirstOrDefault(x => x.IsProton)
                                  ?? CompatLayers.FirstOrDefault();
            _loadingCompat = false;
            CompatStatusText = CompatLayers.Count == 0
                ? "No Wine or Proton installation was detected."
                : $"{CompatLayers.Count} compatibility layers detected · {CompatLayers.Count(x => x.IsProton)} Proton / {CompatLayers.Count(x => !x.IsProton)} Wine";
            CurrentGamePrefix = _compatLayerService.GetPrefixPath(_currentGameBiz);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan compatibility layers");
            CompatStatusText = "Failed to scan compatibility layers.";
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
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
        string? folder = await DialogHelper.PickFolderAsync("Select screenshot folder", ScreenshotFolder);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            ScreenshotFolder = folder;
            AppConfig.ScreenshotFolder = folder;
        }
    }


    [RelayCommand]
    private async Task BrowsePrefixRootAsync()
    {
        string? folder = await DialogHelper.PickFolderAsync("Select Wine/Proton prefix root", WinePrefixRoot);
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
        string? folder = await DialogHelper.PickFolderAsync("Select prefix folder for the current game", CurrentGamePrefix);
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
        string? folder = await DialogHelper.PickFolderAsync("Select Starward data folder", DataFolder);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }
        try
        {
            AppConfig.ChangeUserDataFolder(folder);
            DataFolder = folder;
            await DialogHelper.ShowMessageAsync("Data folder changed",
                "The data folder was changed. Restart Starward to load the database from the new location.");
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
        string? file = await DialogHelper.SaveFileAsync("Backup database",
            $"StarwardDatabase_{DateTime.Now:yyyyMMdd_HHmmss}.db", "*.db");
        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }
        try
        {
            DatabaseService.BackupDatabase(file);
            await DialogHelper.ShowMessageAsync("Backup complete", file);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }


    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        bool ok = await DialogHelper.ShowConfirmAsync("Reset settings",
            "Reset all settings to their defaults? Game install paths and gacha records are kept.");
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
        bool ok = await DialogHelper.ShowConfirmAsync("Restart Starward", "Restart the application now to apply the changes?");
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
