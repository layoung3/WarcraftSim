namespace WarcraftSim.Data.Forever.Rogue;

/// <summary>
/// Provisional level-60 base damage and costs; NOT verified client coefficients.
/// Mechanical rules documented as verified should not be changed to balance DPS.
/// </summary>
public sealed class ForeverRogueTuning
{
    // CP147: all values below are editable beta assumptions unless confirmed
    // in the official Forever class deep dive (which specifies behavior only).
    public decimal HackAndSlashExtraAttackPercentPerRank { get; set; } = 1m;
    public decimal HackAndSlashDaggerCritPercentPerRank { get; set; } = 1m;
    public decimal HackAndSlashFistCritPercentPerRank { get; set; } = 1m;
    public decimal HackAndSlashMaceArmorIgnorePercentPerRank { get; set; } = 3m;
    public decimal WeaponExpertiseAvoidanceReductionPerRank { get; set; } = 1m;
    public decimal LightningReflexesDodgePercentPerRank { get; set; } = 1m;
    public decimal FleetFootedDodgePercentPerRank { get; set; } = 1m;
    public decimal BaseParryPercent { get; set; } = 0m;
    // Explicit confirmed direction from Rogue deep dive: Deflection grants
    // twice the original parry bonus; exact combat-table treatment provisional.
    public decimal DeflectionParryPercentPerRank { get; set; } = 2m;
    public decimal MasterOfSubtletyDamagePercentPerRank { get; set; } = 3m;
    public decimal MasterOfSubtletyDurationSeconds { get; set; } = 6m;
    public decimal QuickRecoveryEnergyRefundPercentPerRank { get; set; } = 40m;
    public decimal EnergyPerSecond { get; set; } = 10m;
    public decimal MainHandSpeedSeconds { get; set; } = 2.6m;
    public decimal OffHandSpeedSeconds { get; set; } = 1.8m;
    public decimal MainHandWeaponDamage { get; set; } = 125m;
    public decimal OffHandWeaponDamage { get; set; } = 80m;
    public decimal SinisterStrikeBonusDamage { get; set; } = 68m;
    public decimal BackstabBonusDamage { get; set; } = 85m;
    public decimal HemorrhageBonusDamage { get; set; } = 45m;
    public decimal MutilateBonusDamagePerHand { get; set; } = 17.25m;
    // Illustrative CP143 proc/crit parameters pending level-60 verification.
    public decimal PuncturingWoundsBackstabCritPerRank { get; set; } = 10m;
    public decimal PuncturingWoundsMutilateCritPerRank { get; set; } = 5m;
    public decimal PuncturingWoundsExtraPointChancePerRank { get; set; } = 10m;
    public decimal AmbushBonusDamage { get; set; } = 150m;
    public decimal GarroteTickDamage { get; set; } = 65m;
    public decimal EviscerateDamagePerPoint { get; set; } = 160m;
    public decimal RuptureTickDamagePerPoint { get; set; } = 24m;
    public decimal PoisonDamage { get; set; } = 90m;
    // Explicit assumptions; official class article confirms the affected abilities
    // but does not provide endgame numerical modifiers.
    public decimal LethalityBonusCriticalDamagePerRank { get; set; } = .04m;
    public decimal ImprovedGarroteDamagePerRank { get; set; } = .05m;
    public decimal ImprovedKidneyShotDamagePerRank { get; set; } = .03m;
    // Removed Deadliness rank no longer influences the active game model.
    // Defense and threat amounts are configurable illustrative assumptions.
    public decimal FeintThreatReduction { get; set; } = 800m;
    public decimal BaseDodgePercent { get; set; } = 5m;
    public decimal EvasionBonusDodgePercent { get; set; } = 50m;
    public decimal ImprovedExposeArmorEnergyDiscountPerRank { get; set; } = 5m;
    // Official effect: refunds Combo Points on a five-point Expose Armor.
    // Exact number refunded is not given in the class overview; tune from client data.
    public int ImprovedExposeArmorFivePointRefund { get; set; } = 5;
    // Forever client: Improved Poisons preserves a charge 10% per talent rank.
    public decimal PoisonChargePreservationChancePerRank { get; set; } = 10m;
    public decimal InstantPoisonChancePercent { get; set; } = 20m;
    public decimal ArmorReductionPerExposeComboPoint { get; set; } = 250m;
    public decimal ArmorDamageReductionConstant { get; set; } = 5500m;
    public decimal DeadlyPoisonDamagePerStack { get; set; } = 20m;
    public decimal DeadlyPoisonDurationSeconds { get; set; } = 12m;
    public decimal DeadlyPoisonTickIntervalSeconds { get; set; } = 3m;
    public decimal UtilityPoisonDurationSeconds { get; set; } = 12m;
    // Healing-received reduction from Wound Poison; deliberately provisional
    // until a verified Forever rank tooltip or combat log is available.
    public decimal WoundPoisonHealingReductionPercent { get; set; } = 50m;
    // Ability cast-time slow from Mind-numbing Poison; beta approximation.
    public decimal MindNumbingPoisonCastTimeMultiplier { get; set; } = 1.3m;
    public decimal MainHandHitPercent { get; set; } = 82m;
    public decimal OffHandHitPercent { get; set; } = 65m;
    public decimal PhysicalCritPercent { get; set; } = 20m;
    public decimal SliceAndDiceHastePercent { get; set; } = 30m;
    public decimal AdrenalineRushDurationSeconds { get; set; } = 15m;

