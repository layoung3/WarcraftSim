namespace WarcraftSim.Core.Simulation.Building;

public static class CharacterSimulationStatContributorPipeline
{
    public static IReadOnlyList<ICharacterSimulationStatContributor>
        CreateStandard(
            IEnumerable<ICharacterSimulationStatContributor>?
                additionalContributors = null)
    {
        var contributors =
            new List<ICharacterSimulationStatContributor>
            {
                new CharacterEquipmentStatContributor(),
                new CharacterTalentStatContributor(),
                new CharacterBuffStatContributor()
            };

        if (additionalContributors is not null)
        {
            contributors.AddRange(
                additionalContributors
            );
        }

        return contributors
            .OrderBy(
                contributor =>
                    contributor.Order
            )
            .ThenBy(
                contributor =>
                    contributor.Key,
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();
    }
}
