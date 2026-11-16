using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.Gacha;
using Starward.Core.HoYoPlay;
using Starward.Features.Gacha;
using Starward.Features.GameLauncher;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.ViewHost;

public partial class GachaViewModel : ObservableObject
{

    private readonly ILogger<GachaViewModel> _logger;

    private readonly GachaServiceFactory _factory;

    private GameId? _gameId;

    private GachaLogService? _service;

    private CancellationTokenSource? _cts;


    public GachaViewModel(ILogger<GachaViewModel> logger, GachaServiceFactory factory)
    {
        _logger = logger;
        _factory = factory;
    }


    public ObservableCollection<long> Uids { get; } = new();

    public ObservableCollection<GachaTypeStats> Stats { get; } = new();

    public ObservableCollection<GachaLogItemEx> RecentFiveStars { get; } = new();

    [ObservableProperty]
    public partial long? SelectedUid { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "Gacha records";

    [ObservableProperty]
    public partial string? ProgressText { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasData { get; set; }

    [ObservableProperty]
    public partial string EmptyText { get; set; } = "Import a gacha log URL to get started.";


    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));


    partial void OnSelectedUidChanged(long? value)
    {
        if (_gameId is not null && value is > 0)
        {
            AppConfig.SetLastUidInGachaLogPage(_gameId.GameBiz.Game, value.Value);
        }
        LoadStats();
    }


    public void UpdateGame(GameId gameId)
    {
        _gameId = gameId;
        _service = _factory.GetService(gameId.GameBiz);
        Title = _service is null ? "Gacha records" : GachaLogService.GetGachaLogText(gameId.GameBiz);
        if (_service is null)
        {
            Uids.Clear();
            Stats.Clear();
            RecentFiveStars.Clear();
            HasData = false;
            EmptyText = "Gacha records are not supported for this game.";
            return;
        }
        LoadUids();
    }


    public void Activate()
    {
        if (_gameId is not null)
        {
            UpdateGame(_gameId);
        }
    }


    private void LoadUids()
    {
        if (_service is null)
        {
            return;
        }
        try
        {
            Uids.Clear();
            foreach (long uid in _service.GetUids().OrderByDescending(x => x))
            {
                Uids.Add(uid);
            }
            long last = _gameId is null ? 0 : AppConfig.GetLastUidInGachaLogPage(_gameId.GameBiz.Game);
            SelectedUid = Uids.Contains(last) ? last : Uids.FirstOrDefault();
            HasData = Uids.Count > 0;
            EmptyText = "Import a gacha log URL to get started.";
            if (SelectedUid is null)
            {
                Stats.Clear();
                RecentFiveStars.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load gacha uids");
            Error = ex.Message;
        }
    }


    private void LoadStats()
    {
        Stats.Clear();
        RecentFiveStars.Clear();
        if (_service is null || SelectedUid is not > 0)
        {
            return;
        }
        try
        {
            (List<GachaTypeStats> stats, List<GachaLogItemEx> items) = _service.GetGachaTypeStats(SelectedUid.Value);
            foreach (GachaTypeStats item in stats)
            {
                Stats.Add(item);
            }
            foreach (GachaLogItemEx item in items.Where(x => x.RankType is 5 or 4).Take(60))
            {
                RecentFiveStars.Add(item);
            }
            HasData = Stats.Count > 0;
            EmptyText = HasData ? "" : "No records for this UID yet.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load gacha stats");
            Error = ex.Message;
        }
    }


    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_service is null || _gameId is null || IsBusy)
        {
            return;
        }
        Error = null;
        string? url = SelectedUid is > 0 ? _service.GetGachaLogUrlByUid(SelectedUid.Value) : null;
        url ??= TryGetWebCacheUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            url = await DialogHelper.ShowInputAsync("Gacha log URL",
                "Paste the full gacha history URL copied from the game's history page (the URL contains authkey).",
                "");
        }
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }
        await FetchAsync(url, false);
    }


    [RelayCommand]
    private async Task ImportUrlAsync()
    {
        if (_service is null || IsBusy)
        {
            return;
        }
        string? url = await DialogHelper.ShowInputAsync("Import gacha log URL",
            "Paste the full gacha history URL. Existing records are kept and only newer records are fetched.");
        if (!string.IsNullOrWhiteSpace(url))
        {
            await FetchAsync(url.Trim(), false);
        }
    }


    [RelayCommand]
    private async Task ImportAllAsync()
    {
        if (_service is null || IsBusy)
        {
            return;
        }
        string? url = TryGetWebCacheUrl()
                      ?? await DialogHelper.ShowInputAsync("Import all gacha records",
                          "Paste the full gacha history URL. All pages will be fetched again.");
        if (!string.IsNullOrWhiteSpace(url))
        {
            await FetchAsync(url.Trim(), true);
        }
    }


    private string? TryGetWebCacheUrl()
    {
        try
        {
            if (_gameId is null)
            {
                return null;
            }
            string? installPath = GameLauncherService.GetGameInstallPath(_gameId);
            if (string.IsNullOrWhiteSpace(installPath))
            {
                return null;
            }
            return GachaLogClient.GetGachaUrlFromWebCache(_gameId.GameBiz, installPath);
        }
        catch
        {
            return null;
        }
    }


    private async Task FetchAsync(string url, bool all)
    {
        if (_service is null)
        {
            return;
        }
        IsBusy = true;
        Error = null;
        ProgressText = "Starting…";
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            Progress<string> progress = new(x => ProgressText = x);
            long uid = await _service.GetGachaLogAsync(url, all, AppConfig.GachaLanguage, progress, _cts.Token);
            ProgressText = uid > 0 ? "Done" : "No gacha records found for this URL.";
            LoadUids();
            if (uid > 0)
            {
                SelectedUid = uid;
                if (!Uids.Contains(uid))
                {
                    Uids.Insert(0, uid);
                }
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Cancelled";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fetch gacha log");
            Error = ex.Message;
            ProgressText = null;
        }
        finally
        {
            IsBusy = false;
        }
    }


    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }


    [RelayCommand]
    private async Task ExportAsync()
    {
        if (_service is null || SelectedUid is not > 0)
        {
            return;
        }
        string? file = await DialogHelper.SaveFileAsync("Export gacha records",
            $"StarwardGacha_{SelectedUid}_{DateTime.Now:yyyyMMdd_HHmmss}.json", "*.json");
        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }
        try
        {
            await _service.ExportGachaLogAsync(SelectedUid.Value, file, "json");
            ProgressText = $"Exported to {file}";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }


    [RelayCommand]
    private async Task ImportFileAsync()
    {
        if (_service is null)
        {
            return;
        }
        string? file = await DialogHelper.PickFileAsync("Import gacha records", null, "*.json");
        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }
        try
        {
            long uid = _service.ImportGachaLog(file);
            if (uid > 0)
            {
                ProgressText = $"Imported UID {uid}";
                LoadUids();
                SelectedUid = uid;
            }
            else
            {
                Error = "The file could not be parsed as a Starward/UIGF gacha log.";
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }


    [RelayCommand]
    private async Task DeleteUidAsync()
    {
        if (_service is null || SelectedUid is not > 0)
        {
            return;
        }
        bool ok = await DialogHelper.ShowConfirmAsync("Delete gacha records",
            $"Delete all local gacha records of UID {SelectedUid}? This cannot be undone.");
        if (ok)
        {
            _service.DeleteUid(SelectedUid.Value);
            LoadUids();
        }
    }

}
