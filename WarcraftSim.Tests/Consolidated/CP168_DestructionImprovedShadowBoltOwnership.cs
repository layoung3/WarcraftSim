using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DestructionImprovedShadowBoltOwnership
{
    [Fact]
    public void CriticalOwnerShadowBoltGrantsOnlyTargetOwnedShadowBonusWithoutCharges()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{DurationSeconds=25m,CaptureTimeline=true});
        var owner=new SimulationActorState{Key="warlock",TeamKey="raid"};
        var other=new SimulationActorState{Key="another-warlock",TeamKey="raid"};
        var a=new SimulationActorState{Key="target",TeamKey="enemy",MaximumHealth=10000m};
        var b=new SimulationActorState{Key="target-1",TeamKey="enemy",MaximumHealth=10000m};
        a.InitializeHealth(10000m);
        b.InitializeHealth(10000m);
        foreach(var actor in new[]{owner,other,a,b})ctx.AddActor(actor);
        var auras=new AuraManager();
        var proc=new WarlockImprovedShadowBoltProcessor(5,auras);
        void Event(string source,string target,bool critical,bool periodic=false)=>
            proc.Process(ctx,new CombatEvent{
                Type=CombatEventType.Damage,
                SourceActorKey=source,TargetActorKey=target,AbilityKey="shadow-bolt",
                ResultKey=critical?"critical":"hit",IsCritical=critical,
                IsPeriodic=periodic,Amount=100m
            });
        Event("another-warlock","target",true);
        Event("warlock","target-1",false);
        Event("warlock","target",true,true);
        Assert.Empty(a.ActiveAuras);
        Assert.Empty(b.ActiveAuras);
        Event("warlock","target",true);
        Assert.Single(a.ActiveAuras, x=>x.Definition.Key==
            WarlockImprovedShadowBoltProcessor.AuraKey);
        Assert.Empty(b.ActiveAuras);
        Assert.Equal(1.2m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            a,0m,"warlock","shadow",5));
        Assert.Equal(1m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            a,0m,"warlock","fire",5));
        Assert.Equal(1m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            a,0m,"another-warlock","shadow",5));
        Assert.Equal(1m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            b,0m,"warlock","shadow",5));
        Assert.Equal(1m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            a,12m,"warlock","shadow",5));
        Event("warlock","target",true);
        Assert.Single(a.ActiveAuras, x=>x.Definition.Key==
            WarlockImprovedShadowBoltProcessor.AuraKey);
        auras.RemoveAuras(ctx,a,WarlockImprovedShadowBoltProcessor.AuraKey,"warlock");
        Assert.Equal(1m,WarlockImprovedShadowBoltProcessor.DamageMultiplier(
            a,0m,"warlock","shadow",5));
    }
}
