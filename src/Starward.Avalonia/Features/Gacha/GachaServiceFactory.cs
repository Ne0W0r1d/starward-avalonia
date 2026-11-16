using Starward.Core;
using System;

namespace Starward.Features.Gacha;

public sealed class GachaServiceFactory
{

    private readonly GenshinGachaService _genshin;

    private readonly StarRailGachaService _starRail;

    private readonly ZZZGachaService _zzz;


    public GachaServiceFactory(GenshinGachaService genshin, StarRailGachaService starRail, ZZZGachaService zzz)
    {
        _genshin = genshin;
        _starRail = starRail;
        _zzz = zzz;
    }


    public GachaLogService? GetService(GameBiz biz)
    {
        return biz.Game switch
        {
            GameBiz.hk4e => _genshin,
            GameBiz.hkrpg => _starRail,
            GameBiz.nap => _zzz,
            _ => null,
        };
    }

}
