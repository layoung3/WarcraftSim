namespace WarcraftSim.Core.Simulation.Building;

public sealed class CharacterSimulationStatBonusSource
{
    private readonly IReadOnlyDictionary<string, decimal>
        _statBonuses;

    public CharacterSimulationStatBonusSource(
        string key,
        string sourceType,
        IReadOnlyDictionary<string, decimal> statBonuses,
        string? name = null,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(
                key))
        {
            throw new ArgumentException(
                "Stat bonus source requires a key.",
                nameof(key)
            );
        }

        if (string.IsNullOrWhiteSpace(
                sourceType))
        {
            throw new ArgumentException(
                "Stat bonus source requires a source type.",
                nameof(sourceType)
            );
        }

        ArgumentNullException.ThrowIfNull(
            statBonuses
        );

        Key =
            key.Trim();

        SourceType =
            sourceType.Trim();

        Name =
            string.IsNullOrWhiteSpace(
                name)
                ? Key
                : name.Trim();

        IsActive =
            isActive;

        _statBonuses =
            CopyAndValidateBonuses(
                statBonuses
            );
    }

    public string Key { get; }

    public string SourceType { get; }

    public string Name { get; }

    public bool IsActive { get; }

    public IReadOnlyDictionary<string, decimal>
        StatBonuses =>
            _statBonuses;

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
