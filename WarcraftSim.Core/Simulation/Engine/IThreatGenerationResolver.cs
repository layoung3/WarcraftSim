namespace WarcraftSim.Core.Simulation.Engine;

public interface IThreatGenerationResolver
{
    IReadOnlyList<ThreatGenerationContribution> Resolve(
        SimulationContext context,
        CombatEvent combatEvent);
}
