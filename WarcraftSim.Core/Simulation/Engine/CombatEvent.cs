namespace WarcraftSim.Core.Simulation.Engine;

public sealed class CombatEvent
{
    public decimal TimeSeconds { get; set; }

    public CombatEventType Type { get; set; }

    public string? SourceActorKey { get; set; }

    public string? TargetActorKey { get; set; }

    public string? AbilityKey { get; set; }

    public Guid? AbilityExecutionId { get; set; }

    public string? EffectKey { get; set; }

    public Guid? AuraInstanceId { get; set; }

    public string? SchoolKey { get; set; }

    public string? ResultKey { get; set; }

    // Amount after crit/scaling but before armor/resistance.
    public decimal? RawAmount { get; set; }

    // Amount removed by armor/resistance.
    public decimal? MitigatedAmount { get; set; }

    public decimal? MitigationPercent { get; set; }

    // Final amount actually applied to health.
    public decimal? Amount { get; set; }

    public decimal? OverhealingAmount { get; set; }

    public bool IsCritical { get; set; }

    public bool IsPeriodic { get; set; }

    public bool IsInternal { get; set; }

    public string? Description { get; set; }
}
