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

        foreach (
            var contributor in
            _statContributors)
        {
            contributor.Contribute(
                profile,
                effectiveStats
            );
        }

        return new CharacterSimulationRuntimeCalculationResult
        {
            MaximumHealth =
                maximumHealth,

            StartingHealth =
                startingHealth,

            EffectiveStats =
                effectiveStats
        };
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
