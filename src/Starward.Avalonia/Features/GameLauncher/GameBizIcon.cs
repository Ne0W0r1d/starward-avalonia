using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Helpers;
using System;

namespace Starward.Features.GameLauncher;

public partial class GameBizIcon : ObservableObject
{

    public GameId GameId { get; }

    public GameBiz GameBiz => GameId.GameBiz;

    public string GameName { get; }

    public string ServerName { get; }

    public Bitmap? GameIcon { get; }

    public Bitmap? ServerIcon { get; }

    public string IconKey { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }


    public GameBizIcon(GameBiz biz)
    {
        GameId = GameId.FromGameBiz(biz) ?? throw new ArgumentOutOfRangeException(nameof(biz));
        GameName = biz.ToGameName();
        ServerName = biz.ToGameServerName();
        IconKey = biz.Game switch
        {
            GameBiz.bh3 => "Assets/Image/icon_bh3.jpg",
            GameBiz.hk4e => "Assets/Image/icon_ys.jpg",
            GameBiz.hkrpg => "Assets/Image/icon_sr.jpg",
            GameBiz.nap => "Assets/Image/icon_zzz.jpg",
            _ => "Assets/Image/Transparent.png",
        };
        GameIcon = ImageHelper.LoadAsset(IconKey);
        string serverIcon = biz.Server switch
        {
            "cn" => "Assets/Image/gameicon_hyperion.png",
            "global" => "Assets/Image/gameicon_hoyolab.png",
            "bilibili" => "Assets/Image/gameicon_bilibili.png",
            _ => "Assets/Image/Transparent.png",
        };
        ServerIcon = ImageHelper.LoadAsset(serverIcon);
    }


    public string DisplayName => $"{GameName} · {ServerName}";

}
