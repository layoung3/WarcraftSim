using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Applies the natural level-based attack-skill advantage for creature
/// melee auto-attacks. This is intentionally separate from bonus Defense:
/// the target's bonus Defense is handled by the Defense provider.
/// </summary>
public sealed class ForeverCreatureLevelCombatRollAdjustmentProvider :
    ICombatRollContextAdjustmentProvider
{
    public const decimal SkillPointsPerLevel =
        5m;

    public const decimal PercentPerSkillPoint =
        0.04m;

    public CombatRollContextAdjustment GetAdjustment(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule)
    {
        if (!string.Equals(
                effect.ResolutionType,
                ForeverCombatResolutionTypes.CreatureMeleeAuto,
                StringComparison.OrdinalIgnoreCase))
        {
            return
                CombatRollContextAdjustment.None;
        }

        var skillDifference =
            (source.Level - target.Level) *
            SkillPointsPerLevel;

        var adjustment =
            skillDifference *
            PercentPerSkillPoint;

        if (adjustment == 0m)
        {
            return
                CombatRollContextAdjustment.None;
        }

        return new CombatRollContextAdjustment
        {
            HitChancePercentDelta =
                adjustment,

            DodgeChancePercentDelta =
                -adjustment,

            ParryChancePercentDelta =
                -adjustment,

            BlockChancePercentDelta =
                -adjustment,

            CriticalChancePercentDelta =
                adjustment
        };
    }
}
