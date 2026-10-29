using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DestructionShadowburnBoundary
{
    private static (SimulationRunResult Run,decimal FinalShards) RunWindow(
        decimal deathAt,bool duplicateDeath=false,bool secondTarget=false)
    {
        var ctx=new SimulationContext(new SimulationRunOptions{
            DurationSeconds=12m,PrimaryActorKey="warlock",CaptureTimeline=true,Seed=168021
        });
        var owner=new SimulationActorState{
            Key="warlock",TeamKey="raid",Level=60,MaximumHealth=2000m
        };
        owner.InitializeHealth(2000m);
        owner.AddResource(new ResourceState("soul-shard",20m,0m));
        var boss=new SimulationActorState{
            Key="target",TeamKey="enemy",Level=63,MaximumHealth=1000m
        };
        boss.InitializeHealth(1000m);
        ctx.AddActor(owner);
        ctx.AddActor(boss);
        if(secondTarget)
        {
            var add=new SimulationActorState{
                Key="target-1",TeamKey="enemy",Level=63,MaximumHealth=1000m
            };
            add.InitializeHealth(1000m);
            ctx.AddActor(add);
        }
        var handler=new WarlockShadowburnShardProcessor(true);
        var run=new SimulationEngine([handler]).Run(ctx,start=>{
            start.ScheduleEvent(new CombatEvent {
                TimeSeconds=0m,Type=CombatEventType.Damage,
                SourceActorKey="warlock",TargetActorKey="target",
                AbilityKey="shadowburn",ResultKey="hit",Amount=100m
            });
            start.ScheduleEvent(new CombatEvent {
                TimeSeconds=deathAt,Type=CombatEventType.ActorDied,
                SourceActorKey="warlock",TargetActorKey="target"
            });
            if(duplicateDeath)
                start.ScheduleEvent(new CombatEvent {
                    TimeSeconds=deathAt,Type=CombatEventType.ActorDied,
                    SourceActorKey="warlock",TargetActorKey="target"
                });
            if(secondTarget)
                start.ScheduleEvent(new CombatEvent {
                    TimeSeconds=deathAt,Type=CombatEventType.ActorDied,
                    SourceActorKey="warlock",TargetActorKey="target-1"
                });
        });
        return (run,owner.Resources["soul-shard"].Current);
    }

    [Theory]
    [InlineData(7.999999,true)]
    [InlineData(8.0,true)]
    [InlineData(8.000001,false)]
    [InlineData(9.0,false)]
    public void DeathWindowHasExactBoundaryAndRefundsAtMostOnce(double seconds,bool refund)
    {
        var result=RunWindow((decimal)seconds,true,true);
        var refunds=result.Run.Timeline.Where(e=>
            e.Type==CombatEventType.ResourceChanged &&
            e.EffectKey=="shadowburn-kill-shard").ToArray();
        Assert.Equal(refund?1:0,refunds.Length);
        Assert.Equal(refund?1m:0m,result.FinalShards);
    }

    [Fact]
    public void SeparateBrandedTargetsDoNotShareShadowburnWindow()
    {
        var result=RunWindow(4m,false,true);
        Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.EffectKey=="shadowburn-kill-shard" && e.TargetActorKey=="warlock");
        Assert.Equal(1m,result.FinalShards);
    }
}
