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
    [Fact]
    public void WarlockTimedAddReportsIndependentAbsoluteHealthAndStableReplay()
    {
        var r=WarlockSimRequest.CreatePreset("destruction");
        r.DurationSeconds=22m;r.Seed=175713;r.AdditionalTargets=1;
        r.EnablePet=false;r.CaptureTimeline=true;
        r.SoloEncounter=Windows();
        var first=new WarlockSimRunner().Run(r);
        var second=new WarlockSimRunner().Run(r);
        AssertLifecycle(first.Run.Timeline,"target-1",4m,7m,12m);
        Assert.Equal(Receipts(first.Run.Timeline),Receipts(second.Run.Timeline));
        Assert.Equal(first.DamageDone,second.DamageDone);
    }

    [Fact]
    public void HunterTimedAddReportsIndependentAbsoluteHealthAndStableReplay()
    {
        var r=new MarksmanshipHunterSimRequest{
            DurationSeconds=22m,Seed=175714,AdditionalTargets=1,CaptureTimeline=true,
            SoloEncounter=new(){
                Enabled=true,StartingHealthPercent=100m,AddWindows=[
                    new(){TargetKey="add-1",SpawnTimeSeconds=4m,
                        DespawnTimeSeconds=12m,StartingHealthPercent=100m,
                        HealthMilestones=[new(){TimeSeconds=7m,HealthPercent=55m}]}
                ]
            }
        };
        var first=new MarksmanshipHunterSimRunner().Run(r);
        var second=new MarksmanshipHunterSimRunner().Run(r);
        AssertLifecycle(first.Run.Timeline,"add-1",4m,7m,12m);
        Assert.Equal(Receipts(first.Run.Timeline),Receipts(second.Run.Timeline));
        Assert.Equal(first.DamageDone,second.DamageDone);
    }

    [Fact]
    public void WarriorTimedAddHonorsSpawnAndDespawnWithoutDuplicateLegacyEvents()
    {
        var r=new WarriorSimRequest{
            Preset="fury",TalentMode="tree",Iterations=1,DurationSeconds=22m,
            Seed=175715,CaptureRepresentativeTimeline=true,
            AdditionalTargets=[new(){
                Key="add-1",Name="Add",SpawnTimeSeconds=4m,
                DespawnTimeSeconds=12m
            }],
            SoloEncounter=new(){
                Enabled=true,StartingHealthPercent=100m,AddWindows=[
                    new(){TargetKey="add-1",SpawnTimeSeconds=4m,
                        DespawnTimeSeconds=12m,StartingHealthPercent=100m,
                        HealthMilestones=[new(){TimeSeconds=7m,HealthPercent=55m}]}
                ]
            }
        };
        var first=new ForeverWarriorSimRunner().Run(r);
        var second=new ForeverWarriorSimRunner().Run(r);
        AssertLifecycle(first.Batch.RepresentativeRun.Timeline,"add-1",4m,7m,12m);
        Assert.Equal(Receipts(first.Batch.RepresentativeRun.Timeline),
            Receipts(second.Batch.RepresentativeRun.Timeline));
        Assert.Equal(first.MeanDps,second.MeanDps);
    }

    [Fact]
    public void RelativeToSpawnUsesIndependentLocalTime()
    {
        var script=Windows();
        script.AddWindows[0].HealthMilestonesRelativeToSpawn=true;
        // Offset +7 makes the checkpoint 11s, still before the 12s despawn.
        var ctx=new SimulationContext(new SimulationRunOptions{
            Seed=175716,DurationSeconds=15m,PrimaryActorKey="mage",CaptureTimeline=true
        });
        var source=new SimulationActorState{Key="mage",Name="Mage",TeamKey="raid",
            MaximumHealth=1000m};source.InitializeHealth(1000m);
        var target=new SimulationActorState{Key="target",Name="Boss",TeamKey="enemy",
            MaximumHealth=1000m};target.InitializeHealth(1000m);
        var add=new SimulationActorState{Key="target-1",Name="Add",TeamKey="enemy",
            MaximumHealth=1000m};add.InitializeHealth(1000m);
        ctx.AddActor(source);ctx.AddActor(target);ctx.AddActor(add);
        var run=new SimulationEngine([new SoloDpsAddLifecycleProcessor(script,"mage")])
            .Run(ctx);
        AssertLifecycle(run.Timeline,"target-1",4m,11m,12m);
    }

    private static (decimal Time,string? Key,string? Effect,decimal? Amount)[] Receipts(
        IEnumerable<CombatEvent> events)=>events
        .Where(e=>e.Type==CombatEventType.ScriptedTargetStateChanged)
        .Select(e=>(e.TimeSeconds,e.TargetActorKey,e.EffectKey,e.Amount)).ToArray();

    private static void AssertLifecycle(IEnumerable<CombatEvent> events,
        string key,decimal spawn,decimal milestone,decimal despawn)
    {
        var receipts=Receipts(events);
        Assert.Contains(receipts,e=>e.Key==key && e.Effect==
            SoloDpsAddLifecycleProcessor.SpawnEvent &&
            e.Time==spawn&&e.Amount==100m);
        Assert.Contains(receipts,e=>e.Key==key && e.Effect==
            "solo-dps-health-checkpoint"&&e.Time==milestone&&e.Amount==55m);
        Assert.Contains(receipts,e=>e.Key==key && e.Effect==
            SoloDpsAddLifecycleProcessor.DespawnEvent &&
            e.Time==despawn&&e.Amount==0m);
        Assert.Equal(3,receipts.Count(e=>e.Key==key));
    }

}
