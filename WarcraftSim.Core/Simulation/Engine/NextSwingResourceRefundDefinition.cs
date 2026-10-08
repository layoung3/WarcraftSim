namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Defines an outcome-sensitive refund for one resource cost paid by a
/// queued next-swing replacement. The refund amount is a percentage of the
/// amount actually paid at swing time, not the original configured amount.
/// </summary>
public sealed class NextSwingResourceRefundDefinition
{
    public string ResourceKey { get; set; } = "";

    public decimal RefundPercent { get; set; }

    public List<string> ResultKeys { get; set; } = [];
}
