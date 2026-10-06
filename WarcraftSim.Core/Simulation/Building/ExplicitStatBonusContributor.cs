using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Core.Simulation.Building;

public sealed class ExplicitStatBonusContributor :
    ICharacterSimulationStatContributor
{
    private readonly IReadOnlyDictionary<string, decimal>
        _statBonuses;

    public ExplicitStatBonusContributor(
        string key,
        int order,
        IReadOnlyDictionary<string, decimal> statBonuses)
    {
        if (string.IsNullOrWhiteSpace(
                key))
        {
            throw new ArgumentException(
                "Stat bonus contributor requires a key.",
                nameof(key)
            );
        }

        ArgumentNullException.ThrowIfNull(
            statBonuses
        );

        Key =
            key.Trim();

        Order =
            order;

        _statBonuses =
            CopyAndValidateBonuses(
                statBonuses
            );
    }

    public string Key { get; }

    public int Order { get; }

    public IReadOnlyDictionary<string, decimal>
        StatBonuses =>
            _statBonuses;

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
            var bonus in
            _statBonuses)
        {
            effectiveStats.Set(
                bonus.Key,
                effectiveStats.Get(
                    bonus.Key
                ) +
                bonus.Value
            );
        }
    }

    private static IReadOnlyDictionary<string, decimal>
        CopyAndValidateBonuses(
            IReadOnlyDictionary<string, decimal> source)
    {
        var copy =
            new Dictionary<string, decimal>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (
            var bonus in
            source)
        {
            if (string.IsNullOrWhiteSpace(
                    bonus.Key))
            {
                throw new ArgumentException(
                    "Stat bonus keys cannot be blank.",
                    nameof(source)
                );
            }

            var normalizedKey =
                bonus.Key.Trim();

            if (!copy.TryAdd(
                    normalizedKey,
                    bonus.Value))
            {
                throw new ArgumentException(
                    $"Duplicate stat bonus key '{normalizedKey}'.",
                    nameof(source)
                );
            }
        }

        return copy;
    }
}
