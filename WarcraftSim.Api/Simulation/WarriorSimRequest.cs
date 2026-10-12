namespace WarcraftSim.Api.Simulation;

/// <summary>
/// First manually configured, level-60 Forever Warrior simulation.
/// All combat stats are final character-sheet values, not item IDs or raw gear stats.
/// Coefficients marked provisional must not be interpreted as verified beta values.
/// </summary>
public sealed class WarriorSimRequest
{
    public string Preset { get; set; } = "arms";
    // "automatic": starter priority derived from selected build, or "custom": explicit action list.
    public string RotationMode { get; set; } = "automatic";
    public List<WarriorRotationRule> CustomRotation { get; set; } = [];

    public int Level { get; set; } = 60;
    public decimal DurationSeconds { get; set; } = 180m;
    // Timed single-character DPS: by default the final 30 seconds are the <=20% phase.
    // Zero disables execute phase. Explicit checkpoints override this shortcut.
    public decimal ExecutePhaseDurationSeconds { get; set; } = 30m;
    public List<WarriorTargetHealthMilestone> TargetHealthMilestones { get; set; } = [];
    public int Iterations { get; set; } = 100;
    public int Seed { get; set; } = 12345;
    public bool CaptureRepresentativeTimeline { get; set; }

    // A fresh pull gives each auto-attack stream an opening attack at t=0.
    // False preserves the legacy first-swing wait for A/B comparisons.
    public bool ImmediateOpeningSwing { get; set; } = true;

    // Rear-arc melee attacks cannot be parried or blocked; NPC dodge remains.
    public bool BehindTarget { get; set; } = true;

    // Optional additive gear/enchant bonuses on top of the unbuffed baseline.
    // 'manual' preserves all CP108 character-sheet behavior.
    // manual | gear-bonuses | catalog (server-owned item/enchant IDs)
    public string EquipmentMode { get; set; } = "manual";
    public List<WarriorGearEntry> Gear { get; set; } = [];


    public decimal MainHandMinimumDamage { get; set; } = 160m;
    public decimal MainHandMaximumDamage { get; set; } = 250m;
    public decimal MainHandSpeedSeconds { get; set; } = 3.5m;
    public bool IsTwoHanded { get; set; } = true;

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
