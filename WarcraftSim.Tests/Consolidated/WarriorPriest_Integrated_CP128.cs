using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class WarriorPriestIntegratedCp128Tests
{
    private static WarriorPriestIntegratedRequest Setup(decimal bossDamage = 240m,
        decimal duration = 28m)
    {
        var warrior = new WarriorSimRequest
        {
            Preset = "protection", EquipmentMode = "manual", TalentMode = "legacy",
            ShieldEquipped = true, TankMode = true, EnableBossAutoAttacks = true,
            IsTwoHanded = false, DualWield = false,
            DurationSeconds = duration, Iterations = 1, Seed = 300128,
            BossFirstSwingSeconds = 1m, BossSwingSpeedSeconds = 2m,
            BossSwingMinimumDamage = bossDamage, BossSwingMaximumDamage = bossDamage,
            PlayerDodgePercent = 0m, PlayerParryPercent = 0m,
            PlayerBlockPercent = 0m, BossMeleeCritPercent = 0m,
            BossMeleeCrushPercent = 0m, BossParryHaste = false,
            UseBloodrage = false, StartingRage = 50m,
            TargetDodgePercent = 0m, TargetParryPercent = 0m,
            CaptureRepresentativeTimeline = true,
            MaximumHealth = 3000m
        };
        var priest = new HolyPriestPreviewRequest
        {
            DurationSeconds = duration, Seed = 300128, CaptureTimeline = true,
            EnableHealer = true, EnableExtendedPriest = true,
            UseCharacterDerivedPriestStats = true,
            UsePriestTalentBuild = true,
            UseAdaptiveHealingPolicy = true,
            UseSpecializationHealingPolicy = true,
            UsePrayerOfMending = false, UsePenance = false,
            UsePowerWordShield = true, UseRenew = false,
            UsePrayerOfHealing = false, UseBindingHeal = false,
            HealerMaximumHealth = 2000m,
            PriestInitialHealthPercent = 100m,
            AdditionalRaidMembers = 0, RaidPulseDamage = 0m,
            PriestCharacter = new CharacterProfile
            {
                Name = "Test Priest", ClassKey = "priest", SpecializationKey = "discipline",
                RulesetKey = "wow-forever", Level = 60,
                BaseStats = new StatCollection { Values = new Dictionary<string, decimal>
                {
                    ["intellect"] = 190m, ["spirit"] = 210m
                } }
            }
        };
        return new WarriorPriestIntegratedRequest { Warrior = warrior, Priest = priest };
    }

    [Fact]
    public void BothClassesActInOneTimeline_AndRealBossDamageConsumesHealth()
    {
        var result = new ForeverWarriorSimRunner().RunIntegrated(Setup());
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.SourceActorKey == "warrior" && e.TargetActorKey == "target");
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.SourceActorKey == "target" && e.TargetActorKey == "warrior");
        Assert.Contains(result.Run.Timeline, e => e.SourceActorKey == "priest" &&
            (e.Type == CombatEventType.Healing || e.AbilityKey == "power-word-shield"));
        Assert.True(result.BossMeleeDamageDone > 0m);
        Assert.True(result.TankThreat > 0m);
        Assert.True(result.PriestMaximumMana > result.PriestEndingMana);
    }

    [Fact]
    public void SharedShieldsAbsorbRealBossSwings()
    {
        var result = new ForeverWarriorSimRunner().RunIntegrated(Setup());
        Assert.True(result.PriestAbsorptionDone > 0m);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "boss-main-hand-auto" &&
            (e.AbsorbedAmount ?? 0m) > 0m);
    }

    [Fact]
    public void IncreasedPressureCanKillTheTank_AndBossStopsHittingDeadActor()
    {
        var config = Setup(bossDamage: 20000m, duration: 15m);
        config.Priest.UsePowerWordShield = false;
        var result = new ForeverWarriorSimRunner().RunIntegrated(config);
        Assert.False(result.TankSurvived);
        Assert.Equal(0m, result.TankEndingHealth);
        Assert.NotNull(result.TankDeathTimeSeconds);
        Assert.DoesNotContain(result.Run.Timeline, e =>
            e.Type == CombatEventType.Damage && e.SourceActorKey == "target" &&
            e.TargetActorKey == "warrior" &&
            e.TimeSeconds > result.TankDeathTimeSeconds!.Value);
    }

    [Fact]
    public void RaidPulsesAndPriestActionsOccurWithinSharedEngine()
    {
        var config = Setup(duration: 24m);
        config.Priest.AdditionalRaidMembers = 2;
        config.Priest.RaidPulseDamage = 140m;
        config.Priest.RaidPulseIncludePriest = true;
        var result = new ForeverWarriorSimRunner().RunIntegrated(config);
        Assert.Contains(result.Run.Timeline, e =>
            e.EncounterEventKey == "healer-raid-pulse" &&
            e.Type == CombatEventType.Damage && e.TargetActorKey == "raid-1");
        Assert.Contains(result.Run.Timeline, e =>
            e.EncounterEventKey == "healer-raid-pulse" &&
            e.Type == CombatEventType.Damage && e.TargetActorKey == "priest");
    }

    [Fact]
    public void MismatchedLengthsAreRejected()
    {
        var config = Setup();
        config.Priest.DurationSeconds++;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().RunIntegrated(config));
    }

    [Fact]
    public void RequiresProtectionAndLearnedPriestMode()
    {
        var config = Setup();
        config.Warrior.Preset = "fury";
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().RunIntegrated(config));
        config = Setup();
        config.Priest.UseSpecializationHealingPolicy = false;
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().RunIntegrated(config));
    }
}
