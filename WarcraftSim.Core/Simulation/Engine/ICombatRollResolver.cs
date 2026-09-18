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
}
