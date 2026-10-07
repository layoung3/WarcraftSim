using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// WoW: Forever defense-skill adjustments derived from the current
/// character-sheet behavior. Each point of Defense above the target
/// character's natural level cap (level * 5) changes miss, dodge, parry,
/// block, and incoming critical chance by 0.04%. Creature level advantage
/// is composed separately by the creature-level provider.
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

        var naturalDefenseSkill =
            target.Level *
            SkillPointsPerLevel;

        var bonusDefense =
            defenseSkill -
            naturalDefenseSkill;

        var defenseAdjustment =
            bonusDefense *
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
