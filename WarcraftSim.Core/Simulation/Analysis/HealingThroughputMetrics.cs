namespace WarcraftSim.Core.Simulation.Analysis;

public sealed class HealingThroughputMetrics
{
    public string HealerActorKey { get; set; } = "";

    public decimal WindowSeconds { get; set; }

    public decimal EffectiveHealing { get; set; }

    public decimal Overhealing { get; set; }

    public decimal RawHealing { get; set; }

    public decimal EffectiveHps { get; set; }

    public decimal RawHps { get; set; }

    public decimal OverhealingPercent { get; set; }

    public string ResourceKey { get; set; } = "";

    public decimal StartingResource { get; set; }

    public decimal EndingResource { get; set; }

    public bool BecameResourceStarved { get; set; }

    public decimal? FirstResourceStarvedAtSeconds { get; set; }

    public decimal ResourceStarvedSeconds { get; set; }

    public decimal ResourceStarvedPercent { get; set; }

    public Dictionary<string, int> CompletedCastsByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
