namespace WarcraftSim.Core.Rotations;

public static class RotationConditionTypes
{
    public const string CurrentTimeSeconds = "current-time-seconds";

    public const string SourceHealthPercent = "source-health-percent";
    public const string TargetHealthPercent = "target-health-percent";

    public const string SourceResourceCurrent = "source-resource-current";
    public const string SourceResourcePercent = "source-resource-percent";

    public const string SourceAuraActive = "source-aura-active";
    public const string SourceAuraMissing = "source-aura-missing";
    public const string TargetAuraActive = "target-aura-active";
    public const string TargetAuraMissing = "target-aura-missing";

    public const string EncounterPhaseActive = "encounter-phase-active";
    public const string EncounterPhaseInactive = "encounter-phase-inactive";
}
