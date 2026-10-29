using WarcraftSim.Core.Characters;
using WarcraftSim.Data.Forever.Priest;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// CP125 controlled healing fixture. Values are CALIBRATION INPUTS, not
/// claimed level-60 Forever client spell ranks or talent calculations.
/// </summary>
public sealed class HolyPriestPreviewRequest
{
    // CP127 opt-ins: CP125/126 imports retain their prior behavior by default.
    public bool UseCharacterDerivedPriestStats { get; set; }
    public CharacterProfile? PriestCharacter { get; set; }
    public ForeverPriestStatAssumptions PriestStatAssumptions { get; set; } = new();
    public bool UsePriestTalentBuild { get; set; }
    public bool UseMajorCooldowns { get; set; } // opt-in until loadout-aware cooldown optimization
    public ForeverPriestHealingTalentBuild PriestTalents { get; set; } = new();
    public bool UseAdaptiveHealingPolicy { get; set; }
    // CP127.3 opt-in. When active, class spec determines the priority list.
    // Old imports continue to use their existing adaptive behavior.
    public bool UseSpecializationHealingPolicy { get; set; }
    // Repeating scripted raid damage, independent of boss white swings.
    // Off by default for exact CP125-CP127.2 reproducibility.
    public decimal RaidPulseDamage { get; set; }
    public decimal RaidPulseIntervalSeconds { get; set; } = 12m;
    public decimal RaidPulseFirstSeconds { get; set; } = 6m;
    public int RaidPulseTargetCount { get; set; } = 3;
    public bool RaidPulseIncludePriest { get; set; }
    public decimal ForecastHorizonSeconds { get; set; } = 2m;
    public decimal MinimumSecondaryTargetManaPercent { get; set; } = 25m;
    // CP126 opt-in: CP125 exports remain on the old deterministic fixture.
    public bool EnableExtendedPriest { get; set; }
    public bool EnableHealingCriticals { get; set; } = true;
    public decimal PriestHealingCritPercent { get; set; } = 12m;
    public decimal PriestSpiritRegenPerSecond { get; set; } = 10m;
    public decimal PriestMp5 { get; set; } = 25m;
    public decimal MeditationCastingSpiritFraction { get; set; } = 0.5m;
    public decimal HealingThreatCoefficient { get; set; } = 0.5m;
    public int AdditionalRaidMembers { get; set; } = 3;
    public decimal RaidMemberMaximumHealth { get; set; } = 2500m;
    public decimal PriestInitialHealthPercent { get; set; } = 100m;
    public decimal RaidMemberInitialHealthPercent { get; set; } = 65m;
    public decimal RenewTickAmount { get; set; } = 145m;
    public decimal ShieldAbsorbAmount { get; set; } = 650m;
    public decimal PrayerOfMendingAmount { get; set; } = 250m;
    public decimal PrayerOfHealingAmount { get; set; } = 540m;
    public decimal BindingHealAmount { get; set; } = 590m;
    public decimal PenanceTickAmount { get; set; } = 280m;
    public bool UsePrayerOfMending { get; set; } = true;
    public bool UsePowerWordShield { get; set; } = true;
    public bool UseRenew { get; set; } = true;
    public bool UseBindingHeal { get; set; } = true;
    public bool UsePrayerOfHealing { get; set; } = true;
    public bool UsePenance { get; set; } = true;
    public bool UseDivineAegis { get; set; } = true;
    public decimal DivineAegisFraction { get; set; } = 0.15m;
    public int ImprovedRenewRanks { get; set; } = 3;
    public int ImprovedShieldRanks { get; set; } = 3;
    public int SpiritualHealingRanks { get; set; } = 3;
    public int DivineFuryRanks { get; set; } = 5;
    public int ImprovedHealingRanks { get; set; } = 3;
    public int TwinDisciplinesRanks { get; set; } = 0;
    public bool SoulWarding { get; set; } = false;
    public decimal LitanyRefundFraction { get; set; } = 0.10m;

    public int Seed { get; set; } = 9924;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public bool EnableHealer { get; set; } = true;
    public decimal TankMaximumHealth { get; set; } = 3000m;
    public decimal BossSwingDamage { get; set; } = 360m;
    public decimal BossSwingIntervalSeconds { get; set; } = 2m;
    public decimal HealerMaximumHealth { get; set; } = 2000m;
    public decimal PriestMaximumMana { get; set; } = 5000m;
    public decimal PriestManaRegenPerSecond { get; set; } = 12m;
    public decimal FlashHealAmount { get; set; } = 680m;
    public decimal FlashHealManaCost { get; set; } = 180m;
    public decimal FlashHealCastSeconds { get; set; } = 1.5m;
    public decimal GreaterHealAmount { get; set; } = 1550m;
    public decimal GreaterHealManaCost { get; set; } = 410m;
    public decimal GreaterHealCastSeconds { get; set; } = 2.5m;
    public decimal GreaterHealHealthThresholdPercent { get; set; } = 50m;
    public decimal FlashHealHealthThresholdPercent { get; set; } = 85m;
}
