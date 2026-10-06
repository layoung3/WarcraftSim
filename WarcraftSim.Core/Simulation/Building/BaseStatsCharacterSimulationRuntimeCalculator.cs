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

    public BaseStatsCharacterSimulationRuntimeCalculator(
        Func<CharacterProfile, decimal> maximumHealthResolver,
        Func<CharacterProfile, decimal?>? startingHealthResolver = null)
    {
        ArgumentNullException.ThrowIfNull(
            maximumHealthResolver
        );

        _maximumHealthResolver =
            maximumHealthResolver;

        _startingHealthResolver =
            startingHealthResolver;
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

        return new CharacterSimulationRuntimeCalculationResult
        {
            MaximumHealth =
                maximumHealth,

            StartingHealth =
                startingHealth,

            EffectiveStats =
                CopyStats(
                    profile.BaseStats
                )
        };
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
