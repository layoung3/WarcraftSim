using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Standard Forever contextual combat-roll composition. Individual effects
/// opt into the offensive weapon-skill and/or defensive defense-skill
/// calculations through their configured stat keys. Spell effects receive
/// the separate target-level spell hit adjustment and delivery-aware critical
/// chance modifiers.
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
                    new ForeverWeaponHandCombatRollAdjustmentProvider(),
                    new ForeverCreatureLevelCombatRollAdjustmentProvider(),
                    new ForeverDefenseCombatRollAdjustmentProvider(),
                    new ForeverSpellLevelCombatRollAdjustmentProvider(),
                    new ForeverSpellCriticalDeliveryAdjustmentProvider()
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
        return GetAdjustment(
            context,
            source,
            target,
            ability,
            effect,
            rule,
            CombatEffectDeliveryType.Direct
        );
    }

    public CombatRollContextAdjustment GetAdjustment(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule,
        CombatEffectDeliveryType deliveryType)
    {
        return _composite.GetAdjustment(
            context,
            source,
            target,
            ability,
            effect,
            rule,
            deliveryType
        );
    }
}
