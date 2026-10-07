using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Standard Forever contextual combat-roll composition. Individual effects
/// opt into the offensive weapon-skill and/or defensive defense-skill
/// calculations through their configured stat keys. Spell effects receive
/// the separate target-level spell hit adjustment.
/// </summary>
public sealed class ForeverCombatRollContextAdjustmentProvider :
    ICombatRollContextAdjustmentProvider
{
    private readonly CompositeCombatRollContextAdjustmentProvider
        _composite;

    public ForeverCombatRollContextAdjustmentProvider()
    {
        _composite =
            new CompositeCombatRollContextAdjustmentProvider(
                [
                    new ForeverWeaponSkillCombatRollAdjustmentProvider(),
                    new ForeverCreatureLevelCombatRollAdjustmentProvider(),
                    new ForeverDefenseCombatRollAdjustmentProvider(),
                    new ForeverSpellLevelCombatRollAdjustmentProvider()
                ]
            );
    }

    public CombatRollContextAdjustment GetAdjustment(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule)
    {
        return _composite.GetAdjustment(
            context,
            source,
            target,
            ability,
            effect,
            rule
        );
    }
}
