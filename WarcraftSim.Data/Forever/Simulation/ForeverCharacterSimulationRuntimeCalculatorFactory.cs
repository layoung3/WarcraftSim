using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Simulation;

public static class ForeverCharacterSimulationRuntimeCalculatorFactory
{
    public static BaseStatsCharacterSimulationRuntimeCalculator
        CreateStandard(
            Func<CharacterProfile, decimal> maximumHealthResolver,
            Func<CharacterProfile, decimal?>? startingHealthResolver = null,
            IEnumerable<ICharacterSimulationStatContributor>?
                additionalStatContributors = null)
    {
        ArgumentNullException.ThrowIfNull(
            maximumHealthResolver
        );

        var foreverContributors =
            new List<ICharacterSimulationStatContributor>
            {
                new ForeverPhysicalStatConversionContributor(),
                new ForeverPrimaryPhysicalStatContributor()
            };

        if (additionalStatContributors is not null)
        {
            foreverContributors.AddRange(
                additionalStatContributors
            );
        }

        return CharacterSimulationRuntimeCalculatorFactory
            .CreateStandard(
                maximumHealthResolver,
                startingHealthResolver,
                foreverContributors
            );
    }
}
