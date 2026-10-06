using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterEquipmentStatContributor :
    ICharacterSimulationStatContributor,
    ICharacterSimulationStatContributionProvider
{
    private readonly CharacterEquipmentStatSourceAdapter
        _adapter;

    public CharacterEquipmentStatContributor(
        int order =
            CharacterSimulationStatContributorOrders.Equipment,
        CharacterEquipmentStatSourceAdapter? adapter = null)
    {
        Order =
            order;

        _adapter =
            adapter ??
            new CharacterEquipmentStatSourceAdapter();
    }

    public string Key =>
        "equipment";

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
