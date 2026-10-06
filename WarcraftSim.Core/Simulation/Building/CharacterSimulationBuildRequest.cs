using WarcraftSim.Core.Characters;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationBuildRequest
{
    public required CharacterProfile Profile { get; init; }

    public required CharacterSimulationMappingOptions Options { get; init; }
}
