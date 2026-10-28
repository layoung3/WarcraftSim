namespace WarcraftSim.Data.Forever.Warlock;

/// <summary>
/// Forever Bane of Agony has 12 two-second ticks and ramps in three stages.
/// The 0.5/1/1.5 distribution is the documented Classic per-tick behavior
/// carried forward as a PROVISIONAL numerical shape, not independently
/// measured in a level-60 Forever combat log. It preserves the source-known
/// 552 rank-6 total at zero spell power.
/// </summary>
public static class ForeverWarlockAgonyRamp
{
    public static decimal FactorForTick(int index) => index switch
    {
        >=1 and <=4 => .5m,
        >=5 and <=8 => 1m,
        >=9 and <=12 => 1.5m,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static int TickIndex(decimal elapsedSeconds)
    {
        if(elapsedSeconds<=0m) return 1;
        return Math.Clamp((int)Math.Ceiling(elapsedSeconds/2m),1,12);
    }
}
