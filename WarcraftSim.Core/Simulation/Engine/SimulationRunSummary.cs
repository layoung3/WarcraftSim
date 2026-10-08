namespace WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationRunSummary
{
    public int Seed { get; set; }

    public decimal DurationSeconds { get; set; }

    public decimal DamageDone { get; set; }

    public decimal DamageTaken { get; set; }

    public decimal HealingDone { get; set; }

    public decimal HealingReceived { get; set; }

    public decimal OverhealingDone { get; set; }

    public decimal OverhealingReceived { get; set; }

    public decimal AbsorptionDone { get; set; }

    public decimal AbsorptionReceived { get; set; }

    public decimal BlockedDamageReceived { get; set; }

    public Dictionary<string, decimal> DamageDoneByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> DamageTakenByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> HealingDoneByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> HealingReceivedByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> AbsorptionDoneByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> AbsorptionReceivedByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> BlockedDamageReceivedByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Detailed per-ability metrics for the primary actor. ActorSummaries keeps
    // the same metrics for every actor in the run.
    public Dictionary<string, AbilityCombatSummary> Abilities { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, ActorCombatSummary> ActorSummaries { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool PrimaryActorDied { get; set; }

    public decimal? PrimaryActorDeathTimeSeconds { get; set; }
}
