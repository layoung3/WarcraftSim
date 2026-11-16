namespace WarcraftSim.Data.Forever.Warlock;

/// <summary>
/// Forever client 1.60.1.70707, trained Drain Mana ranks 1-4 at
/// levels 24, 34, 44 and 54 (IDs 5138, 6226, 11703, 11704).
/// Base Mana amounts are sourced; Improved Drains 7/13/20% remains provisional.
/// </summary>
public static class ForeverWarlockDrainMana
{
    public static decimal ManaCost(int rank) => rank switch
    {
        1 => 95m, 2 => 155m, 3 => 225m, 4 => 310m,
        _ => throw new ArgumentOutOfRangeException(nameof(rank))
    };

    public static decimal BasePerTick(int rank) => rank switch
    {
        1 => 42m, 2 => 68m, 3 => 99m, 4 => 136m,
        _ => throw new ArgumentOutOfRangeException(nameof(rank))
    };

    public static decimal ModifiedPerTick(int spellRank, int improvedDrainsRank) =>
        BasePerTick(spellRank) * (improvedDrainsRank switch
        {
            0 => 1m, 1 => 1.07m, 2 => 1.13m, 3 => 1.20m,
            _ => throw new ArgumentOutOfRangeException(nameof(improvedDrainsRank))
        });
}
