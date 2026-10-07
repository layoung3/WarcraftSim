namespace WarcraftSim.Data.Forever.Combat;

public sealed class ForeverPrimaryPhysicalClassStatProfile
{
    public string ClassKey { get; init; } = "";

    public decimal AttackPowerPerStrength { get; init; }

    public decimal AttackPowerPerAgility { get; init; }

    public decimal RangedAttackPowerPerAgility { get; init; }

    public IReadOnlyDictionary<int, decimal>
        AgilityPerCriticalPercentByLevel { get; init; } =
            new Dictionary<int, decimal>();
}
