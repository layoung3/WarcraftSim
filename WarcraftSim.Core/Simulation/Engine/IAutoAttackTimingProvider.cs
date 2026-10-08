namespace WarcraftSim.Core.Simulation.Engine;

public interface IAutoAttackTimingProvider
{
    AutoAttackTimingSnapshot Resolve(
        SimulationContext context,
        SimulationActorState source,
        AutoAttackDefinition definition);
}
