namespace WarcraftSim.Core.Rotations;

public static class RotationConditionTypes
{
    public const string CurrentTimeSeconds = "current-time-seconds";
    public const string EncounterTimeRemainingSeconds = "encounter-time-remaining-seconds";
    public const string SourceAbilityReady = "source-ability-ready";
    public const string SourceAbilityCooldownRemainingSeconds = "source-ability-cooldown-remaining-seconds";
    public const string SourceResourceDeficit = "source-resource-deficit";
    public const string SourceAuraRemainingSeconds = "source-aura-remaining-seconds";
    public const string TargetAuraRemainingSeconds = "target-aura-remaining-seconds";
    public const string TargetAuraStackCount = "target-aura-stack-count";

    public const string ActiveEnemyCount = "active-enemy-count";

    public const string SourceHealthPercent = "source-health-percent";
    public const string TargetHealthPercent = "target-health-percent";

    public const string SourceStatValue = "source-stat-value";

    public const string SourceAbilityOnCooldown = "source-ability-on-cooldown";

    public const string SourceResourceCurrent = "source-resource-current";
    public const string SourceResourcePercent = "source-resource-percent";

    public const string SourceAuraActive = "source-aura-active";
    public const string SourceAuraMissing = "source-aura-missing";
    public const string TargetAuraActive = "target-aura-active";
    public const string TargetAuraMissing = "target-aura-missing";

    public const string EncounterPhaseActive = "encounter-phase-active";
    public const string EncounterPhaseInactive = "encounter-phase-inactive";
}
