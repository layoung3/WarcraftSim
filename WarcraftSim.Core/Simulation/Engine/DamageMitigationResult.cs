namespace WarcraftSim.Core.Simulation.Engine;

public sealed class DamageMitigationResult
{
    public decimal RawAmount { get; init; }

    public decimal FinalAmount { get; init; }

    public decimal MitigatedAmount { get; init; }

    public decimal ReductionPercent { get; init; }

    public decimal DefenseValue { get; init; }

    public static DamageMitigationResult Unmitigated(
        decimal rawAmount)
    {
        var amount =
            Math.Max(
                0m,
                rawAmount
            );

        return new DamageMitigationResult
        {
            RawAmount = amount,
            FinalAmount = amount,
            MitigatedAmount = 0m,
            ReductionPercent = 0m,
            DefenseValue = 0m
        };
    }
}
