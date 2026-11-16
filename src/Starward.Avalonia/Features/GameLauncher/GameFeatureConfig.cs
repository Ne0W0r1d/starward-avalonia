using Starward.Core;
using Starward.Core.HoYoPlay;
using System.Collections.Generic;

namespace Starward.Features.GameLauncher;

public sealed class GameFeatureConfig
{

    public bool SupportGacha { get; init; }

    public bool SupportScreenshots { get; init; } = true;

    public bool SupportDailyNote { get; init; }


    public static GameFeatureConfig FromGameId(GameId? gameId)
    {
        if (gameId is null)
        {
            return new GameFeatureConfig();
        }
        return gameId.GameBiz.Game switch
        {
            GameBiz.hk4e or GameBiz.hkrpg or GameBiz.nap => new GameFeatureConfig
            {
                SupportGacha = true,
                SupportScreenshots = true,
                SupportDailyNote = true,
            },
            _ => new GameFeatureConfig
            {
                SupportGacha = false,
                SupportScreenshots = true,
                SupportDailyNote = false,
            },
        };
    }

}
