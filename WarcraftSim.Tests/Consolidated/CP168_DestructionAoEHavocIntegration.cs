using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DestructionMultitargetOutcomes
{
    private static WarlockSimRequest Setup(string spell,int targets)
    {
        var r=WarlockSimRequest.CreatePreset("destruction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.AdditionalTargets=targets-1;
        r.TargetMaximumHealth=1000000m;
        r.WarlockMaximumHealth=10000m;
        r.DurationSeconds=8.5m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseLifeTap=false;
        r.CaptureTimeline=true;
        r.CustomPriority=[spell];
        return r;
    }

    [Theory]
    [InlineData("hellfire",1)]
    [InlineData("hellfire",2)]
    [InlineData("hellfire",3)]
    [InlineData("rain-of-fire",1)]
    [InlineData("rain-of-fire",2)]
    [InlineData("rain-of-fire",3)]
    public void AoEChannelsHitEachActualEnemyAndReconcileDamageAndThreat(string spell,int targets)
    {
        var request=Setup(spell,targets);
        var result=new WarlockSimRunner().Run(request);
        var damage=result.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey==spell && e.SourceActorKey=="warlock" &&
            e.Amount is >0m).ToArray();
        for(var i=0;i<targets;i++)
        {
            var enemy=i==0?"target":$"target-{i}";
            Assert.Contains(damage,e=>e.TargetActorKey==enemy);
            Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged &&
                e.SourceActorKey=="warlock" && e.TargetActorKey==enemy &&
                e.AbilityKey==spell && e.Amount>0m);
        }
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey==spell && e.ResourceKey=="mana" && e.Amount<0m);
        var outgoing=damage.Where(e=>e.TargetActorKey!="warlock")
            .Sum(e=>e.Amount!.Value);
        Assert.Equal(outgoing,result.WarlockDamage);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.000001m);
        if(spell=="hellfire")
        {
            Assert.Contains(damage,e=>e.TargetActorKey=="warlock");
            Assert.True(result.EndingHealth<request.WarlockMaximumHealth);
            Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged &&
                e.TargetActorKey=="warlock" && e.AbilityKey=="hellfire");
        }
        else
        {
            Assert.DoesNotContain(damage,e=>e.TargetActorKey=="warlock");
            Assert.Equal(request.WarlockMaximumHealth,result.EndingHealth);
        }
    }

    [Fact]
    public void SecondaryTargetHavocCopiesOnlyOwnerDamageAndDoesNotCopyItsEcho()
    {
        var r=Setup("bane-of-havoc",2);
        r.DurationSeconds=13m;
        r.CustomPriority=["bane-of-havoc","shadow-bolt"];
        r.BaneOfHavocTargetKey="target-1";
        var run=new WarlockSimRunner().Run(r);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="bane-of-havoc" && e.SourceActorKey=="warlock" &&
            e.TargetActorKey=="target-1");
        var direct=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="shadow-bolt" && e.TargetActorKey=="target" &&
            e.Amount>0m).Sum(e=>e.Amount!.Value);
        var echoed=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="bane-of-havoc-echo" && e.TargetActorKey=="target-1" &&
            e.Amount>0m).Sum(e=>e.Amount!.Value);
        Assert.True(direct>0m);
        Assert.Equal(direct*.15m,echoed);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.AbilityKey=="bane-of-havoc-echo" &&
            e.TargetActorKey=="target");
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
        Assert.Contains("BaneOfHavocTargetKey",JsonSerializer.Serialize(r));
    }

    [Fact]
    public void DefaultDestructionCanOptIntoRealHavocSecondaryTarget()
    {
        var r=Setup("shadow-bolt",2);
        r.CustomPriority.Clear();
        r.UseBaneOfHavoc=true;
        r.BaneOfHavocTargetKey="target-1";
        r.DurationSeconds=35m;
        var sim=new WarlockSimRunner().Run(r);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="bane-of-havoc" && e.TargetActorKey=="target-1");
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="bane-of-havoc-echo" && e.TargetActorKey=="target-1");
    }
}
