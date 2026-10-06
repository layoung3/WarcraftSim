using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Building;

public interface ICharacterSimulationStatContributor
{
    string Key { get; }

    int Order { get; }

    void Contribute(
        CharacterProfile profile,
        StatCollection effectiveStats);
}
