using WarcraftSim.Core.Encounters;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// First manually configured, level-60 Forever Warrior simulation.
/// All combat stats are final character-sheet values, not item IDs or raw gear stats.
/// Coefficients marked provisional must not be interpreted as verified beta values.
/// </summary>
public sealed class WarriorSimRequest
{
    public string Preset { get; set; } = "arms";
    // Legacy preserves all CP111 saved configurations. New builds can opt in
    // to a validated three-tree talent allocation and race-derived base stats.
    public string TalentMode { get; set; } = "legacy"; // legacy | tree
    public Dictionary<string, int> TalentRanks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string StatSourceMode { get; set; } = "manual"; // manual | race-base
    public string Race { get; set; } = "human";

    // "automatic": starter priority derived from selected build, or "custom": explicit action list.
    public string RotationMode { get; set; } = "automatic";
    public List<WarriorRotationRule> CustomRotation { get; set; } = [];

    // Optional in-combat assignments. An empty list preserves CP113 static support.
    public List<WarriorMaintenancePolicy> MaintenancePolicies { get; set; } = [];
    // Separate from GCD priority: can be scheduled/held for burst alignment.
    public List<WarriorMajorCooldownPolicy> MajorCooldowns { get; set; } = [];

    // Fury uses this to capitalize on target-specific Overpower procs.
    // Arms can also initiate Whirlwind dances from an eligible custom priority.
    // Stance-dependent abilities always require the correct aura.
    public bool EnableStanceDancing { get; set; }
    public decimal MaximumRageForOverpowerDance { get; set; } = 20m;
    // Legacy JSON field name retained for imports. This is a shared stance
    // cooldown, independent from the normal ability GCD. Switching still caps Rage.
    public decimal StanceSwitchGlobalCooldownSeconds { get; set; } = 1.5m;


    public int Level { get; set; } = 60;
    public decimal DurationSeconds { get; set; } = 180m;
    // Timed single-character DPS: by default the final 30 seconds are the <=20% phase.
    // Zero disables execute phase. Explicit checkpoints override this shortcut.
    public decimal ExecutePhaseDurationSeconds { get; set; } = 30m;
    public List<WarriorTargetHealthMilestone> TargetHealthMilestones { get; set; } = [];
    // Optional scripted adds; all are within Whirlwind range when active.
    public List<WarriorAdditionalTarget> AdditionalTargets { get; set; } = [];

    // Opt-in attacking-boss preview. Disabled by default for exact CP118 compatibility.
    // These encounter inputs are deliberately exposed instead of claiming that the
    // unverified Forever level-63 avoidance/critical/crushing table is authoritative.
    public bool EnableBossAutoAttacks { get; set; }
    // Opt-in class-neutral encounter abilities. Empty preserves CP121 tank baseline.
    // The Warrior preview uses 'target' as the boss actor and 'warrior' as the player.
    public List<EncounterBossAbilityDefinition> BossAbilities { get; set; } = [];
    public bool TankMode { get; set; }
    // Legacy JSON import only. Solo preview encounters always run to full duration;
    // this field is deliberately ignored. Real healer/death models need separate actors.
    public bool TankContinuousHealing { get; set; } = true;
    // A parry reduces the remaining time until the defender's next main-hand swing.
    // Some raid bosses disable parry haste; keep this a per-encounter option.
    public bool BossParryHaste { get; set; } = true;
    public decimal TankDamageWindowSeconds { get; set; } = 6m;
    public decimal TankDamageWarningPercent { get; set; } = 50m;
    // A shield is derived server-side in catalog mode. Manual mode uses this input.
    public bool ShieldEquipped { get; set; }

    public decimal BossFirstSwingSeconds { get; set; } = 2m;
    public decimal BossSwingSpeedSeconds { get; set; } = 2m;
    public decimal BossSwingMinimumDamage { get; set; } = 750m;
    public decimal BossSwingMaximumDamage { get; set; } = 1100m;
    public decimal BossMeleeCritPercent { get; set; } = 5m;
    public decimal BossMeleeCrushPercent { get; set; }
    public decimal PlayerDodgePercent { get; set; } = 5m;
    public decimal PlayerParryPercent { get; set; } = 5m;
    public bool PlayerParryHaste { get; set; } = true;
    public decimal PlayerBlockPercent { get; set; } = 5m;
    public decimal PlayerBlockValue { get; set; } = 60m;
    // Calibrations for the already-existing Forever incoming-damage Rage model.
    public decimal IncomingRageExpectedCreatureHealth { get; set; } = 10000m;
    public decimal IncomingRageExpectedArmorReductionPercent { get; set; } = 50m;

