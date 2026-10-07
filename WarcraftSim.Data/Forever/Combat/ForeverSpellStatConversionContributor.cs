using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Derives the spell-facing stats currently exposed by the Forever client:
/// shared Hit/Crit ratings for spells, Haste rating, and class/level
/// Intellect-to-spell-critical chance.
///
/// Base spell critical chance, Spirit regeneration, spell miss tables, and
/// resistance behavior are deliberately outside this contributor because
/// those values are not established by the same client stat tables.
/// </summary>
public sealed class ForeverSpellStatConversionContributor :
    ICharacterSimulationStatContributor
{
    public string Key =>
        "forever-spell-stat-conversions";

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

        var classProfile =
            ForeverPrimarySpellStatProfiles.GetRequired(
                profile.ClassKey
            );

        AddRatingConversion(
            effectiveStats,
            ForeverCombatStatKeys.HitRating,
            ForeverCombatStatKeys.SpellHitChancePercent,
            ForeverPhysicalStatConversions.HitRatingPerPercent
        );

        AddRatingConversion(
            effectiveStats,
            ForeverCombatStatKeys.CriticalStrikeRating,
            ForeverCombatStatKeys.SpellCriticalChancePercent,
            ForeverPhysicalStatConversions.CriticalStrikeRatingPerPercent
        );

        AddRatingConversion(
            effectiveStats,
            ForeverCombatStatKeys.HasteRating,
            ForeverCombatStatKeys.HastePercent,
            ForeverSpellStatConversions.HasteRatingPerPercent
        );

        if (!classProfile.UsesMana)
        {
            return;
        }

        var intellect =
            effectiveStats.Get(
                ForeverCombatStatKeys.Intellect
            );

        if (intellect == 0m)
        {
            return;
        }

        var intellectPerSpellCriticalPercent =
            ForeverPrimarySpellStatProfiles
                .GetIntellectPerSpellCriticalPercent(
                    profile.ClassKey,
                    profile.Level
                );

        effectiveStats.Add(
            ForeverCombatStatKeys.SpellCriticalChancePercent,
            intellect /
            intellectPerSpellCriticalPercent
        );
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
            ForeverSpellStatConversions.RatingToPercent(
                rating,
                ratingPerPercent
            )
        );
    }
}
