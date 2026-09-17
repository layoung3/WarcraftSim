namespace WarcraftSim.Core.Simulation.Engine;

public interface ICombatEventProcessor
{
    void Process(
        SimulationContext context,
        CombatEvent combatEvent);
}