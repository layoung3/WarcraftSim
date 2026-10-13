// Consolidated tests for Warrior Foundation.
// Original class names are retained for xUnit test filtering.

// ==================== WarriorScriptedHealthTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class WarriorScriptedHealthTests
{
    private static WarriorSimRequest ExecuteOnly() => new()
    {
        Preset = "fury", DurationSeconds = 30m,
        ExecutePhaseDurationSeconds = 10m,
        Iterations = 1, CaptureRepresentativeTimeline = true,
        TargetLevel = 60, TargetDodgePercent = 0m,
        DeepWoundsRank = 0, ImpaleRank = 0, UseBloodrage = false,
        StartingRage = 100m, RotationMode = "custom",
        CustomRotation = [new WarriorRotationRule { Action = "execute" }]
    };

    [Fact]
    public void ScriptedHealthSchedule_UsesInclusiveBreakpointsAndIgnoresActualDamage()
    {
        var context = new SimulationContext(new SimulationRunOptions { DurationSeconds = 180m });
        var target = new SimulationActorState { Key = "boss" };
        target.InitializeHealth(100_000m);
        context.AddActor(target);
        context.SetScriptedTargetHealth("boss", new ScriptedTargetHealthTimeline(
        [
            new(0m, 100m), new(90m, 60m), new(150m, 20m)
        ]));
        target.TakeDamage(50_000m);
        Assert.Equal(100m, context.GetHealthPercentForConditions(target));
        foreach (var t in new[] { 89.9m, 90m, 149.9m, 150m })
            context.ScheduleEvent(new CombatEvent { Type = CombatEventType.RotationDecision, TimeSeconds = t });
        var values = new List<decimal>();
        while (context.TryGetNextEvent(out _))
            values.Add(context.GetHealthPercentForConditions(target));
        Assert.Equal(new[] { 100m, 60m, 60m, 20m }, values);
        Assert.Equal(50_000m, target.CurrentHealth);
        var realHealthActor = new SimulationActorState { Key = "raid-boss" };
        realHealthActor.InitializeHealth(100m, 75m);
        context.AddActor(realHealthActor);
        Assert.Equal(75m, context.GetHealthPercentForConditions(realHealthActor));
    }

    [Fact]
    public void ExecutePhase_OpensAtConfiguredTime_NotFromCharacterDamage()
    {
        var result = new ForeverWarriorSimRunner().Run(ExecuteOnly());
        var events = result.Batch.RepresentativeRun.Timeline
            .Where(e => e.AbilityKey == "execute").ToList();
        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.True(e.TimeSeconds >= 20m));
    }

    [Fact]
    public void ExecutePhase_ZeroDurationNeverOpens()
    {
        var request = ExecuteOnly();
        request.ExecutePhaseDurationSeconds = 0m;
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.DoesNotContain("execute", result.Batch.Summary.AverageDamageDoneByAbility.Keys);
    }

    [Fact]
    public void CustomMilestones_OverrideDefaultExecuteWindow()
    {
        var request = ExecuteOnly();
        request.ExecutePhaseDurationSeconds = 30m;
        request.TargetHealthMilestones =
        [
            new WarriorTargetHealthMilestone { TimeSeconds = 8m, HealthPercent = 60m },
            new WarriorTargetHealthMilestone { TimeSeconds = 24m, HealthPercent = 20m }
        ];
        var result = new ForeverWarriorSimRunner().Run(request);
        var events = result.Batch.RepresentativeRun.Timeline.Where(e => e.AbilityKey == "execute").ToList();
        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.True(e.TimeSeconds >= 24m));
    }

    [Fact]
    public void InvalidMilestoneOrderAndPercent_AreRejected()
    {
        var request = ExecuteOnly();
        request.TargetHealthMilestones =
        [
            new WarriorTargetHealthMilestone { TimeSeconds = 18m, HealthPercent = 20m },
            new WarriorTargetHealthMilestone { TimeSeconds = 10m, HealthPercent = 60m }
        ];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.TargetHealthMilestones =
        [new WarriorTargetHealthMilestone { TimeSeconds = 10m, HealthPercent = 110m }];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }
}
}
