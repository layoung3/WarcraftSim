namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Resolves a single percentage roll against ordered combat outcomes.
/// Earlier entries occupy the table first and can push later outcomes
/// partially or completely off the table.
/// </summary>
public static class OrderedCombatRollTable
{
    public static CombatRollResult Resolve(
        decimal rollPercent,
        IEnumerable<CombatRollTableEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(
            entries
        );

        if (
            rollPercent < 0m ||
            rollPercent >= 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rollPercent),
                rollPercent,
                "Combat table roll must be at least zero and less than 100."
            );
        }

        var cumulativeChance =
            0m;

        foreach (
            var entry in
            entries)
        {
            ArgumentNullException.ThrowIfNull(
                entry
            );

            var chance =
                Math.Clamp(
                    entry.ChancePercent,
                    0m,
                    100m
                );

            if (chance <= 0m)
            {
                continue;
            }

            cumulativeChance =
                Math.Min(
                    100m,
                    cumulativeChance +
                    chance
                );

            if (rollPercent <
                cumulativeChance)
            {
                return entry.Result;
            }

            if (cumulativeChance >= 100m)
            {
                break;
            }
        }

        return CombatRollResult.Hit();
    }
}
