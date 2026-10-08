using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

public interface ICombatRollResolver
{
    CombatRollResult Resolve(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect);

    /// <summary>
    /// Resolves a combat roll with the delivery classification for this
    /// occurrence. Existing resolvers remain source-compatible and treat
    /// delivery-specific calls the same as direct calls unless they opt in.
    /// </summary>
    CombatRollResult Resolve(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        CombatEffectDeliveryType deliveryType)
    {
        return Resolve(
            context,
            source,
            target,
            ability,
            effect
        );
    }
}
