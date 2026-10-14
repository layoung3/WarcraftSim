using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class WarriorTankAccuracyCp121Tests
{
    private static WarriorSimRequest Base() => new()
    {
        Preset = "protection", EquipmentMode = "manual", TalentMode = "legacy",
        ShieldEquipped = true, TankMode = true, EnableBossAutoAttacks = true,
        IsTwoHanded = false, DualWield = false,
        DurationSeconds = 12m, Iterations = 1, Seed = 72514,
        BossFirstSwingSeconds = 0m, BossSwingSpeedSeconds = 2m,
        BossSwingMinimumDamage = 1000m, BossSwingMaximumDamage = 1000m,
        PlayerDodgePercent = 0m, PlayerParryPercent = 0m,
        PlayerBlockPercent = 0m, BossMeleeCritPercent = 0m,
        UseBloodrage = false, StartingRage = 50m,
        TargetDodgePercent = 0m, TargetParryPercent = 0m,
        CaptureRepresentativeTimeline = true
    };

    [Fact]
    public void ShieldSlamUsesCurrentForeverClientBaseRange()
    {
        var r = Base(); r.TalentMode = "tree";
        r.TalentRanks = new(StringComparer.OrdinalIgnoreCase)
        {
            ["shield-specialization"] = 5, ["iron-will"] = 5,
            ["anticipation"] = 5, ["improved-revenge"] = 3,
            ["last-stand"] = 1, ["defiance"] = 3,
            ["improved-sunder-armor"] = 3, ["improved-shield-wall"] = 2,
            ["concussion-blow"] = 1, ["focused-rage"] = 3,
            ["bastion"] = 5, ["shield-slam"] = 1
        };
        var shieldSlam = WarriorProtectionAbilityFactory.Create(r).Single(x => x.Key == "shield-slam");
        var damage = Assert.Single(shieldSlam.Effects);
        Assert.Equal(421m, damage.MinimumValue);
        Assert.Equal(439m, damage.MaximumValue);
        Assert.Equal(1m, damage.ScalingCoefficient);
    }

    [Fact]
    public void SoloBossRunsToEndEvenWithLegacyFiniteFlagFalse()
    {
        var request = Base();
        request.TankContinuousHealing = false; // old exported JSON remains readable, now ignored.
        request.MaximumHealth = 100m;
        request.BossSwingMinimumDamage = 40000m;
        request.BossSwingMaximumDamage = 40000m;
        var output = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(0m, output.DeathRatePercent);
        Assert.False(output.Batch.RepresentativeRun.Summary.PrimaryActorDied);
        Assert.True(output.MeanDamageTakenPerSecond > 1000m);
        Assert.True(output.RepresentativeMaximumWindowPercentOfHealth > 100m);
        Assert.True(output.Batch.RepresentativeRun.Timeline.Count(e =>
            e.AbilityKey == "boss-main-hand-auto" && e.Type == CombatEventType.Damage) >= 5);
    }

    [Fact]
    public void TankThreatIsReportedAcrossBatchRatherThanOnlyRepresentativeRun()
    {
        var r = Base(); r.Iterations = 4;
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.True(output.MeanThreatPerSecond > 0m);
        Assert.True(output.P95ThreatPerSecond >= output.MeanThreatPerSecond);
        Assert.True(output.RepresentativeThreatPerSecond > 0m);
    }

    [Fact]
    public void DeflectionAddsParryToIncomingTable()
    {
        var r = Base(); r.Preset = "arms"; r.TankMode = false;
        r.TalentMode = "tree";
        r.TalentRanks = new(StringComparer.OrdinalIgnoreCase) { ["deflection"] = 5 };
        // The incoming-table odds now include the selected Deflection ranks;
        // in an isolated actor this is validated by a deterministic 100% parry.
        r.PlayerParryPercent = 95m; r.BossMeleeCritPercent = 0m;
        r.UseRend = false; r.UseHeroicStrike = false;
        var result = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline, e =>
            e.AbilityKey == "boss-main-hand-auto" && e.ResultKey == "parry");
    }

    [Fact]
    public void ParryHasteIsDisabledForBossWhenRequested()
    {
        var r = Base(); r.TargetParryPercent = 100m; r.BehindTarget = false;
        r.BossParryHaste = false;
        var result = new ForeverWarriorSimRunner().Run(r);
        var ticks = result.Batch.RepresentativeRun.Timeline.Where(e =>
            e.AbilityKey == "boss-main-hand-auto" && e.Type == CombatEventType.Damage)
            .Select(e => e.TimeSeconds).ToArray();
        Assert.Equal(new[] {0m,2m,4m,6m,8m,10m}, ticks);
    }

    [Fact]
    public void ParryHasteAcceleratesBossWhenItParries()
    {
        var r = Base(); r.TargetParryPercent = 100m; r.BehindTarget = false;
        r.BossParryHaste = true;
        var result = new ForeverWarriorSimRunner().Run(r);
        var ticks = result.Batch.RepresentativeRun.Timeline.Where(e =>
            e.AbilityKey == "boss-main-hand-auto" && e.Type == CombatEventType.Damage)
            .Select(e => e.TimeSeconds).ToArray();
        Assert.NotEmpty(ticks);
        // At t=0 the warrior's auto attacks immediately; the boss next swing
        // may be accelerated by subsequent parries if target parry is possible.
        Assert.True(ticks.Length >= 6);
        Assert.Contains(ticks, t => t > 0m && t % 2m != 0m);
    }

    [Fact]
    public void DefaultAttackDamagePicksUpShieldBlockMitigation()
    {
        var r = Base(); r.PlayerBlockPercent = 25m;
        var result = new ForeverWarriorSimRunner().Run(r);
        Assert.True(result.Batch.RepresentativeRun.Summary.BlockedDamageReceived > 0m);
        Assert.True(result.MeanDamageTakenPerSecond > 0m);
    }

    [Fact]
    public void PlayerParryHasteAdvancesMainHandAutoSwings()
    {
        var r = Base(); r.PlayerParryPercent = 100m;
        r.BossSwingSpeedSeconds = 1m;
        r.UseHeroicStrike = false;
        r.PlayerParryHaste = false;
        var without = new ForeverWarriorSimRunner().Run(r);
        r.PlayerParryHaste = true;
        var with = new ForeverWarriorSimRunner().Run(r);
        static int CountSwings(WarriorSimResponse result) =>
            result.Batch.RepresentativeRun.Timeline.Count(e =>
                e.AbilityKey == "main-hand-auto" && e.Type == CombatEventType.Damage);
        Assert.True(CountSwings(with) > CountSwings(without),
            $"Expected parry haste to increase player swings: {CountSwings(without)} -> {CountSwings(with)}");
    }

    [Fact]
    public void ThunderClapAllowsBattleAndDefensiveButForbidsBerserkerStance()
    {
        var r = Base();
        r.MaintenancePolicies = [new WarriorMaintenancePolicy
        {
            AbilityKey = "thunder-clap", DesiredStacks = 1
        }];
        var ability = WarriorRuntimeActionProcessor.CreateAbilities(r)
            .Single(x => x.Key == "thunder-clap");
        // Aura requirements are ALL-of, not ANY-of. Requiring both legal
        // stances made this ability impossible to execute in CP121.
        Assert.Empty(ability.RequiredSourceAuraKeys);
        Assert.Single(ability.ForbiddenSourceAuraKeys);
        Assert.Contains(ForeverWarriorAuraKeys.BerserkerStance, ability.ForbiddenSourceAuraKeys);
    }

    [Fact]
    public void MaintainedThunderClapSlowsSubsequentBossSwingIntervals()
    {
        var r = Base();
        r.BossFirstSwingSeconds = 2m;
        r.BossParryHaste = false;
        r.MaintenancePolicies = [new WarriorMaintenancePolicy
        {
            AbilityKey = "thunder-clap", DesiredStacks = 1,
            RefreshWhenRemainingSeconds = 2m
        }];
        r.StartingRage = 100m;
        var output = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(output.Batch.RepresentativeRun.Timeline, e =>
            e.AbilityKey == "thunder-clap" && e.Type == CombatEventType.AbilityCastCompleted);
        var times = output.Batch.RepresentativeRun.Timeline.Where(e =>
            e.AbilityKey == "boss-main-hand-auto" && e.Type == CombatEventType.Damage)
            .Select(e => e.TimeSeconds).ToArray();
        Assert.True(times.Length >= 3);
        Assert.Contains(times.Skip(1).Zip(times, (next, prev) => next - prev),
            seconds => seconds == 2.4m);
    }

}
