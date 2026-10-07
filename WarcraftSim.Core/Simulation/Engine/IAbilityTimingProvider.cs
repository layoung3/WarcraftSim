using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

public interface IAbilityTimingProvider
{
    AbilityTimingSnapshot Resolve(
        SimulationContext context,
        SimulationActorState source,
        AbilityDefinition ability);
}