    public int Iterations { get; set; } = 100;
    public int Seed { get; set; } = 12345;
    public bool CaptureRepresentativeTimeline { get; set; }

    // A fresh pull gives each auto-attack stream an opening attack at t=0.
    // False preserves the legacy first-swing wait for A/B comparisons.
    public bool ImmediateOpeningSwing { get; set; } = true;
    // Delay from pull until first MH/OH auto, 0.00s by default.
    // ImmediateOpeningSwing=false remains a legacy full-speed first swing.
    public decimal OpeningAutoAttackSeconds { get; set; } = 0m;

    // Rear-arc melee attacks cannot be parried or blocked; NPC dodge remains.
    public bool BehindTarget { get; set; } = true;

    // Optional additive gear/enchant bonuses on top of the unbuffed baseline.
    // 'manual' preserves all CP108 character-sheet behavior.
    // manual | gear-bonuses | catalog (server-owned item/enchant IDs)
    public string EquipmentMode { get; set; } = "manual";
    public List<WarriorGearEntry> Gear { get; set; } = [];

    // Static pre-pull raid support snapshot, applied to the existing character
    // buff pipeline; unsupported active procs cannot be selected.
    public List<string> RaidBuffIds { get; set; } = [];
    public List<string> ConsumableIds { get; set; } = [];
    public List<string> BossDebuffIds { get; set; } = [];
    public int SunderArmorStacks { get; set; } = 5;


    public decimal MainHandMinimumDamage { get; set; } = 160m;
    public decimal MainHandMaximumDamage { get; set; } = 250m;
    public decimal MainHandSpeedSeconds { get; set; } = 3.5m;
    public bool IsTwoHanded { get; set; } = true;

    // Resolved from server-owned item catalog, never from freeform bonus stats.
    public string MainHandWeaponType { get; set; } = "";
    public string OffHandWeaponType { get; set; } = "";
    public bool DualWield { get; set; }
    public decimal OffHandMinimumDamage { get; set; } = 75m;
    public decimal OffHandMaximumDamage { get; set; } = 110m;
    public decimal OffHandSpeedSeconds { get; set; } = 2m;

    public decimal AttackPower { get; set; } = 1200m;
    public decimal MeleeCritPercent { get; set; } = 20m;
    public decimal MeleeHitPercent { get; set; } = 4m;
    public decimal MeleeHastePercent { get; set; }
    public decimal OffHandHitPercent { get; set; }
    public decimal OffHandDamagePercent { get; set; }
    public decimal MainHandWeaponSkill { get; set; } = 300m;
    public decimal OffHandWeaponSkill { get; set; } = 300m;
    public decimal MaximumHealth { get; set; } = 5000m;
    public decimal StartingRage { get; set; }

    // Humanoid, giant, dragonkin, or mounted for Spearing Strike bonus.
    public string TargetCreatureType { get; set; } = "humanoid";
    public int TargetLevel { get; set; } = 63;
    public decimal TargetArmor { get; set; } = 3500m;
    public decimal TargetDodgePercent { get; set; } = 5m;
    public decimal TargetParryPercent { get; set; }
    public decimal TargetBlockPercent { get; set; }

    // Deliberately EXPLICIT provisional model inputs. The exact Forever
    // glancing-damage penalty and Armor calibration are not yet verified.
    public decimal ProvisionalGlancingDamageMultiplier { get; set; } = 0.70m;
    public decimal ProvisionalArmorBaseConstant { get; set; } = 400m;
    public decimal ProvisionalArmorPerAttackerLevelConstant { get; set; } = 85m;
    public decimal RendTotalAttackPowerCoefficient { get; set; }

    public int DeepWoundsRank { get; set; } = 3;
    public int ImprovedOverpowerRank { get; set; } = 2;
    public int ImprovedRendRank { get; set; } = 3;
    public int ImpaleRank { get; set; } = 2;
    public int BloodthrillRank { get; set; }

    public bool UseSlam { get; set; }
    public bool UseCleave { get; set; }
    public bool UseBloodrage { get; set; } = true;
    public bool UseRend { get; set; } = true;
    public bool UseExecute { get; set; } = true;
    public bool UseHeroicStrike { get; set; } = true;
    public decimal HeroicStrikeMinimumRage { get; set; } = 65m;

    internal WarriorSimRequest ShallowCopy() => (WarriorSimRequest)MemberwiseClone();
}

/// <summary>A scripted, non-interpolated boss-health checkpoint in a timed character simulation.</summary>
public sealed class WarriorTargetHealthMilestone
{
    public decimal TimeSeconds { get; set; }
    public decimal HealthPercent { get; set; }
}
