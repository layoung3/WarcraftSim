using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class StatBonusSourceContributor :
    ICharacterSimulationStatContributor,
    ICharacterSimulationStatContributionProvider
{
    private readonly IReadOnlyList<CharacterSimulationStatBonusSource>
        _sources;

    public StatBonusSourceContributor(
        string key,
        int order,
        IEnumerable<CharacterSimulationStatBonusSource> sources)
    {
        if (string.IsNullOrWhiteSpace(
                key))
        {
            throw new ArgumentException(
                "Stat bonus source contributor requires a key.",
                nameof(key)
            );
        }

        ArgumentNullException.ThrowIfNull(
            sources
        );

        Key =
            key.Trim();

        Order =
            order;

        _sources =
            CopyAndValidateSources(
                sources
            );
    }

    public string Key { get; }

    public int Order { get; }

    public IReadOnlyList<CharacterSimulationStatBonusSource>
        Sources =>
            _sources;

    public IReadOnlyList<CharacterSimulationStatContribution>
        GetStatContributions(
            CharacterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile
        );

        var contributions =
            new List<CharacterSimulationStatContribution>();

        foreach (
            var source in
            _sources)
        {
            if (!source.IsActive)
            {
                continue;
            }

            foreach (
                var bonus in
                source.StatBonuses
                    .OrderBy(
                        bonus =>
                            bonus.Key,
                        StringComparer.OrdinalIgnoreCase
                    ))
            {
                if (bonus.Value == 0m)
                {
                    continue;
                }

                contributions.Add(
                    new CharacterSimulationStatContribution(
                        contributorKey:
                            Key,

                        sourceKey:
                            source.Key,

                        sourceType:
                            source.SourceType,

                        sourceName:
                            source.Name,

                        statKey:
                            bonus.Key,

                        amount:
                            bonus.Value
                    )
                );
            }
        }

        return contributions;
    }

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

        foreach (
            var source in
            _sources)
        {
            if (!source.IsActive)
            {
                continue;
            }

            foreach (
                var bonus in
                source.StatBonuses)
            {
                effectiveStats.Add(
                    bonus.Key,
                    bonus.Value
                );
            }
        }
    }

    private static IReadOnlyList<CharacterSimulationStatBonusSource>
        CopyAndValidateSources(
            IEnumerable<CharacterSimulationStatBonusSource> sources)
    {
        var materialized =
            sources.ToList();

        var keys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (
            var source in
            materialized)
        {
            if (source is null)
            {
                throw new ArgumentException(
                    "Stat bonus source collection cannot contain null entries.",
                    nameof(sources)
                );
            }

            if (!keys.Add(
                    source.Key))
            {
                throw new ArgumentException(
                    $"Duplicate stat bonus source key '{source.Key}'.",
                    nameof(sources)
                );
            }
        }

        return materialized
            .OrderBy(
                source =>
                    source.Key,
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();
    }
}
