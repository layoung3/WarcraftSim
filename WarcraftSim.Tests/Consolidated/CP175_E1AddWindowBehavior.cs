using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175E1AddWindowBehavior
{
    private static SoloDpsEncounterScript Script(string key,bool relative=false)=>new() {
        Enabled=true,StartingHealthPercent=100m,
        BossHealthMilestones=[new(){TimeSeconds=8m,HealthPercent=50m}],
        AddWindows=[new() {
            TargetKey=key,SpawnTimeSeconds=4m,DespawnTimeSeconds=12m,
            StartingHealthPercent=100m,HealthMilestonesRelativeToSpawn=relative,
            HealthMilestones=[new() {
                TimeSeconds=relative?3m:7m,HealthPercent=55m
            }]
        }]
    };

    private static void CheckEvents(IReadOnlyList<CombatEvent> timeline,string key)
    {
        var events=timeline.Where(e=>e.Type==CombatEventType.ScriptedTargetStateChanged).ToArray();
        Assert.Contains(events,e=>e.EffectKey==SoloDpsAddLifecycleProcessor.SpawnEvent &&
            e.TargetActorKey==key && e.TimeSeconds==4m && e.Amount==100m);
        Assert.Contains(events,e=>e.EffectKey=="solo-dps-health-checkpoint" &&
            e.TargetActorKey==key && e.TimeSeconds==7m && e.Amount==55m);
        Assert.Contains(events,e=>e.EffectKey==SoloDpsAddLifecycleProcessor.DespawnEvent &&
            e.TargetActorKey==key && e.TimeSeconds==12m && e.Amount==0m);
        Assert.Contains(events,e=>e.EffectKey=="solo-dps-health-checkpoint" &&
            e.TargetActorKey=="target" && e.TimeSeconds==8m && e.Amount==50m);
        Assert.Equal(1,events.Count(e=>e.TargetActorKey==key &&
            e.EffectKey==SoloDpsAddLifecycleProcessor.SpawnEvent));
        Assert.Equal(1,events.Count(e=>e.TargetActorKey==key &&
            e.EffectKey==SoloDpsAddLifecycleProcessor.DespawnEvent));
    }

    private sealed class ObserveAdd : ICombatEventProcessor
    {
        public readonly List<(decimal Time,bool Alive,decimal Health)> Seen=[];
        public void Process(SimulationContext ctx,CombatEvent ev)
        {
            if(ev.Type!=CombatEventType.ScriptedTargetStateChanged ||
               ev.TargetActorKey!="add-1")return;
            var actor=ctx.GetActor("add-1")!;
            Seen.Add((ev.TimeSeconds,actor.IsAlive,ctx.GetHealthPercentForConditions(actor)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbsoluteAndRelativeHealthWindowsProduceIdenticalObservedState(bool relative)
    {
        var ctx=new SimulationContext(new SimulationRunOptions {
            Seed=175800,DurationSeconds=16m,PrimaryActorKey="warrior",CaptureTimeline=true
        });
        var owner=new SimulationActorState {
            Key="warrior",Name="Warrior",TeamKey="raid",MaximumHealth=4000m
        };
        owner.InitializeHealth(4000m);ctx.AddActor(owner);
        var boss=new SimulationActorState {
            Key="target",Name="Boss",TeamKey="enemy",MaximumHealth=1000000m
        };
        boss.InitializeHealth(1000000m);ctx.AddActor(boss);
        var add=new SimulationActorState {
            Key="add-1",Name="Add",TeamKey="enemy",MaximumHealth=1000000m
        };
        add.InitializeHealth(1000000m);ctx.AddActor(add);
        var observer=new ObserveAdd();
        Script("add-1",relative).Apply(ctx);
        var run=new SimulationEngine([
            new SoloDpsAddLifecycleProcessor(Script("add-1",relative),"warrior"),
            observer
        ]).Run(ctx);
        CheckEvents(run.Timeline,"add-1");
        Assert.Contains(observer.Seen,e=>e.Time==4m&&e.Alive&&e.Health==100m);
        Assert.Contains(observer.Seen,e=>e.Time==7m&&e.Alive&&e.Health==55m);
        Assert.Contains(observer.Seen,e=>e.Time==12m&&!e.Alive&&e.Health==0m);
        Assert.Equal(50m,ctx.GetHealthPercentForConditions(boss));
        Assert.False(add.IsAlive);
    }

    [Fact]
    public void SpawnAndDespawnAreDeterministicInRealWarlockAndHunterRunners()
    {
        var warlock=WarlockSimRequest.CreatePreset("destruction");
        warlock.Seed=175801;warlock.DurationSeconds=18m;
        warlock.AdditionalTargets=1;warlock.CaptureTimeline=true;
        warlock.EnablePet=false;
        warlock.SoloEncounter=Script("target-1",relative:true);
        var wlFirst=new WarlockSimRunner().Run(warlock);
        var wlSecond=new WarlockSimRunner().Run(warlock);
        CheckEvents(wlFirst.Run.Timeline,"target-1");
        Assert.Equal(wlFirst.Run.Timeline.Where(e=>e.Type==CombatEventType.ScriptedTargetStateChanged)
            .Select(e=>(e.TimeSeconds,e.TargetActorKey,e.EffectKey,e.Amount)),
            wlSecond.Run.Timeline.Where(e=>e.Type==CombatEventType.ScriptedTargetStateChanged)
            .Select(e=>(e.TimeSeconds,e.TargetActorKey,e.EffectKey,e.Amount)));

        var hunter=new MarksmanshipHunterSimRequest {
            Seed=175802,DurationSeconds=18m,AdditionalTargets=1,CaptureTimeline=true,
            SoloEncounter=Script("add-1")
        };
        var hFirst=new MarksmanshipHunterSimRunner().Run(hunter);
        var hSecond=new MarksmanshipHunterSimRunner().Run(hunter);
        CheckEvents(hFirst.Run.Timeline,"add-1");
        Assert.Equal(hFirst.Run.Timeline.Where(e=>e.Type==CombatEventType.ScriptedTargetStateChanged)
            .Select(e=>(e.TimeSeconds,e.TargetActorKey,e.EffectKey,e.Amount)),
            hSecond.Run.Timeline.Where(e=>e.Type==CombatEventType.ScriptedTargetStateChanged)
            .Select(e=>(e.TimeSeconds,e.TargetActorKey,e.EffectKey,e.Amount)));
    }

    [Fact]
    public void WarriorLegacyAddSchedulerDoesNotDuplicateSharedWindowTransitions()
    {
        var warrior=new WarriorSimRequest {
            Preset="fury",TalentMode="tree",Iterations=1,DurationSeconds=18m,
            Seed=175803,CaptureRepresentativeTimeline=true,
            AdditionalTargets=[new WarriorAdditionalTarget {
                Key="add-1",SpawnTimeSeconds=4m,DespawnTimeSeconds=12m
            }],
            SoloEncounter=Script("add-1",relative:true)
        };
        var result=new ForeverWarriorSimRunner().Run(warrior);
        CheckEvents(result.Batch.RepresentativeRun.Timeline,"add-1");
    }

    [Fact]
    public void InvalidRelativeMilestoneOutsideWindowIsRejected()
    {
        var broken=Script("add-1",relative:true);
        broken.AddWindows[0].HealthMilestones[0].TimeSeconds=10m;
        var ctx=new SimulationContext(new SimulationRunOptions {
            Seed=175804,DurationSeconds=16m,PrimaryActorKey="owner"
        });
        var owner=new SimulationActorState {
            Key="owner",Name="Owner",TeamKey="raid",MaximumHealth=1000m
        };owner.InitializeHealth(1000m);ctx.AddActor(owner);
        foreach(var key in new[]{"target","add-1"}) {
            var actor=new SimulationActorState {
                Key=key,Name=key,TeamKey="enemy",MaximumHealth=100000m
            };actor.InitializeHealth(100000m);ctx.AddActor(actor);
        }
        Assert.Throws<ArgumentException>(()=>new SimulationEngine([
            new SoloDpsAddLifecycleProcessor(broken,"owner")
        ]).Run(ctx));
    }
}
