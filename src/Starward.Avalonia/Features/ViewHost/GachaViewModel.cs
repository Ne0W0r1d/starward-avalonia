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

    private readonly GachaCacheService _cacheService;

    private GameId? _gameId;

    private GachaLogService? _service;

    private CancellationTokenSource? _cts;

    private int _cacheScanVersion;

    private GachaCacheResult? _lastCacheResult;


    public GachaViewModel(ILogger<GachaViewModel> logger, GachaServiceFactory factory, GachaCacheService cacheService)
    {
        _logger = logger;
        _factory = factory;
        _cacheService = cacheService;
    }


    public ObservableCollection<long> Uids { get; } = new();

    public ObservableCollection<GachaTypeStats> Stats { get; } = new();

    public ObservableCollection<GachaCategoryGroup> Categories { get; } = new();

    [ObservableProperty]
    public partial long? SelectedUid { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = Localization.Get("Avalonia_Nav_GachaRecords");

    [ObservableProperty]
    public partial string? ProgressText { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasData { get; set; }

    [ObservableProperty]
    public partial string EmptyText { get; set; } = Localization.Get("Avalonia_Gacha_EmptyText");

    [ObservableProperty]
    public partial string? CacheStatusText { get; set; }

    [ObservableProperty]
    public partial bool HasGameCache { get; set; }


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
        Title = _service is null ? Localization.Get("Avalonia_Nav_GachaRecords") : GachaLogService.GetGachaLogText(gameId.GameBiz);
        if (_service is null)
        {
            Uids.Clear();
            Stats.Clear();
            Categories.Clear();
            HasData = false;
            HasGameCache = false;
            CacheStatusText = null;
            EmptyText = Localization.Get("Avalonia_Gacha_Unsupported");
            return;
        }
        LoadUids();
        _ = RefreshCacheStatusAsync();
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
            EmptyText = Localization.Get("Avalonia_Gacha_EmptyText");
            if (SelectedUid is null)
            {
                Stats.Clear();
                Categories.Clear();
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
        Categories.Clear();
        if (_service is null || SelectedUid is not > 0)
        {
            return;
        }
        try
        {
            (List<GachaTypeStats> stats, _) = _service.GetGachaTypeStats(SelectedUid.Value);
            foreach (GachaTypeStats item in stats)
            {
                Stats.Add(item);
                Categories.Add(new GachaCategoryGroup
                {
                    Stats = item,
                    RareItems = GetRareItems(item),
                });
            }
            HasData = Stats.Count > 0;
            EmptyText = HasData ? "" : Localization.Get("Avalonia_Gacha_NoRecordsForUid");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load gacha stats");
            Error = ex.Message;
        }
    }


    private static IReadOnlyList<GachaLogItemEx> GetRareItems(GachaTypeStats stats)
    {
        return stats.List_5.Concat(stats.List_4)
                    .Where(x => x.Id > 0)
                    .GroupBy(x => x.ItemId)
                    .Select(group =>
                    {
                        GachaLogItemEx item = group.OrderByDescending(x => x.Time).First();
                        item.ItemCount = group.Count();
                        return item;
                    })
                    .OrderByDescending(x => x.RankType)
                    .ThenByDescending(x => x.Time)
                    .ToList();
    }


    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_service is null || _gameId is null || IsBusy)
        {
            return;
        }
        Error = null;
        GachaCacheResult? cache = await FindGameCacheAsync(force: true);
        string? url = cache?.Url;
        url ??= SelectedUid is > 0 ? _service.GetGachaLogUrlByUid(SelectedUid.Value) : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = await DialogHelper.ShowInputAsync(Localization.Get("Avalonia_Gacha_UrlDialogTitle"),
                Localization.Get("Avalonia_Gacha_UrlDialogMessage"),
                "");
        }
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }
        await FetchAsync(url, false);
    }


    [RelayCommand]
    private async Task ImportFromGameCacheAsync()
    {
        if (_service is null || IsBusy)
        {
            return;
        }
        GachaCacheResult? cache = await FindGameCacheAsync(force: true);
        if (cache is null)
        {
            await DialogHelper.ShowMessageAsync(Localization.Get("Avalonia_Gacha_NoLinkTitle"),
                Localization.Get("Avalonia_Gacha_NoLinkMessage"));
            return;
        }
        await FetchAsync(cache.Url, false);
    }


    [RelayCommand]
    private async Task ImportUrlAsync()
    {
        if (_service is null || IsBusy)
        {
            return;
        }
        string? url = await DialogHelper.ShowInputAsync(Localization.Get("Avalonia_Gacha_ImportUrlTitle"),
            Localization.Get("Avalonia_Gacha_ImportUrlMessage"));
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
        GachaCacheResult? cache = await FindGameCacheAsync(force: true);
        string? url = cache?.Url
                      ?? await DialogHelper.ShowInputAsync(Localization.Get("Avalonia_Gacha_ImportAllTitle"),
                          Localization.Get("Avalonia_Gacha_ImportAllMessage"));
        if (!string.IsNullOrWhiteSpace(url))
        {
            await FetchAsync(url.Trim(), true);
        }
    }


    private async Task<GachaCacheResult?> FindGameCacheAsync(bool force)
    {
        if (_gameId is null)
        {
            return null;
        }
        if (!force && _lastCacheResult is not null)
        {
            return _lastCacheResult;
        }
        int version = ++_cacheScanVersion;
        CacheStatusText = Localization.Get("Avalonia_Gacha_ScanningCache");
        GachaCacheResult? result = await Task.Run(() => _cacheService.Find(_gameId.GameBiz));
        if (version != _cacheScanVersion)
        {
            return result;
        }
        _lastCacheResult = result;
        HasGameCache = result is not null;
        CacheStatusText = result is null
            ? Localization.Get("Avalonia_Gacha_NoCacheLink")
            : Localization.Format("Avalonia_Gacha_CacheReadyFormat", result.LastWriteTime);
        return result;
    }


    private async Task RefreshCacheStatusAsync()
    {
        _lastCacheResult = null;
        try
        {
            await FindGameCacheAsync(force: true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to scan the game web cache");
            HasGameCache = false;
            CacheStatusText = Localization.Get("Avalonia_Gacha_CacheReadFailed");
        }
    }


    private async Task FetchAsync(string url, bool all)
    {
        if (_service is null)
        {
            return;
        }
        url = GachaLogClient.NormalizeGachaUrl(url);
        IsBusy = true;
        Error = null;
        ProgressText = Localization.Get("Avalonia_Gacha_Starting");
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            Progress<string> progress = new(x => ProgressText = x);
            long uid = await _service.GetGachaLogAsync(url, all, AppConfig.GachaLanguage, progress, _cts.Token);
            ProgressText = Localization.Get(uid > 0 ? "Avalonia_Gacha_Done" : "Avalonia_Gacha_NoRecordsForUrl");
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
            ProgressText = Localization.Get("Avalonia_Gacha_Cancelled");
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
        string? file = await DialogHelper.SaveFileAsync(Localization.Get("Avalonia_Gacha_ExportTitle"),
            $"StarwardGacha_{SelectedUid}_{DateTime.Now:yyyyMMdd_HHmmss}.json", "*.json");
        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }
        try
        {
            await _service.ExportGachaLogAsync(SelectedUid.Value, file, "json");
            ProgressText = Localization.Format("Avalonia_Gacha_ExportedFormat", file);
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
        string? file = await DialogHelper.PickFileAsync(Localization.Get("Avalonia_Gacha_ImportFileTitle"), null, "*.json");
        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }
        try
        {
            long uid = _service.ImportGachaLog(file);
            if (uid > 0)
            {
                ProgressText = Localization.Format("Avalonia_Gacha_ImportedUidFormat", uid);
                LoadUids();
                SelectedUid = uid;
            }
            else
            {
                Error = Localization.Get("Avalonia_Gacha_InvalidFile");
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
        bool ok = await DialogHelper.ShowConfirmAsync(Localization.Get("Avalonia_Gacha_DeleteTitle"),
            Localization.Format("Avalonia_Gacha_DeleteMessageFormat", SelectedUid));
        if (ok)
        {
            _service.DeleteUid(SelectedUid.Value);
            LoadUids();
        }
    }

}
