using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175E1SharedTimedAdds
{
    private static SoloDpsEncounterScript Windows()=>new(){
        Enabled=true,StartingHealthPercent=100m,
        AddWindows=[
            new(){TargetKey="target-1",SpawnTimeSeconds=4m,
                DespawnTimeSeconds=12m,StartingHealthPercent=100m,
                HealthMilestones=[new(){TimeSeconds=7m,HealthPercent=55m}]}
        ]
    };
    [Fact]
    public void SharedAddProcessorSchedulesRealActivationAndExpiryEvents()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{
            Seed=175710,DurationSeconds=15m,PrimaryActorKey="mage",
            CaptureTimeline=true
        });
        var source=new SimulationActorState{Key="mage",Name="Mage",TeamKey="raid",
            MaximumHealth=1000m};
        source.InitializeHealth(1000m);
        var boss=new SimulationActorState{Key="target",Name="Boss",TeamKey="enemy",
            MaximumHealth=1000000m};
        boss.InitializeHealth(1000000m);
        var add=new SimulationActorState{Key="target-1",Name="Add",TeamKey="enemy",
            MaximumHealth=1000m};
        add.InitializeHealth(1000m);
        ctx.AddActor(source);ctx.AddActor(boss);ctx.AddActor(add);
        var run=new SimulationEngine([new SoloDpsAddLifecycleProcessor(Windows(),"mage")])
            .Run(ctx);
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.ScriptedTargetStateChanged &&
            e.EffectKey==SoloDpsAddLifecycleProcessor.SpawnEvent &&
            e.TargetActorKey=="target-1"&&e.TimeSeconds==4m&&e.Amount==100m);
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.ScriptedTargetStateChanged &&
            e.EffectKey==SoloDpsAddLifecycleProcessor.DespawnEvent &&
            e.TargetActorKey=="target-1"&&e.TimeSeconds==12m&&e.Amount==0m);
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.ScriptedTargetStateChanged &&
            e.EffectKey=="solo-dps-health-checkpoint"&&e.TargetActorKey=="target-1" &&
            e.TimeSeconds==7m&&e.Amount==55m);
        Assert.Equal(0m,ctx.GetHealthPercentForConditions(add));
        Assert.False(add.IsAlive);
    }
    [Fact]
    public void TypedMageHasScriptedAddEventsAndExactSeedReplay()
    {
        var request=MageSimRequest.CreatePreset("fire");
        request.Seed=175711;request.DurationSeconds=22m;
        request.AdditionalTargets=1;request.CaptureTimeline=true;
        request.SoloEncounter=Windows();
        var first=new MageSimRunner().Run(request);
        var second=new MageSimRunner().Run(request);
        Assert.Contains(first.Run.Timeline,e=>e.Type==CombatEventType.ScriptedTargetStateChanged &&
            e.EffectKey==SoloDpsAddLifecycleProcessor.SpawnEvent&&e.TimeSeconds==4m&&e.Amount==100m);
        Assert.Contains(first.Run.Timeline,e=>e.Type==CombatEventType.ScriptedTargetStateChanged &&
            e.EffectKey==SoloDpsAddLifecycleProcessor.DespawnEvent&&e.TimeSeconds==12m&&e.Amount==0m);
        Assert.Equal(first.Run.Timeline.Where(e=>e.Type==CombatEventType.ScriptedTargetStateChanged)
           .Select(e=>(e.TimeSeconds,e.TargetActorKey,e.EffectKey,e.Amount)),
           second.Run.Timeline.Where(e=>e.Type==CombatEventType.ScriptedTargetStateChanged)
           .Select(e=>(e.TimeSeconds,e.TargetActorKey,e.EffectKey,e.Amount)));
        Assert.Equal(first.DamageDone,second.DamageDone);
    }
    [Fact]
    public void UnknownAddAndConflictingWindowAreRejectedBeforeCombat()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{
            Seed=175712,DurationSeconds=20m,PrimaryActorKey="mage"
        });
        var boss=new SimulationActorState{Key="target",Name="Boss",TeamKey="enemy",
            MaximumHealth=100000m};boss.InitializeHealth(100000m);
        ctx.AddActor(boss);
        Assert.Throws<ArgumentException>(()=>new SimulationEngine([
            new SoloDpsAddLifecycleProcessor(Windows(),"mage")
        ]).Run(ctx));
    }
}
