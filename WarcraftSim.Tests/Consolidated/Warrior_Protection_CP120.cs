using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class WarriorProtectionCp120Tests
{
    private static WarriorSimRequest Base() => new()
    {
        Preset = "protection", TalentMode = "legacy", EquipmentMode = "manual",
        Level = 60, RotationMode = "automatic", TankMode = true,
        EnableBossAutoAttacks = true, TankContinuousHealing = true,
        ShieldEquipped = true, IsTwoHanded = false, DualWield = false,
        BossFirstSwingSeconds = 0m, BossSwingSpeedSeconds = 1m,
        BossSwingMinimumDamage = 1000m, BossSwingMaximumDamage = 1000m,
        BossMeleeCritPercent = 0m, BossMeleeCrushPercent = 0m,
        PlayerDodgePercent = 0m, PlayerParryPercent = 0m,
        PlayerBlockPercent = 0m, TargetLevel = 60, TargetDodgePercent = 0m,
        DurationSeconds = 12m, Iterations = 1, Seed = 123456,
        StartingRage = 60m, MaximumHealth = 1500m,
        UseBloodrage = false, UseHeroicStrike = false,
        CaptureRepresentativeTimeline = true
    };

    [Fact]
    public void ProtectionRequiresShieldAndTankMode()
    {
        var r = Base(); r.ShieldEquipped = false;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(r));
        r = Base(); r.TankMode = false;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(r));
    }

    [Fact]
    public void AShieldCannotBePairedWithATwoHanderOrDualWield()
    {
        var r = Base(); r.IsTwoHanded = true;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(r));
        r = Base(); r.DualWield = true;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(r));
    }

    [Fact]
    public void ShieldCatalogResolvesHandBlockValueAndDefense()
    {
        var r = Base(); r.EquipmentMode = "catalog";
        r.Gear =
        [
            new WarriorGearEntry { Slot = "main-hand", ItemId = 279259 },
            new WarriorGearEntry { Slot = "off-hand", ItemId = 12602 }
        ];
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(r);
        Assert.True(resolved.ShieldEquipped);
        Assert.False(resolved.DualWield);
        Assert.False(resolved.IsTwoHanded);
        Assert.True(resolved.PlayerBlockValue >= r.PlayerBlockValue + 40m);
        var calculated = WarriorGearStatCalculator.Calculate(resolved);
        Assert.True(calculated.EffectiveStats.Get(WarcraftSim.Data.Forever.Combat.ForeverCombatStatKeys.DefenseSkill) >= 10m);
    }

    [Fact]
    public void ContinuousTankAnalysisRunsAllSwingsWithoutArtificialDeath()
    {
        var r = Base(); r.BossSwingMinimumDamage = 20000m; r.BossSwingMaximumDamage = 20000m;
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.Equal(0m, output.DeathRatePercent);
        Assert.False(output.Batch.RepresentativeRun.Summary.PrimaryActorDied);
        Assert.True(output.RepresentativeMaximumWindowPercentOfHealth > 100m);
        Assert.True(output.RepresentativeMaximumWindowDamage > r.MaximumHealth);
        Assert.True(output.MeanMaximumWindowPercentOfHealth > 100m);
        Assert.True(output.P95MaximumWindowPercentOfHealth >= output.MeanMaximumWindowPercentOfHealth);
        var swings = output.Batch.RepresentativeRun.Timeline.Count(ev =>
            ev.Type == CombatEventType.Damage && ev.AbilityKey == "boss-main-hand-auto");
        Assert.True(swings >= 7, $"Expected continuing attacks, observed {swings}.");
    }

    [Fact]
    public void FiniteHealthModeCanStillCauseDeath()
    {
        var r = Base(); r.TankContinuousHealing = false;
        r.BossSwingMinimumDamage = 50000m; r.BossSwingMaximumDamage = 50000m;
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.Equal(100m, output.DeathRatePercent);
    }

    [Fact]
    public void ShieldBlockIsAnActualCastWithCostAndCooldown()
    {
        var output = new ForeverWarriorSimRunner().Run(Base());
        Assert.Contains(output.Batch.RepresentativeRun.Timeline, ev =>
            ev.AbilityKey == "shield-block" && ev.Type == CombatEventType.AbilityCastCompleted);
    }

    [Fact]
    public void ShieldWallUsesRealCooldownAndReducesIncomingDamage()
    {
        var r = Base(); r.MajorCooldowns = [new WarriorMajorCooldownPolicy
        {
            AbilityKey = "shield-wall", Mode = "at-times", PlannedUseSeconds = [0m]
        }];
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(output.Batch.RepresentativeRun.Timeline, ev =>
            ev.AbilityKey == "shield-wall" && ev.Type == CombatEventType.AbilityCastCompleted);
        Assert.True(output.MeanDamageTakenPerSecond > 0m);
    }

    [Fact]
    public void LastStandRequiresTalent()
    {
        var r = Base(); r.MajorCooldowns = [new WarriorMajorCooldownPolicy
        {
            AbilityKey = "last-stand", Mode = "at-times", PlannedUseSeconds = [0m]
        }];
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(r));
    }

    [Fact]
    public void DamageWindowMustBeValid()
    {
        var r = Base(); r.TankDamageWindowSeconds = 0m;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(r));
    }

    private static WarriorSimRequest TalentBuild()
    {
        var r = Base();
        r.TalentMode = "tree";
        r.TalentRanks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["improved-bloodrage"] = 2, ["shield-specialization"] = 5,
            ["iron-will"] = 5, ["anticipation"] = 5,
            ["improved-revenge"] = 3, ["improved-thunder-clap"] = 3,
            ["last-stand"] = 1, ["master-of-defense"] = 2,
            ["improved-disarm"] = 3, ["defiance"] = 3,
            ["improved-sunder-armor"] = 3, ["vanguard"] = 1,
            ["improved-shield-bash"] = 2, ["improved-shield-wall"] = 2,
            ["concussion-blow"] = 1, ["focused-rage"] = 3,
            ["bastion"] = 5, ["shield-slam"] = 1
        };
        return r;
    }

    [Fact]
    public void FullyAllocatedProtectionBuildIsValid()
    {
        var r = TalentBuild();
        ForeverWarriorSimRunner.Validate(r);
        Assert.Equal(50, r.TalentRanks.Values.Sum());
    }

    [Fact]
    public void ProtectionTalentUnlocksShieldSlam()
    {
        var r = TalentBuild();
        var result = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline, ev =>
            ev.AbilityKey == "shield-slam" && ev.Type == CombatEventType.AbilityCastCompleted);
    }

    [Fact]
    public void ImprovedShieldWallReducesItsRegisteredCooldown()
    {
        var r = TalentBuild();
        var ability = WarriorProtectionAbilityFactory.Create(r)
            .Single(x => x.Key == "shield-wall");
        Assert.Equal(240m, ability.CooldownSeconds);
    }

    [Fact]
    public void ProtectionWithNoBossAttacksIsRejected()
    {
        var r = Base(); r.EnableBossAutoAttacks = false;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(r));
    }
}
