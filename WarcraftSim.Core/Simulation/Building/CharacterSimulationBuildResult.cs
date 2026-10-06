using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationBuildResult
{
    public required CharacterSimulationClassDefinition ClassDefinition
    {
        get;
        init;
    }

    public required SimulationActorBuildDefinition BuildDefinition
    {
        get;
        init;
    }

    public required SimulationActorState Actor
    {
        get;
        init;
    }

    public CharacterSimulationRuntimeCalculationResult?
        RuntimeCalculation
    {
        get;
        init;
    }
}
