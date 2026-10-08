namespace WarcraftSim.Core.Simulation.Engine;

public interface INextSwingAdditionalTargetResolver
{
    IReadOnlyList<SimulationActorState> Resolve(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState primaryTarget,
        NextSwingReplacementDefinition replacement);
}
