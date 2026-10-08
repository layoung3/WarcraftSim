namespace WarcraftSim.Core.Abilities;

public sealed class AbilityAdditionalResourceConsumption
{
    public string ResourceKey { get; set; } = "";

    // Null consumes all resource remaining after the ability's ordinary
    // costs are paid. A value caps how much of the remaining resource may be
    // consumed. Zero is valid and consumes nothing.
    public decimal? MaximumAmount { get; set; }
}
