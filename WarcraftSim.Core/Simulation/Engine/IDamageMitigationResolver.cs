using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

public interface IDamageMitigationResolver
{
    DamageMitigationResult Resolve(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        decimal rawAmount);
}
