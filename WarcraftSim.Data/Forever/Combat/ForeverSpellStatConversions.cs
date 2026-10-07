namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Spell-facing rating conversions exposed by the current WoW: Forever
/// client. Hit and critical rating use the same raw ratings already consumed
/// by physical attacks. Haste rating is also shared by weapon and cast speed;
/// CP82 only establishes the derived stat and does not yet alter timing.
/// </summary>
public static class ForeverSpellStatConversions
{
    public const decimal HasteRatingPerPercent =
        10m;

    public static decimal RatingToPercent(
        decimal rating,
        decimal ratingPerPercent)
    {
        if (ratingPerPercent <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ratingPerPercent),
                ratingPerPercent,
                "Rating per percent must be greater than zero."
            );
        }

        return
            rating /
            ratingPerPercent;
    }
}
