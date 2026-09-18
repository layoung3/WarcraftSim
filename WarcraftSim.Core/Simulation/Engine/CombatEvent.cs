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

    public string? EncounterPhaseKey { get; set; }

    public string? EncounterEventKey { get; set; }

    public string? SchoolKey { get; set; }

    public string? ResultKey { get; set; }

    public decimal? RawAmount { get; set; }

    public decimal? MitigatedAmount { get; set; }

    public decimal? MitigationPercent { get; set; }

    public decimal? Amount { get; set; }

    public decimal? OverhealingAmount { get; set; }

    public bool IsCritical { get; set; }

    public bool IsPeriodic { get; set; }

    public bool IsInternal { get; set; }

    public string? Description { get; set; }
}
