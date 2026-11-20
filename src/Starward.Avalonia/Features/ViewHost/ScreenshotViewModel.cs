using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Starward.Core.HoYoPlay;
using Starward.Features.Screenshot;
using Starward.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Features.ViewHost;

public partial class ScreenshotViewModel : ObservableObject
{

    private readonly ILogger<ScreenshotViewModel> _logger;

    private readonly ScreenshotService _screenshotService;

    private GameId? _gameId;


    public ScreenshotViewModel(ILogger<ScreenshotViewModel> logger, ScreenshotService screenshotService)
    {
        _logger = logger;
        _screenshotService = screenshotService;
    }


    public ObservableCollection<ScreenshotItem> Screenshots { get; } = new();

    [ObservableProperty]
    public partial ScreenshotItem? SelectedScreenshot { get; set; }

    [ObservableProperty]
    public partial Bitmap? Preview { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial string? Error { get; set; }


    partial void OnSelectedScreenshotChanged(ScreenshotItem? value)
    {
        Preview = value is null ? null : ImageHelper.LoadFile(value.FilePath);
    }


    public void UpdateGame(GameId gameId)
    {
        _gameId = gameId;
        _ = RefreshAsync();
    }


    public void Activate()
    {
        if (_gameId is not null)
        {
            _ = RefreshAsync();
        }
    }


    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_gameId is null)
        {
            return;
        }
        IsBusy = true;
        Error = null;
        try
        {
            var list = await _screenshotService.GetScreenshotsAsync(_gameId);
            Screenshots.Clear();
            foreach (var item in list)
            {
                Screenshots.Add(item);
            }
            StatusText = list.Count == 0
                ? Localization.Get("Avalonia_Screenshots_NoneFound")
                : Localization.Format("Avalonia_Screenshots_CountFormat", list.Count);
            SelectedScreenshot = Screenshots.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load screenshots");
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }


    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (_gameId is null)
        {
            return;
        }
        var (backup, game) = await _screenshotService.GetGameScreenshotPathAsync(_gameId);
        string? folder = Directory.Exists(game) ? game : backup;
        if (Directory.Exists(folder))
        {
            OpenPath(folder!);
        }
    }


    [RelayCommand]
    private void OpenSelected()
    {
        if (SelectedScreenshot is not null && File.Exists(SelectedScreenshot.FilePath))
        {
            OpenPath(SelectedScreenshot.FilePath);
        }
    }


    [RelayCommand]
    private async Task SetExternalFolderAsync()
    {
        if (_gameId is null)
        {
            return;
        }
        string? folder = await DialogHelper.PickFolderAsync(Localization.Get("Avalonia_Screenshots_SelectFolderTitle"));
        if (!string.IsNullOrWhiteSpace(folder))
        {
            AppConfig.SetExternalScreenshotFolder(_gameId.GameBiz, folder);
            await RefreshAsync();
        }
    }


    [RelayCommand]
    private async Task ClearExternalFolderAsync()
    {
        if (_gameId is null)
        {
            return;
        }
        AppConfig.SetExternalScreenshotFolder(_gameId.GameBiz, null);
        await RefreshAsync();
    }


    private void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

}
