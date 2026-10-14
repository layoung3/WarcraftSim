using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class WarriorEnemyCombatCp119Tests
{
    private static WarriorSimRequest Base() => new()
    {
        Preset = "fury", Level = 60, DurationSeconds = 12m,
        Iterations = 1, Seed = 23014, MaximumHealth = 50000m,
        StartingRage = 0m, CaptureRepresentativeTimeline = true,
        // Keep the original manual/no-catalog regression path.
        TalentMode = "legacy", EquipmentMode = "manual"
    };

    [Fact]
    public void DisabledBossAttacks_PreserveNonAttackingPreview()
    {
        var result = new ForeverWarriorSimRunner().Run(Base());
        Assert.Equal(0m, result.MeanDamageTakenPerSecond);
        Assert.Equal(0m, result.DeathRatePercent);
        Assert.DoesNotContain(result.Batch.RepresentativeRun.Timeline,
            ev => ev.AbilityKey == "boss-main-hand-auto");
    }

    [Fact]
    public void BossMelee_EmitsIncomingDamageAndTracksThreat()
    {
        var request = Base();
        request.EnableBossAutoAttacks = true;
        request.BossFirstSwingSeconds = 0m;
        request.BossSwingSpeedSeconds = 1m;
        request.BossSwingMinimumDamage = 400m;
        request.BossSwingMaximumDamage = 400m;
        request.BossMeleeCritPercent = 0m;
        request.PlayerDodgePercent = 0m;
        request.PlayerParryPercent = 0m;
        request.PlayerBlockPercent = 0m;
        request.IncomingRageExpectedCreatureHealth = 1000m;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.True(result.MeanDamageTakenPerSecond > 0m);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline,
            ev => ev.Type == CombatEventType.Damage &&
                  ev.AbilityKey == "boss-main-hand-auto" &&
                  ev.TargetActorKey == "warrior" && (ev.Amount ?? 0m) > 0m);
        Assert.True(result.RepresentativeBossThreat > 0m);
        Assert.True(result.Batch.RepresentativeRun.CustomMetrics.ContainsKey("warrior-health-remaining"));
    }

    [Fact]
    public void ExcessiveBossDamage_DoesNotStopSoloEncounter()
    {
        var request = Base();
        request.EnableBossAutoAttacks = true;
        request.TankMode = true;
        request.TankContinuousHealing = false;
        request.MaximumHealth = 100m;
        request.BossFirstSwingSeconds = 0m;
        request.BossSwingSpeedSeconds = 0.5m;
        request.BossSwingMinimumDamage = 50000m;
        request.BossSwingMaximumDamage = 50000m;
        request.PlayerDodgePercent = 0m;
        request.PlayerParryPercent = 0m;
        request.PlayerBlockPercent = 0m;
        request.BossMeleeCritPercent = 0m;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(0m, result.DeathRatePercent);
        Assert.False(result.Batch.RepresentativeRun.Summary.PrimaryActorDied);
        Assert.True(result.MeanDamageTakenPerSecond > 0m);
        var bossSwings = result.Batch.RepresentativeRun.Timeline.Count(ev =>
            ev.AbilityKey == "boss-main-hand-auto" && ev.Type == CombatEventType.Damage);
        Assert.True(bossSwings >= 20, $"Expected attacks through the entire 12s encounter: {bossSwings}");
    }

    [Theory]
    [InlineData(-1, 800)]
    [InlineData(1000, 900)]
    public void InvalidBossDamageInputs_AreRejected(int minimum, int maximum)
    {
        var request = Base();
        request.EnableBossAutoAttacks = true;
        request.BossSwingMinimumDamage = minimum;
        request.BossSwingMaximumDamage = maximum;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void TankModeWithoutIncomingAttacks_IsRejected()
    {
        var request = Base();
        request.TankMode = true;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void IncomingBossSimulation_IsRepeatableForSameSeed()
    {
        var request = Base();
        request.EnableBossAutoAttacks = true;
        request.BossFirstSwingSeconds = 0m;
        request.BossSwingSpeedSeconds = 1m;
        var one = new ForeverWarriorSimRunner().Run(request);
        var two = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(one.MeanDamageTakenPerSecond, two.MeanDamageTakenPerSecond);
        Assert.Equal(one.DeathRatePercent, two.DeathRatePercent);
        Assert.Equal(one.RepresentativeBossThreat, two.RepresentativeBossThreat);
    }

    [Fact]
    public void DefensiveTankStance_ReducesIncomingRawSwingVersusBerserker()
    {
        var request = Base();
        request.EnableBossAutoAttacks = true;
        request.BossFirstSwingSeconds = 0m;
        request.BossSwingSpeedSeconds = 1m;
        request.BossSwingMinimumDamage = 500m;
        request.BossSwingMaximumDamage = 500m;
        request.BossMeleeCritPercent = 0m;
        request.PlayerDodgePercent = 0m;
        request.PlayerParryPercent = 0m;
        request.PlayerBlockPercent = 0m;
        var dps = new ForeverWarriorSimRunner().Run(request);
        request.TankMode = true;
        var tank = new ForeverWarriorSimRunner().Run(request);
        var dpsSwing = dps.Batch.RepresentativeRun.Timeline.First(ev =>
            ev.AbilityKey == "boss-main-hand-auto" && (ev.RawAmount ?? 0m) > 0m);
        var tankSwing = tank.Batch.RepresentativeRun.Timeline.First(ev =>
            ev.AbilityKey == "boss-main-hand-auto" && (ev.RawAmount ?? 0m) > 0m);
        Assert.True(tankSwing.RawAmount < dpsSwing.RawAmount);
    }

}
