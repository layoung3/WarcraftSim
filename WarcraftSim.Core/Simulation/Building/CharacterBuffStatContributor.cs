using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterBuffStatContributor :
    ICharacterSimulationStatContributor,
    ICharacterSimulationStatContributionProvider
{
    private readonly CharacterBuffStatSourceAdapter
        _adapter;

    public CharacterBuffStatContributor(
        int order,
        CharacterBuffStatSourceAdapter? adapter = null)
    {
        Order =
            order;

        _adapter =
            adapter ??
            new CharacterBuffStatSourceAdapter();
    }

    public string Key =>
        "buffs";

    public int Order { get; }

    public void Contribute(
        CharacterProfile profile,
        StatCollection effectiveStats)
    {
        ArgumentNullException.ThrowIfNull(
            profile
        );

        ArgumentNullException.ThrowIfNull(
            effectiveStats
        );

        CreateSourceContributor(
                profile
            )
            .Contribute(
                profile,
                effectiveStats
            );
    }

    public IReadOnlyList<CharacterSimulationStatContribution>
        GetStatContributions(
            CharacterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile
        );

        return CreateSourceContributor(
                profile
            )
            .GetStatContributions(
                profile
            );
    }

    private StatBonusSourceContributor CreateSourceContributor(
        CharacterProfile profile)
    {
        return new StatBonusSourceContributor(
            key:
                Key,

            order:
                Order,

            sources:
                _adapter.CreateSources(
                    profile
                )
        );
    }
}
