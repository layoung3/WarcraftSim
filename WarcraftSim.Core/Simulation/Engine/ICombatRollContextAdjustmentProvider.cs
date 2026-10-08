using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;

namespace WarcraftSim.Core.Simulation.Engine;

public interface ICombatRollContextAdjustmentProvider
{
    CombatRollContextAdjustment GetAdjustment(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule);

    /// <summary>
    /// Delivery-aware adjustment hook. Providers that do not care whether an
    /// effect is direct, periodic, or a channel tick can keep implementing the
    /// original overload.
    /// </summary>
    CombatRollContextAdjustment GetAdjustment(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule,
        CombatEffectDeliveryType deliveryType)
    {
        return GetAdjustment(
            context,
            source,
            target,
            ability,
            effect,
            rule
        );
    }
}
