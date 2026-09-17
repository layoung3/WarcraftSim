namespace WarcraftSim.Core.Simulation.Engine;

public sealed class CombatEvent
{
    public decimal TimeSeconds { get; set; }

    public CombatEventType Type { get; set; }

    public string? SourceActorKey { get; set; }

    public string? TargetActorKey { get; set; }

    public string? AbilityKey { get; set; }

    public string? EffectKey { get; set; }

    public string? SchoolKey { get; set; }

    // Final amount after things like mitigation, overhealing, etc.
    public decimal? Amount { get; set; }

    // Amount before mitigation, overhealing, etc.
    public decimal? RawAmount { get; set; }

    public decimal? OverhealingAmount { get; set; }

    public bool IsCritical { get; set; }

    public bool IsPeriodic { get; set; }

    public string? Description { get; set; }
}