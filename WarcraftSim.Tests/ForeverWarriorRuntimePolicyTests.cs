using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class ForeverWarriorRuntimePolicyTests
{
    private static WarriorSimRequest Base() => new()
    {
        Preset = "arms", TalentMode = "tree", StatSourceMode = "manual",
        DurationSeconds = 20m, ExecutePhaseDurationSeconds = 0m, Iterations = 1,
        StartingRage = 100m, CaptureRepresentativeTimeline = true,
        UseBloodrage = false, UseRend = false, UseExecute = false, UseHeroicStrike = false,
        TargetLevel = 60, TargetDodgePercent = 0m,
    };

    [Theory]
    [InlineData("sunder-armor", 5)]
    [InlineData("battle-shout", 1)]
    [InlineData("demoralizing-shout", 1)]
    [InlineData("thunder-clap", 1)]
    public void UtilityAssignments_CastActualAbilities(string action, int stacks)
    {
        var request = Base();
        request.MaintenancePolicies = [new WarriorMaintenancePolicy
        {
            AbilityKey = action, DesiredStacks = stacks, RefreshWhenRemainingSeconds = 2m
        }];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(result.Batch.RepresentativeRun!.Timeline,
            item => item.Type == CombatEventType.AbilityCastCompleted && item.AbilityKey == action);
    }

    [Fact]
    public void Sunder_RefreshAndStackingAreNotFreePreAppliedDebuffs()
    {
        var request = Base();
        request.DurationSeconds = 34m;
        request.BossDebuffIds = ["sunder-armor"];
        request.MaintenancePolicies = [new WarriorMaintenancePolicy
        {
            AbilityKey = "sunder-armor", DesiredStacks = 5, RefreshWhenRemainingSeconds = 2m
        }];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(request.TargetArmor, result.EffectiveBossArmor); // static pre-pull reduction excluded
        Assert.Contains(result.Batch.RepresentativeRun!.Timeline,
            item => item.AbilityKey == "sunder-armor" && item.Type == CombatEventType.AuraApplied);
    }

    [Fact]
    public void AssignedBattleShout_RemovesExternalStaticSource()
    {
        var request = Base();
        request.RaidBuffIds = ["battle-shout"];
        request.MaintenancePolicies = [new WarriorMaintenancePolicy { AbilityKey = "battle-shout" }];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.True(result.EffectiveCharacterStats["attack-power"] < 1400m);
        Assert.Contains(result.Batch.RepresentativeRun!.Timeline,
            item => item.AbilityKey == "battle-shout" && item.Type == CombatEventType.AbilityCastCompleted);
    }

    [Fact]
    public void OrcBloodFury_FollowsPlannedTime()
    {
        var request = Base();
        request.Race = "orc";
        request.MajorCooldowns = [new WarriorMajorCooldownPolicy
        {
            AbilityKey = "blood-fury", Mode = "at-times", PlannedUseSeconds = [5m]
        }];
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Contains(result.Batch.RepresentativeRun!.Timeline,
            item => item.AbilityKey == "blood-fury" && item.Type == CombatEventType.AbilityCastStarted && item.TimeSeconds == 5m);
    }

    [Fact]
    public void OrcBloodFury_HoldsSecondUseForFinalExecuteWindow()
    {
        var request = Base();
        request.Race = "orc";
        request.DurationSeconds = 180m;
        request.MajorCooldowns = [new WarriorMajorCooldownPolicy
        {
            AbilityKey = "blood-fury", OpeningDelaySeconds = 5m,
            HoldLastUse = true, FinalWindowSeconds = 35m
        }];
        var result = new ForeverWarriorSimRunner().Run(request);
        var casts = result.Batch.RepresentativeRun!.Timeline
            .Where(item => item.AbilityKey == "blood-fury" && item.Type == CombatEventType.AbilityCastStarted)
            .Select(item => item.TimeSeconds).ToList();
        Assert.Contains(5m, casts);
        Assert.Contains(145m, casts);
        Assert.DoesNotContain(125m, casts);
    }

    [Fact]
    public void RacialCooldown_RejectsOtherRace()
    {
        var request = Base();
        request.Race = "human";
        request.MajorCooldowns = [new WarriorMajorCooldownPolicy { AbilityKey = "blood-fury" }];
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void DeathWish_RequiresSelectedTalent()
    {
        var request = Base();
        request.MajorCooldowns = [new WarriorMajorCooldownPolicy { AbilityKey = "death-wish" }];
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void SunderMaintenance_RejectsExposeArmor()
    {
        var request = Base();
        request.BossDebuffIds = ["expose-armor"];
        request.MaintenancePolicies = [new WarriorMaintenancePolicy { AbilityKey = "sunder-armor" }];
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }

    [Fact]
    public void InvalidRefreshWindow_IsRejected()
    {
        var request = Base();
        request.MaintenancePolicies = [new WarriorMaintenancePolicy
        { AbilityKey = "sunder-armor", RefreshWhenRemainingSeconds = 30m }];
        Assert.Throws<ArgumentException>(() => new ForeverWarriorSimRunner().Run(request));
    }
}
