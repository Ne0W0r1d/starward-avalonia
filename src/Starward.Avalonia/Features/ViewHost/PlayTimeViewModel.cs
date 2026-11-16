using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.PlayTime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Features.ViewHost;

public partial class PlayTimeViewModel : ObservableObject
{

    private readonly ILogger<PlayTimeViewModel> _logger;

    private readonly PlayTimeStatsService _statsService;

    private GameId? _gameId;


    public PlayTimeViewModel(ILogger<PlayTimeViewModel> logger, PlayTimeStatsService statsService)
    {
        _logger = logger;
        _statsService = statsService;
    }


    public ObservableCollection<PlayTimeSummary> Totals { get; } = new();

    public ObservableCollection<PlayTimeSession> Sessions { get; } = new();

    [ObservableProperty]
    public partial string CurrentTotalText { get; set; } = "00:00:00";

    [ObservableProperty]
    public partial string CurrentGameName { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }


    public void UpdateGame(GameId gameId)
    {
        _gameId = gameId;
        CurrentGameName = $"{gameId.GameBiz.ToGameName()} · {gameId.GameBiz.ToGameServerName()}";
        _ = RefreshAsync();
    }


    public void Activate()
    {
        _ = RefreshAsync();
    }


    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await Task.Run(() => _statsService.ConvertItemToStats());
            Totals.Clear();
            foreach (GameBiz biz in GameBiz.AllGameBizs)
            {
                TimeSpan total = _statsService.GetPlayTimeTotal(biz);
                if (total <= TimeSpan.Zero)
                {
                    continue;
                }
                DateTimeOffset? last = null;
                try
                {
                    List<PlayTimeStats> sessions = _statsService.GetPlayTimeInRange(biz, DateTimeOffset.UnixEpoch, DateTimeOffset.Now);
                    last = sessions.Count > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(sessions.Max(x => x.EndTime)) : null;
                }
                catch
                {
                    // ignored
                }
                Totals.Add(PlayTimeSummary.Create(biz, total, last));
            }
            Sessions.Clear();
            if (_gameId is not null)
            {
                List<PlayTimeStats> list = _statsService.GetPlayTimeInRange(_gameId.GameBiz, DateTimeOffset.Now.AddDays(-60), DateTimeOffset.Now);
                foreach (PlayTimeStats item in list.OrderByDescending(x => x.StartTime).Take(30))
                {
                    Sessions.Add(new PlayTimeSession
                    {
                        Start = DateTimeOffset.FromUnixTimeMilliseconds(item.StartTime).LocalDateTime,
                        Duration = TimeSpan.FromMilliseconds(Math.Max(0, item.EndTime - item.StartTime)),
                        Interrupted = item.Interruption,
                    });
                }
                CurrentTotalText = PlayTimeSummary.Format(_statsService.GetPlayTimeTotal(_gameId.GameBiz));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load play time");
        }
        finally
        {
            IsBusy = false;
        }
    }

}
