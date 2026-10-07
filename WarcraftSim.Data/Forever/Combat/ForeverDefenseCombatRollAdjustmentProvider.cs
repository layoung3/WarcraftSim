using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// WoW: Forever defense-skill adjustments derived from the current
/// character-sheet behavior. Defense is compared with the attacker's
/// level-based weapon skill (level * 5). Each point of difference changes
/// miss, dodge, parry, block, and incoming critical chance by 0.04%.
/// </summary>
public sealed class ForeverDefenseCombatRollAdjustmentProvider :
    ICombatRollContextAdjustmentProvider
{
    public const decimal SkillPointsPerLevel =
        5m;

    public const decimal DefensePercentPerSkillPoint =
        0.04m;

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
                effect.TargetDefenseSkillStatKey))
        {
            return
                CombatRollContextAdjustment.None;
        }

        if (!target.Stats.TryGet(
                effect.TargetDefenseSkillStatKey,
                out var defenseSkill))
        {
            throw new InvalidOperationException(
                $"Actor '{target.Key}' is missing defense skill stat '{effect.TargetDefenseSkillStatKey}' required by effect '{effect.Key}'."
            );
        }

        var attackerSkill =
            source.Level *
            SkillPointsPerLevel;

        var defenseDifference =
            defenseSkill -
            attackerSkill;

        var defenseAdjustment =
            defenseDifference *
            DefensePercentPerSkillPoint;

        return new CombatRollContextAdjustment
        {
            // More defender skill increases the attacker's miss chance,
            // represented by reducing its hit chance.
            HitChancePercentDelta =
                -defenseAdjustment,

            DodgeChancePercentDelta =
                defenseAdjustment,

            ParryChancePercentDelta =
                defenseAdjustment,

            BlockChancePercentDelta =
                defenseAdjustment,

            CriticalChancePercentDelta =
                -defenseAdjustment

            // Crushing chance is intentionally not reduced directly here.
            // It is pushed off the single-roll table by miss/dodge/parry/
            // block coverage, matching the classic avoidance-cap model.
        };
    }
}