    public void Validate()
    {
        var values = new[] {
            HackAndSlashExtraAttackPercentPerRank, HackAndSlashDaggerCritPercentPerRank,
            HackAndSlashFistCritPercentPerRank, HackAndSlashMaceArmorIgnorePercentPerRank,
            WeaponExpertiseAvoidanceReductionPerRank, LightningReflexesDodgePercentPerRank,
            FleetFootedDodgePercentPerRank, BaseParryPercent, DeflectionParryPercentPerRank,
            MasterOfSubtletyDamagePercentPerRank, MasterOfSubtletyDurationSeconds,
            QuickRecoveryEnergyRefundPercentPerRank, EnergyPerSecond, MainHandSpeedSeconds, OffHandSpeedSeconds,
            MainHandWeaponDamage, OffHandWeaponDamage, SinisterStrikeBonusDamage,
            BackstabBonusDamage, HemorrhageBonusDamage, MutilateBonusDamagePerHand,
            PuncturingWoundsBackstabCritPerRank, PuncturingWoundsMutilateCritPerRank,
            PuncturingWoundsExtraPointChancePerRank, AmbushBonusDamage, GarroteTickDamage,
            EviscerateDamagePerPoint, RuptureTickDamagePerPoint, PoisonDamage,
            PoisonChargePreservationChancePerRank,
            LethalityBonusCriticalDamagePerRank, ImprovedGarroteDamagePerRank,
            ImprovedKidneyShotDamagePerRank, FeintThreatReduction,
            BaseDodgePercent, EvasionBonusDodgePercent,
            ImprovedExposeArmorEnergyDiscountPerRank,
            InstantPoisonChancePercent, ArmorReductionPerExposeComboPoint,
            ArmorDamageReductionConstant, DeadlyPoisonDamagePerStack,
            DeadlyPoisonDurationSeconds, DeadlyPoisonTickIntervalSeconds,
            UtilityPoisonDurationSeconds, WoundPoisonHealingReductionPercent, MindNumbingPoisonCastTimeMultiplier, MainHandHitPercent, OffHandHitPercent,
            PhysicalCritPercent, SliceAndDiceHastePercent, AdrenalineRushDurationSeconds };
        if (MasterOfSubtletyDurationSeconds <= 0m ||
            HackAndSlashExtraAttackPercentPerRank > 20m ||
            HackAndSlashMaceArmorIgnorePercentPerRank > 20m ||
            QuickRecoveryEnergyRefundPercentPerRank > 50m ||
            ImprovedExposeArmorFivePointRefund is < 0 or > 5 ||
            values.Any(x => x < 0m) || MainHandSpeedSeconds <= 0m || OffHandSpeedSeconds <= 0m ||
            EnergyPerSecond <= 0m || AdrenalineRushDurationSeconds <= 0m ||
            DeadlyPoisonDurationSeconds <= 0m || DeadlyPoisonTickIntervalSeconds <= 0m ||
            ArmorDamageReductionConstant <= 0m ||
            WoundPoisonHealingReductionPercent > 100m || MindNumbingPoisonCastTimeMultiplier < 1m || MindNumbingPoisonCastTimeMultiplier > 3m ||
            InstantPoisonChancePercent > 100m || PoisonChargePreservationChancePerRank > 100m || PuncturingWoundsBackstabCritPerRank > 100m ||
            PuncturingWoundsMutilateCritPerRank > 100m ||
            PuncturingWoundsExtraPointChancePerRank > 100m || MainHandHitPercent > 100m ||
            OffHandHitPercent > 100m || PhysicalCritPercent > 100m ||
            BaseDodgePercent > 100m || EvasionBonusDodgePercent > 100m ||
            BaseParryPercent > 100m || DeflectionParryPercentPerRank > 20m)
            throw new ArgumentException("Invalid level-60 Rogue tuning values.");
    }
}
