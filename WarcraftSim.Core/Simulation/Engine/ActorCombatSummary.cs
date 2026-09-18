namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ActorCombatSummary
{
    public string ActorKey { get; set; } = "";

    public string Name { get; set; } = "";

    public decimal DamageDone { get; set; }

    public decimal DamageTaken { get; set; }

    public decimal HealingDone { get; set; }

    public decimal HealingReceived { get; set; }

    public decimal OverhealingDone { get; set; }

    public decimal OverhealingReceived { get; set; }

    public Dictionary<string, decimal> DamageDoneByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> DamageTakenByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> HealingDoneByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> HealingReceivedByAbility { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool Died { get; set; }

    public decimal? DeathTimeSeconds { get; set; }
}
