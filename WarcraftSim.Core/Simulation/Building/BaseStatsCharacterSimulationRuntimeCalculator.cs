using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class BaseStatsCharacterSimulationRuntimeCalculator :
    ICharacterSimulationRuntimeCalculator
{
    private readonly Func<CharacterProfile, decimal>
        _maximumHealthResolver;

    private readonly Func<CharacterProfile, decimal?>?
        _startingHealthResolver;

    private readonly IReadOnlyList<ICharacterSimulationStatContributor>
        _statContributors;

    public BaseStatsCharacterSimulationRuntimeCalculator(
        Func<CharacterProfile, decimal> maximumHealthResolver,
        Func<CharacterProfile, decimal?>? startingHealthResolver = null,
        IEnumerable<ICharacterSimulationStatContributor>? statContributors = null)
    {
        ArgumentNullException.ThrowIfNull(
            maximumHealthResolver
        );

        _maximumHealthResolver =
            maximumHealthResolver;

        _startingHealthResolver =
            startingHealthResolver;

        _statContributors =
            BuildContributorPipeline(
                statContributors
            );
    }

    public CharacterSimulationRuntimeCalculationResult Calculate(
        CharacterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile
        );

        var maximumHealth =
            _maximumHealthResolver(
                profile
            );

        if (maximumHealth <= 0m)
        {
            throw new InvalidOperationException(
                "Calculated maximum health must be greater than zero."
            );
        }

        var startingHealth =
            _startingHealthResolver?.Invoke(
                profile
            );

        if (
            startingHealth.HasValue &&
            (
                startingHealth.Value < 0m ||
                startingHealth.Value >
                    maximumHealth
            )
        )
        {
            throw new InvalidOperationException(
                "Calculated starting health must be between zero and maximum health."
            );
        }

        var effectiveStats =
            CopyStats(
                profile.BaseStats
            );

        var statContributions =
            CreateBaseStatContributions(
                profile.BaseStats
            );

        foreach (
            var contributor in
            _statContributors)
        {
            var before =
                CopyStats(
                    effectiveStats
                );

            contributor.Contribute(
                profile,
                effectiveStats
            );

            if (
                contributor is
                    ICharacterSimulationStatContributionProvider
                    contributionProvider)
            {
                statContributions.AddRange(
                    contributionProvider
                        .GetStatContributions(
                            profile
                        )
                );

                continue;
            }

            statContributions.AddRange(
                CreateContributorDeltaContributions(
                    contributor,
                    before,
                    effectiveStats
                )
            );
        }

        return new CharacterSimulationRuntimeCalculationResult
        {
            MaximumHealth =
                maximumHealth,

            StartingHealth =
                startingHealth,

            EffectiveStats =
                effectiveStats,

            StatContributions =
                statContributions
        };
    }

    private static List<CharacterSimulationStatContribution>
        CreateBaseStatContributions(
            StatCollection baseStats)
    {
        return baseStats.Values
            .Where(
                stat =>
                    stat.Value != 0m
            )
            .OrderBy(
                stat =>
                    stat.Key,
                StringComparer.OrdinalIgnoreCase
            )
            .Select(
                stat =>
                    new CharacterSimulationStatContribution(
                        contributorKey:
                            "base-stats",

                        sourceKey:
                            "base-stats",

                        sourceType:
                            "base",

                        sourceName:
                            "Base Stats",

                        statKey:
                            stat.Key,

                        amount:
                            stat.Value
                    )
            )
            .ToList();
    }

    private static IReadOnlyList<CharacterSimulationStatContribution>
        CreateContributorDeltaContributions(
            ICharacterSimulationStatContributor contributor,
            StatCollection before,
            StatCollection after)
    {
        var statKeys =
            before.Values.Keys
                .Concat(
                    after.Values.Keys
                )
                .Distinct(
                    StringComparer.OrdinalIgnoreCase
                )
                .OrderBy(
                    key =>
                        key,
                    StringComparer.OrdinalIgnoreCase
                );

        var contributions =
            new List<CharacterSimulationStatContribution>();

        foreach (
            var statKey in
            statKeys)
        {
            var delta =
                after.Get(
                    statKey
                ) -
                before.Get(
                    statKey
                );

            if (delta == 0m)
            {
                continue;
            }

            contributions.Add(
                new CharacterSimulationStatContribution(
                    contributorKey:
                        contributor.Key,

                    sourceKey:
                        contributor.Key,

                    sourceType:
                        "contributor",

                    sourceName:
                        contributor.Key,

                    statKey:
                        statKey,

                    amount:
                        delta
                )
            );
        }

        return contributions;
    }

    private static IReadOnlyList<ICharacterSimulationStatContributor>
        BuildContributorPipeline(
            IEnumerable<ICharacterSimulationStatContributor>? contributors)
    {
        if (contributors is null)
        {
            return [];
        }

        var materialized =
            contributors.ToList();

        var keys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (
            var contributor in
            materialized)
        {
            if (contributor is null)
            {
                throw new ArgumentException(
                    "Character stat contributor pipeline cannot contain null entries.",
                    nameof(contributors)
                );
            }

            if (string.IsNullOrWhiteSpace(
                    contributor.Key))
            {
                throw new ArgumentException(
                    "Character stat contributors require a key.",
                    nameof(contributors)
                );
            }

            if (!keys.Add(
                    contributor.Key))
            {
                throw new ArgumentException(
                    $"Duplicate character stat contributor key '{contributor.Key}'.",
                    nameof(contributors)
                );
            }
        }

        return materialized
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

    private static StatCollection CopyStats(
        StatCollection source)
    {
        var copy =
            new StatCollection();

        foreach (
            var stat in
            source.Values)
        {
            copy.Set(
                stat.Key,
                stat.Value
            );
        }

        return copy;
    }
}
