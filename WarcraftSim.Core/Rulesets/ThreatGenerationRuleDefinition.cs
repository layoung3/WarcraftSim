namespace WarcraftSim.Core.Rulesets;

public sealed class ThreatGenerationRuleDefinition
{
    public decimal BaseMultiplier { get; set; }

    public string ThreatOwnerSelectionMode { get; set; } =
        ThreatOwnerSelectionModes.EventTarget;

    public string ThreatOwnerDistributionMode { get; set; } =
        ThreatOwnerDistributionModes.FullAmountPerOwner;

    public string AmountBasis { get; set; } =
        ThreatAmountBasisTypes.EffectiveAmount;

    // Optional direct multiplier stat on the source actor.
    // Example: a verified ruleset could populate a dedicated
    // "threat-multiplier" stat with a value such as 1.3.
    public string? SourceMultiplierStatKey { get; set; }

    public decimal MissingSourceMultiplier { get; set; } =
        1m;

    // Optional ability-specific multiplicative overrides.
    public Dictionary<string, decimal> AbilityMultipliers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
