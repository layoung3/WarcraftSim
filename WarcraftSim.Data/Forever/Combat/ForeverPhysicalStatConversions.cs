namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Physical combat stat conversions exposed by the current WoW: Forever
/// character-sheet/client data. Rating conversions are flat at every level.
/// </summary>
public static class ForeverPhysicalStatConversions
{
    public const decimal HitRatingPerPercent =
        10m;

    public const decimal CriticalStrikeRatingPerPercent =
        14m;

    public const decimal DodgeRatingPerPercent =
        12m;

    public const decimal ParryRatingPerPercent =
        15m;

    public const decimal BlockRatingPerPercent =
        5m;

    public const decimal StrengthPerBlockValue =
        20m;

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

    public static decimal StrengthToBlockValue(
        decimal strength)
    {
        return
            strength /
            StrengthPerBlockValue;
    }
}
