namespace WarcraftSim.Core.Abilities;

public sealed class AbilityResourceCost
{
    public string ResourceKey { get; set; } = "";

    public decimal Amount { get; set; }

    public bool IsPercentOfMaximum { get; set; }
}