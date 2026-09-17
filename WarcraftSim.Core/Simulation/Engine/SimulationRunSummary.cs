namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationRunSummary
{
    public int Seed { get; set; }

    public decimal DurationSeconds { get; set; }

    public decimal DamageDone { get; set; }

    public decimal DamageTaken { get; set; }

    public decimal HealingDone { get; set; }

    public decimal HealingReceived { get; set; }

    public bool PrimaryActorDied { get; set; }

    public decimal? PrimaryActorDeathTimeSeconds { get; set; }
}