using WarcraftSim.Core.Characters;

namespace WarcraftSim.Core.Simulation.Building;

public interface ICharacterSimulationStatContributionProvider
{
    IReadOnlyList<CharacterSimulationStatContribution>
        GetStatContributions(
            CharacterProfile profile);
}
