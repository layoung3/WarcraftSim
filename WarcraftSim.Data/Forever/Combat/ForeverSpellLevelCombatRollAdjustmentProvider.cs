using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Applies the current provisional spell miss progression for targets above
/// the caster's level. Forever exposes the equal-level 4% and raid-boss 17%
/// requirements, but not the complete server-side level formula. Until that
/// is measured directly, the intermediate progression follows Classic:
/// 4%, 5%, 6%, 17% miss against targets 0, 1, 2, and 3 levels higher.
/// Beyond +3 the same Classic +11 percentage-point progression is retained.
/// </summary>
public sealed class ForeverSpellLevelCombatRollAdjustmentProvider :
    ICombatRollContextAdjustmentProvider
{
    public const decimal MissPenaltyPerHigherLevelThroughTwo =
        1m;

    public const decimal MissPenaltyPerHigherLevelAfterTwo =
        11m;

    public CombatRollContextAdjustment GetAdjustment(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            source
        );

        ArgumentNullException.ThrowIfNull(
            target
        );

        ArgumentNullException.ThrowIfNull(
            ability
        );

        ArgumentNullException.ThrowIfNull(
            effect
        );

        ArgumentNullException.ThrowIfNull(
            rule
        );

        if (!string.Equals(
                effect.ResolutionType,
                ForeverCombatResolutionTypes.PlayerSpell,
                StringComparison.OrdinalIgnoreCase))
        {
            return CombatRollContextAdjustment.None;
        }

        var higherTargetLevels =
            Math.Max(
                0,
                target.Level - source.Level
            );

        if (higherTargetLevels == 0)
        {
            return CombatRollContextAdjustment.None;
        }

        var firstTwoLevels =
            Math.Min(
                2,
                higherTargetLevels
            );

        var levelsAfterTwo =
            Math.Max(
                0,
                higherTargetLevels - 2
            );

        var hitPenalty =
            (firstTwoLevels *
                MissPenaltyPerHigherLevelThroughTwo) +
            (levelsAfterTwo *
                MissPenaltyPerHigherLevelAfterTwo);

        return new CombatRollContextAdjustment
        {
            HitChancePercentDelta =
                -hitPenalty
        };
    }
}
