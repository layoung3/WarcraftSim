using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Paladin;

namespace WarcraftSim.Api.Simulation;

/// <summary>One class-wide request. Neither a preset name nor a tree label chooses mechanics.</summary>
public sealed class PaladinSimRequest
{
    public CharacterProfile Character { get; set; } = new() { Name="Shared Paladin", ClassKey="paladin", SpecializationKey="retribution", RulesetKey="wow-forever", Race="human", Level=60 };
    public ForeverPaladinTalents Talents { get; set; } = new();
    public ForeverExternalModifierSelection ExternalModifiers { get; set; } = new();
    public CombatUseItemsOptions CombatItems { get; set; } = new();
    public ForeverLevel60BaselineObservation? Level60Baseline { get; set; }
    public bool UseReviewedLevel60Baseline { get; set; }
    public bool UseProvisionalLevel60Baseline { get; set; }
    public int Seed { get; set; } = 175113;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public decimal TargetMaximumHealth { get; set; } = 200000m;
    public decimal TargetInitialHealthPercent { get; set; } = 100m;
    public decimal TargetArmor { get; set; } = 3500m;
    public int TargetCount { get; set; } = 1;
    public string TargetCreatureType { get; set; } = "humanoid";
    public bool TargetControlImmune { get; set; } = true;
    public decimal MaximumHealth { get; set; } = 3500m;
    public decimal InitialHealthPercent { get; set; } = 100m;
    public decimal InitialManaPercent { get; set; } = 100m;
    // An explicit regeneration observation/assumption. No unverified Spirit formula is invented.
    public decimal ManaRegenPerSecond { get; set; }
    public string Seal { get; set; } = "righteousness";
    public string? SecondarySeal { get; set; }
    public decimal? SealOfJusticeProcChancePercent { get; set; }
    public decimal SealSwitchIntervalSeconds { get; set; } = 9m;
    public string Aura { get; set; } = "devotion";
    public bool UseRighteousFury { get; set; }
    public bool UseAutoAttacks { get; set; } = true;
    public bool UseManualWeaponInputs { get; set; }
    public string ManualWeaponKind { get; set; } = "two-hand";
    public decimal WeaponMinDamage { get; set; } = 200m;
    public decimal WeaponMaxDamage { get; set; } = 300m;
    public decimal WeaponSpeedSeconds { get; set; } = 3.5m;
    public bool ManualShield { get; set; }
    public decimal ManualShieldBlockValue { get; set; } = 40m;
    public bool UseManualCombatAssumptions { get; set; }
    // These explicit encounter hit assumptions remain provisional even when critical chances use prepared stats.
    public decimal BaseMeleeHitPercent { get; set; } = 92m;
    public decimal BaseRangedHitPercent { get; set; } = 92m;
    public decimal BaseSpellHitPercent { get; set; } = 83m;
    public decimal CriticalChancePercent { get; set; } = 5m;
    public decimal IncomingBlockChancePercent { get; set; } = 5m;
    public decimal IncomingCriticalChancePercent { get; set; }
    public decimal IncomingBossHitDamage { get; set; }
    public decimal IncomingAttackPower { get; set; }
    public decimal IncomingBossSwingSeconds { get; set; } = 3m;
    public string IncomingDamageSchool { get; set; } = "physical";
    public string IncomingAttackType { get; set; } = "melee";
    public bool FriendlyTargetEnabled { get; set; }
    public decimal FriendlyTargetMaximumHealth { get; set; } = 5000m;
    public decimal FriendlyTargetInitialHealthPercent { get; set; } = 50m;
    public decimal FriendlyTargetIncomingDamage { get; set; }
    public decimal FriendlyTargetIncomingIntervalSeconds { get; set; } = 3m;
    public string HealTarget { get; set; } = "self";
    public string OathTarget { get; set; } = "healing-target";
    public decimal HealBelowHealthPercent { get; set; } = 70m;
    public bool UseWindfuryTotem { get; set; }
    public decimal WindfuryTotemProcChancePercent { get; set; } = 20m;
    public List<string>? CustomPriority { get; set; }
    public PaladinUtilityScenario? UtilityScenario { get; set; }
}

/// <summary>Bounded, explicit conditions for control, cleansing and movement consumers.</summary>
public sealed class PaladinUtilityScenario
{
    public decimal InitialTargetDistanceYards { get; set; } = 3m;
    public bool MoveTowardTarget { get; set; }
    public bool Mounted { get; set; }
    public decimal BaseMovementSpeedYardsPerSecond { get; set; } = 7m;
    public decimal OtherMovementSpeedBonusPercent { get; set; }
    public decimal TargetFleeSpeedYardsPerSecond { get; set; }
    public List<PaladinControlEvent> Effects { get; set; } = [];
}
public sealed class PaladinControlEvent
{
    public decimal AtSeconds { get; set; } = 2m;
    public string Kind { get; set; } = "silence";
    public decimal DurationSeconds { get; set; } = 5m;
    public string Target { get; set; } = "paladin";
    public decimal DamagePerSecond { get; set; }
}

public sealed record PaladinPrecombatPreview(StatCollection EffectiveStats,decimal AttackPower,
    decimal SpellPower,decimal HealingPower,decimal MaximumMana,decimal MaximumHealth,object? Baseline,
    string SourceStatus,IReadOnlyList<string> ModelingNotes,PaladinRotationPreview Rotation,
    IReadOnlyList<string> AvailableActions,string WeaponKind,bool HasShield,PaladinSimRequest Request)
{
    public string ClassKey => "paladin";
    public bool ClassWide => true;
    public string StatPreviewScope => "paladin-shared-exact-precombat-source-provisional";
    public string RotationPreviewScope => "classwide-conditional-automatic-or-custom-priority";
}
public sealed record PaladinRotationPreview(IReadOnlyList<RotationEntry> Entries);

public sealed class PaladinSimResponse
{
    public string ClassKey { get; init; } = "paladin";
    public decimal Dps { get; init; }
    public decimal DamageDone { get; init; }
    public decimal HealingDone { get; init; }
    public decimal OverhealingDone { get; init; }
    public decimal AbsorptionDone { get; init; }
    public decimal DamageTaken { get; init; }
    public decimal BlockedDamage { get; init; }
    public decimal EndingMana { get; init; }
    public decimal MaximumMana { get; init; }
    public decimal MaximumHealth { get; init; }
    public decimal EndingMaximumMana { get; init; }
    public decimal EndingMaximumHealth { get; init; }
    public decimal EndingHealth { get; init; }
    public decimal FriendlyEndingHealth { get; init; }
    public decimal AttackPower { get; init; }
    public decimal SpellPower { get; init; }
    public decimal HealingPower { get; init; }
    public Dictionary<string,decimal> DamageByAbility { get; init; } = new();
    public Dictionary<string,int> CombatItemUses { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
