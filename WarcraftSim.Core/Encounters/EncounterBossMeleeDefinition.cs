namespace WarcraftSim.Core.Encounters;

/// <summary>
/// Class-independent boss white-swing configuration. The roll/mitigation
/// formulas are supplied by the selected ruleset, not by the encounter.
/// </summary>
public sealed class EncounterBossMeleeDefinition
{
    public string BossActorKey { get; set; } = "target";
    public string AbilityKey { get; set; } = "boss-main-hand-auto";
    public string PreferredTargetActorKey { get; set; } = "warrior";
    public string? EligibleTargetTeamKey { get; set; }
    public string TargetMode { get; set; } = EncounterBossMeleeTargetModes.Fixed;
    public decimal FirstSwingSeconds { get; set; } = 1m;
    public decimal SwingSpeedSeconds { get; set; } = 2m;
    public decimal MinimumDamage { get; set; } = 900m;
    public decimal MaximumDamage { get; set; } = 1400m;
    public bool CanCrush { get; set; } = true;
    public bool EnableBossParryHaste { get; set; } = true;
    // The threshold is provisional until melee range and exact Forever aggro
    // rules are supported. 110 represents a 10% overtake requirement.
    public decimal ThreatSwapThresholdPercent { get; set; } = 110m;
    public bool PreserveTargetHealth { get; set; }
    public string ResolutionType { get; set; } = "forever-creature-melee-auto";
    public string TargetDefenseSkillStatKey { get; set; } = "defense";
}

public static class EncounterBossMeleeTargetModes
{
    public const string Fixed = "fixed";
    public const string HighestThreat = "highest-threat";
}
