namespace WarcraftSim.Data.Forever.Combat;

public sealed class ForeverPrimarySpellClassStatProfile
{
    public string ClassKey { get; init; } = "";

    public bool UsesMana { get; init; }

    public IReadOnlyDictionary<int, decimal>
        IntellectPerSpellCriticalPercentByLevel { get; init; } =
            new Dictionary<int, decimal>();
}
