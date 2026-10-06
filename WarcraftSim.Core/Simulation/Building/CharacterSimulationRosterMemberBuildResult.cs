using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationRosterMemberBuildResult
{
    public required CharacterProfile Profile { get; init; }

    public required CharacterSimulationBuildResult Character { get; init; }

    public SimulationActorState Actor =>
        Character.Actor;
}
