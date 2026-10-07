namespace WarcraftSim.Core.Simulation.Engine;

public sealed class CombatRollResult
{
    public bool Landed { get; init; }

    public bool IsCritical { get; init; }

    public string ResultKey { get; init; } = "";

    public decimal AmountMultiplier { get; init; } = 1m;

    // Requested block value supplied by the combat ruleset. The actual
    // blocked amount is clamped after ordinary damage mitigation.
    public decimal BlockValue { get; init; }

    public bool IsBlocked =>
        Landed &&
        string.Equals(
            ResultKey,
            CombatResultTypes.Block,
            StringComparison.OrdinalIgnoreCase
        );

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
        return Avoided(
            CombatResultTypes.Miss
        );
    }

    public static CombatRollResult Dodge()
    {
        return Avoided(
            CombatResultTypes.Dodge
        );
    }

    public static CombatRollResult Parry()
    {
        return Avoided(
            CombatResultTypes.Parry
        );
    }

    public static CombatRollResult Block(
        decimal blockValue)
    {
        return new CombatRollResult
        {
            Landed = true,
            ResultKey = CombatResultTypes.Block,
            AmountMultiplier = 1m,
            BlockValue =
                Math.Max(
                    0m,
                    blockValue
                )
        };
    }

    public static CombatRollResult Avoided(
        string resultKey)
    {
        if (string.IsNullOrWhiteSpace(
                resultKey))
        {
            throw new ArgumentException(
                "Avoided combat result requires a result key.",
                nameof(resultKey)
            );
        }

        return new CombatRollResult
        {
            Landed = false,
            ResultKey = resultKey,
            AmountMultiplier = 0m
        };
    }
}
