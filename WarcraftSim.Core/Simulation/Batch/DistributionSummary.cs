namespace WarcraftSim.Core.Simulation.Batch;

public sealed class DistributionSummary
{
    public int Count { get; set; }

    public decimal Mean { get; set; }

    public decimal StandardDeviation { get; set; }

    public decimal Minimum { get; set; }

    public decimal Percentile05 { get; set; }

    public decimal Percentile25 { get; set; }

    public decimal Median { get; set; }

    public decimal Percentile75 { get; set; }

    public decimal Percentile95 { get; set; }

    public decimal Maximum { get; set; }
}
