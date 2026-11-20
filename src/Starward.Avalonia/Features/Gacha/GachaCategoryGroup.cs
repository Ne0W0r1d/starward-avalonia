using System.Collections.Generic;

namespace Starward.Features.Gacha;

public sealed class GachaCategoryGroup
{

    public required GachaTypeStats Stats { get; init; }

    public required IReadOnlyList<GachaLogItemEx> RareItems { get; init; }

    public string Title => Stats.GachaTypeText;

    public int RareCount => RareItems.Count;

    public bool HasRareItems => RareItems.Count > 0;

}
