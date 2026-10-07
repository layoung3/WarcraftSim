using System.Collections.ObjectModel;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ThreatTableState
{
    private readonly Dictionary<string, decimal>
        _threatByActor =
            new(StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyDictionary<string, decimal>
        _readOnlyEntries;

    public ThreatTableState()
    {
        _readOnlyEntries =
            new ReadOnlyDictionary<string, decimal>(
                _threatByActor
            );
    }

    public IReadOnlyDictionary<string, decimal> Entries =>
        _readOnlyEntries;

    public decimal GetThreat(
        string actorKey)
    {
        if (string.IsNullOrWhiteSpace(
                actorKey))
        {
            throw new ArgumentException(
                "Threat lookup requires an actor key.",
                nameof(actorKey)
            );
        }

        return _threatByActor.TryGetValue(
            actorKey,
            out var threat)
                ? threat
                : 0m;
    }

    public decimal SetThreat(
        string actorKey,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(
                actorKey))
        {
            throw new ArgumentException(
                "Threat assignment requires an actor key.",
                nameof(actorKey)
            );
        }

        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Threat cannot be negative."
            );
        }

        _threatByActor[
            actorKey
        ] = amount;

        return amount;
    }

    public decimal AddThreat(
        string actorKey,
        decimal amount)
    {
        if (string.IsNullOrWhiteSpace(
                actorKey))
        {
            throw new ArgumentException(
                "Threat generation requires an actor key.",
                nameof(actorKey)
            );
        }

        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Threat generation cannot be negative."
            );
        }

        var current =
            GetThreat(
                actorKey
            );

        var updated =
            current +
            amount;

        _threatByActor[
            actorKey
        ] = updated;

        return updated;
    }

    public decimal GetHighestThreatValue(
        IEnumerable<string> eligibleActorKeys)
    {
        ArgumentNullException.ThrowIfNull(
            eligibleActorKeys
        );

        var eligible =
            new HashSet<string>(
                eligibleActorKeys
                    .Where(
                        actorKey =>
                            !string.IsNullOrWhiteSpace(
                                actorKey
                            )
                    ),
                StringComparer.OrdinalIgnoreCase
            );

        if (eligible.Count == 0)
        {
            return 0m;
        }

        return _threatByActor
            .Where(
                entry =>
                    entry.Value > 0m &&
                    eligible.Contains(
                        entry.Key
                    )
            )
            .Select(
                entry =>
                    entry.Value
            )
            .DefaultIfEmpty(
                0m
            )
            .Max();
    }

    public string? GetHighestThreatActorKey(
        IEnumerable<string> eligibleActorKeys)
    {
        ArgumentNullException.ThrowIfNull(
            eligibleActorKeys
        );

        var eligible =
            new HashSet<string>(
                eligibleActorKeys
                    .Where(
                        actorKey =>
                            !string.IsNullOrWhiteSpace(
                                actorKey
                            )
                    ),
                StringComparer.OrdinalIgnoreCase
            );

        if (eligible.Count == 0)
        {
            return null;
        }

        return _threatByActor
            .Where(
                entry =>
                    entry.Value > 0m &&
                    eligible.Contains(
                        entry.Key
                    )
            )
            .OrderByDescending(
                entry =>
                    entry.Value
            )
            .ThenBy(
                entry =>
                    entry.Key,
                StringComparer.OrdinalIgnoreCase
            )
            .Select(
                entry =>
                    entry.Key
            )
            .FirstOrDefault();
    }

    public string? GetHighestThreatActorKey()
    {
        return _threatByActor
            .Where(
                entry =>
                    entry.Value > 0m
            )
            .OrderByDescending(
                entry =>
                    entry.Value
            )
            .ThenBy(
                entry =>
                    entry.Key,
                StringComparer.OrdinalIgnoreCase
            )
            .Select(
                entry =>
                    entry.Key
            )
            .FirstOrDefault();
    }
}
