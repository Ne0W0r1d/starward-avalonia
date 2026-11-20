using Avalonia.Media.Imaging;
using Starward.Core;
using Starward.Helpers;
using System;

namespace Starward.Features.PlayTime;

public sealed class PlayTimeSummary
{

    public required GameBiz GameBiz { get; init; }

    public required string GameName { get; init; }

    public required string ServerName { get; init; }

    public required TimeSpan Total { get; init; }

    public required DateTimeOffset? LastPlayed { get; init; }

    public Bitmap? Icon { get; init; }

    public string TotalText => Format(Total);

    public string LastPlayedText => LastPlayed is null
        ? Localization.Get("Avalonia_PlayTime_NeverPlayed")
        : Localization.Format("Avalonia_PlayTime_LastPlayedFormat", LastPlayed);

    public string DisplayName => $"{GameName} · {ServerName}";


    public static string Format(TimeSpan time)
    {
        if (time.TotalHours >= 24)
        {
            return Localization.Format("Avalonia_PlayTime_DaysFormat", (int)time.TotalDays, time.Hours, time.Minutes, time.Seconds);
        }
        return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
    }


    public static PlayTimeSummary Create(GameBiz biz, TimeSpan total, DateTimeOffset? last)
    {
        return new PlayTimeSummary
        {
            GameBiz = biz,
            GameName = biz.ToGameName(),
            ServerName = biz.ToGameServerName(),
            Total = total,
            LastPlayed = last,
            Icon = ImageHelper.LoadAsset(biz.Game switch
            {
                GameBiz.bh3 => "Assets/Image/icon_bh3.jpg",
                GameBiz.hk4e => "Assets/Image/icon_ys.jpg",
                GameBiz.hkrpg => "Assets/Image/icon_sr.jpg",
                GameBiz.nap => "Assets/Image/icon_zzz.jpg",
                _ => "Assets/Image/Transparent.png",
            }),
        };
    }

}


public sealed class PlayTimeSession
{

    public DateTime Start { get; init; }

    public TimeSpan Duration { get; init; }

    public bool Interrupted { get; init; }

    public string StartText => Start.ToString("yyyy-MM-dd HH:mm");

    public string DurationText => PlayTimeSummary.Format(Duration);

    public string StateText => Localization.Get(Interrupted ? "Avalonia_PlayTime_Interrupted" : "Avalonia_PlayTime_Completed");

}
