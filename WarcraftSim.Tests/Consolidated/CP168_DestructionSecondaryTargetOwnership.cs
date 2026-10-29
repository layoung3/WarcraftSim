using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DestructionSecondaryTargetCasting
{
    private static WarlockSimRequest Request(int adds)
    {
        var r=WarlockSimRequest.CreatePreset("destruction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.AdditionalTargets=adds;
        r.DurationSeconds=28m;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CaptureTimeline=true;
        r.CustomPriority=["shadow-bolt"];
        return r;
    }

    [Fact]
    public void ThreeIndependentEnemiesRetainOwnImmolateApplicationsAndTicks()
    {
        var request=Request(2);
        request.DestructionAdditionalTargetPriority["target-1"]=["immolate"];
        request.DestructionAdditionalTargetPriority["target-2"]=["immolate"];
        var run=new WarlockSimRunner().Run(request);
        foreach(var enemy in new[]{"target-1","target-2"})
        {
            Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
                e.AbilityKey=="immolate" && e.TargetActorKey==enemy &&
                e.SourceActorKey=="warlock");
            Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey=="immolate" && e.TargetActorKey==enemy &&
                e.IsPeriodic && e.Amount>0m);
        }
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
        var copy=JsonSerializer.Deserialize<WarlockSimRequest>(JsonSerializer.Serialize(request));
        Assert.NotNull(copy);
        Assert.Equal("immolate",Assert.Single(copy.DestructionAdditionalTargetPriority["target-2"]));
    }

    [Fact]
    public void ConflagrateCanConsumeOnlyItsOwnSecondaryTargetImmolate()
    {
        var request=Request(1);
        request.DurationSeconds=15m;
        request.DestructionAdditionalTargetPriority["target-1"]=
            ["immolate","conflagrate"];
        var result=new WarlockSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="conflagrate" && e.TargetActorKey=="target-1" &&
            e.SourceActorKey=="warlock" && e.Amount>0m);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="conflagrate" && e.TargetActorKey=="target");
    }

    [Fact]
    public void UnknownSecondaryEnemyAndWrongSpecAreRejected()
    {
        var request=Request(1);
        request.DestructionAdditionalTargetPriority["target-5"]=["immolate"];
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(request));
        request.DestructionAdditionalTargetPriority.Clear();
        request.DestructionAdditionalTargetPriority["target-1"]=["drain-life"];
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(request));
        var wrong=WarlockSimRequest.CreatePreset("affliction");
        wrong.DestructionAdditionalTargetPriority["target-1"]=["immolate"];
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(wrong));
    }
}
