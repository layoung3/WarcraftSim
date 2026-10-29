using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BsMageScorchTargetOwnership
{
    [Fact]
    public void OverlappingScorchApplicationsRetainSeparateSourceOwnedStacks()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{
            DurationSeconds=4m,Seed=167221,PrimaryActorKey="mage",CaptureTimeline=true
        });
        SimulationActorState Actor(string key,string team)
        {
            var a=new SimulationActorState{Key=key,TeamKey=team,Level=60,MaximumHealth=5000m};
            a.InitializeHealth(5000m);
            return a;
        }
        var caster=Actor("mage","raid");
        var first=Actor("target","enemy");
        var second=Actor("target-1","enemy");
        ctx.AddActor(caster);ctx.AddActor(first);ctx.AddActor(second);
        var auras=new AuraManager();
        var scorch=new MageImprovedScorchProcessor(caster,auras,3);
        var result=new SimulationEngine([auras,scorch]).Run(ctx,start=>
        {
            foreach(var (at,enemy,landed) in new[]{
                (.1m,"target",true),(.2m,"target-1",true),
                (.3m,"target",true),(.4m,"target-1",false)})
                start.ScheduleEvent(new CombatEvent{
                    TimeSeconds=at,Type=CombatEventType.Damage,
                    SourceActorKey="mage",TargetActorKey=enemy,
                    AbilityKey="scorch",SchoolKey="fire",
                    ResultKey=landed?"hit":"miss",
                    Amount=landed?100m:0m
                });
        });
        Assert.Equal(2,scorch.GetStacks("target",4m));
        Assert.Equal(1,scorch.GetStacks("target-1",4m));
        Assert.Equal(1.06m,scorch.FireDamageMultiplier("target",4m));
        Assert.Equal(1.03m,scorch.FireDamageMultiplier("target-1",4m));
        Assert.Equal(3,scorch.Applications);
        Assert.DoesNotContain(result.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.TimeSeconds==.4m);
    }

    [Fact]
    public void ScorchPerTargetExpirationsDoNotRemoveTheOthersRefreshedAura()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{
            DurationSeconds=33m,Seed=167222,PrimaryActorKey="mage",CaptureTimeline=true
        });
        SimulationActorState Actor(string key,string team)
        {
            var a=new SimulationActorState{Key=key,TeamKey=team,Level=60,MaximumHealth=5000m};
            a.InitializeHealth(5000m);
            return a;
        }
        var caster=Actor("mage","raid");var a=Actor("target","enemy");var b=Actor("target-1","enemy");
        ctx.AddActor(caster);ctx.AddActor(a);ctx.AddActor(b);
        var auras=new AuraManager();
        var scorch=new MageImprovedScorchProcessor(caster,auras,3);
        var run=new SimulationEngine([auras,scorch]).Run(ctx,start=>{
            foreach(var (at,enemy) in new[]{(.1m,"target"),(2m,"target-1")})
                start.ScheduleEvent(new CombatEvent {
                    TimeSeconds=at,Type=CombatEventType.Damage,SourceActorKey="mage",
                    TargetActorKey=enemy,AbilityKey="scorch",SchoolKey="fire",
                    ResultKey="hit",Amount=100m
                });
        });
        Assert.Equal(0,scorch.GetStacks("target",32.5m));
        Assert.Equal(0,scorch.GetStacks("target-1",32.5m));
        Assert.Equal(0,scorch.GetStacks("target",31m));
        Assert.Equal(1,scorch.GetStacks("target-1",31m));
        Assert.True(run.Timeline.Count(e=>e.Type==CombatEventType.AuraRemoved &&
            e.EffectKey=="improved-scorch-stack")>=2);
    }
}