namespace WarcraftSim.Core.Simulation.Batch;

public sealed class SimulationBatchSummary
{
    public int Iterations { get; set; }

    public int BaseSeed { get; set; }

    // Explicit statistical conventions for export and visible results tooltips.
    public string SeedStrategy { get; set; } = "unchecked(baseSeed + iterationIndex), independent engine context per run";
    public string StandardDeviationMethod { get; set; } = "population: sqrt(sum((x - mean)^2) / N)";
    public string PercentileMethod { get; set; } = "linear interpolation at p * (N - 1) on sorted values";

    public SimulationType SimulationType { get; set; }

    public decimal DurationSeconds { get; set; }

    public DistributionSummary DamageDone { get; set; } = new();

    public DistributionSummary DamagePerSecond { get; set; } = new();

    public DistributionSummary DamageTaken { get; set; } = new();

    public DistributionSummary DamageTakenPerSecond { get; set; } = new();

    public DistributionSummary HealingDone { get; set; } = new();

    public DistributionSummary HealingPerSecond { get; set; } = new();

    public decimal PrimaryActorDeathRatePercent { get; set; }

    public Dictionary<string, AbilityBatchOutcomeSummary> AverageCombatOutcomesByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> AverageDamageDoneByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> AverageDamageTakenByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> AverageHealingDoneByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
