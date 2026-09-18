namespace WarcraftSim.Core.Simulation.Engine;

public sealed class CombatRollResult
{
    public bool Landed { get; init; }

    public bool IsCritical { get; init; }

    public string ResultKey { get; init; } = "";

    public decimal AmountMultiplier { get; init; } = 1m;

    public static CombatRollResult Hit()
    {
        return new CombatRollResult
        {
            Landed = true,
            ResultKey = CombatResultTypes.Hit,
            AmountMultiplier = 1m
        };
    }

    public static CombatRollResult Critical(decimal multiplier)
    {
        return new CombatRollResult
        {
            Landed = true,
            IsCritical = true,
            ResultKey = CombatResultTypes.Critical,
            AmountMultiplier = Math.Max(0m, multiplier)
        };
    }

    public static CombatRollResult Miss()
    {
        return new CombatRollResult
        {
            Landed = false,
            ResultKey = CombatResultTypes.Miss,
            AmountMultiplier = 0m
        };
    }
}
