using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterTalentStatContributor :
    ICharacterSimulationStatContributor,
    ICharacterSimulationStatContributionProvider
{
    private readonly CharacterTalentStatSourceAdapter
        _adapter;

    public CharacterTalentStatContributor(
        int order =
            CharacterSimulationStatContributorOrders.Talents,
        CharacterTalentStatSourceAdapter? adapter = null)
    {
        Order =
            order;

        _adapter =
            adapter ??
            new CharacterTalentStatSourceAdapter();
    }

    public string Key =>
        "talents";

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
