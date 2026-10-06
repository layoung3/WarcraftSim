using WarcraftSim.Core.Characters;

namespace WarcraftSim.Core.Simulation.Building;

public static class CharacterSimulationRuntimeCalculatorFactory
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

        return new BaseStatsCharacterSimulationRuntimeCalculator(
            maximumHealthResolver,
            startingHealthResolver,
            CharacterSimulationStatContributorPipeline
                .CreateStandard(
                    additionalStatContributors
                )
        );
    }
}
