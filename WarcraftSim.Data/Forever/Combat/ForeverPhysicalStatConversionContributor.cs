using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Characters;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Converts raw Forever physical ratings and Strength into the percentage/value
/// stats consumed by the combat-roll rules. Runs after equipment, talents,
/// and static buffs so all pre-simulation rating sources are included.
/// </summary>
public sealed class ForeverPhysicalStatConversionContributor :
    ICharacterSimulationStatContributor
{
    private static readonly HashSet<string>
        BlockValueFromStrengthClasses =
            new(
                [
                    ForeverCharacterClassKeys.Warrior,
                    ForeverCharacterClassKeys.Paladin,
                    ForeverCharacterClassKeys.Shaman
                ],
                StringComparer.OrdinalIgnoreCase
            );

    public string Key =>
        "forever-physical-stat-conversions";

    public int Order =>
        CharacterSimulationStatContributorOrders.Derived;

    public void Contribute(
        CharacterProfile profile,
        StatCollection effectiveStats)
    {
        ArgumentNullException.ThrowIfNull(
            profile
        );

        ArgumentNullException.ThrowIfNull(
            effectiveStats
        );

        AddRatingConversion(
            effectiveStats,
            ForeverCombatStatKeys.HitRating,
            ForeverCombatStatKeys.HitChancePercent,
            ForeverPhysicalStatConversions.HitRatingPerPercent
        );

        AddRatingConversion(
            effectiveStats,
            ForeverCombatStatKeys.CriticalStrikeRating,
            ForeverCombatStatKeys.AttackCriticalChancePercent,
            ForeverPhysicalStatConversions.CriticalStrikeRatingPerPercent
        );

        AddRatingConversion(
            effectiveStats,
            ForeverCombatStatKeys.DodgeRating,
            ForeverCombatStatKeys.DodgeChancePercent,
            ForeverPhysicalStatConversions.DodgeRatingPerPercent
        );

        AddRatingConversion(
            effectiveStats,
            ForeverCombatStatKeys.ParryRating,
            ForeverCombatStatKeys.ParryChancePercent,
            ForeverPhysicalStatConversions.ParryRatingPerPercent
        );

        AddRatingConversion(
            effectiveStats,
            ForeverCombatStatKeys.BlockRating,
            ForeverCombatStatKeys.BlockChancePercent,
            ForeverPhysicalStatConversions.BlockRatingPerPercent
        );

        if (BlockValueFromStrengthClasses.Contains(
                profile.ClassKey))
        {
            effectiveStats.Add(
                ForeverCombatStatKeys.BlockValue,
                ForeverPhysicalStatConversions.StrengthToBlockValue(
                    effectiveStats.Get(
                        ForeverCombatStatKeys.Strength
                    )
                )
            );
        }
    }

    private static void AddRatingConversion(
        StatCollection stats,
        string ratingStatKey,
        string outputStatKey,
        decimal ratingPerPercent)
    {
        var rating =
            stats.Get(
                ratingStatKey
            );

        if (rating == 0m)
        {
            return;
        }

        stats.Add(
            outputStatKey,
            ForeverPhysicalStatConversions.RatingToPercent(
                rating,
                ratingPerPercent
            )
        );
    }
}
