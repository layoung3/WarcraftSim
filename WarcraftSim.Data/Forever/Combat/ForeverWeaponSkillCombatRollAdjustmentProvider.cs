using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// WoW: Forever weapon/attack-skill adjustments observed from the current
/// in-game skill tooltip data. This provider intentionally owns the
/// Forever-specific numbers so WarcraftSim.Core remains ruleset-neutral.
/// </summary>
public sealed class ForeverWeaponSkillCombatRollAdjustmentProvider :
    ICombatRollContextAdjustmentProvider
{
    public const decimal SkillPointsPerLevel =
        5m;

    public const decimal HitDodgeParryPercentPerSkillPoint =
        0.04m;

    public const decimal CriticalPercentPerSkillPoint =
        0.02m;

    public const decimal BaseGlancingChancePercent =
        10m;

    public const decimal GlancingChancePercentPerSkillDeficit =
        2m;

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

        if (string.IsNullOrWhiteSpace(
                effect.AttackSkillStatKey))
        {
            return
                CombatRollContextAdjustment.None;
        }

        if (!source.Stats.TryGet(
                effect.AttackSkillStatKey,
                out var attackSkill))
        {
            throw new InvalidOperationException(
                $"Actor '{source.Key}' is missing attack skill stat '{effect.AttackSkillStatKey}' required by effect '{effect.Key}'."
            );
        }

        var targetBaselineSkill =
            target.Level *
            SkillPointsPerLevel;

        var skillDifference =
            attackSkill -
            targetBaselineSkill;

        var hitDodgeParryAdjustment =
            skillDifference *
            HitDodgeParryPercentPerSkillPoint;

        var criticalAdjustment =
            skillDifference *
            CriticalPercentPerSkillPoint;

        var sourceNaturalSkillCap =
            source.Level *
            SkillPointsPerLevel;

        var skillUsedForGlancingChance =
            Math.Min(
                attackSkill,
                sourceNaturalSkillCap
            );

        var glancingSkillDeficit =
            targetBaselineSkill -
            skillUsedForGlancingChance;

        var glancingChance =
            BaseGlancingChancePercent +
            (
                glancingSkillDeficit *
                GlancingChancePercentPerSkillDeficit
            );

        return new CombatRollContextAdjustment
        {
            HitChancePercentDelta =
                hitDodgeParryAdjustment,

            // More attacker skill reduces the target's dodge/parry;
            // less attacker skill increases those target outcomes.
            DodgeChancePercentDelta =
                -hitDodgeParryAdjustment,

            ParryChancePercentDelta =
                -hitDodgeParryAdjustment,

            CriticalChancePercentDelta =
                criticalAdjustment,

            GlancingChancePercentOverride =
                Math.Clamp(
                    glancingChance,
                    0m,
                    100m
                )
        };
    }
}
