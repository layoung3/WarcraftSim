using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168FinalWarlockCrossSpecializationRegression
{
    private static WarlockSimRequest Setup(string spec,bool custom)
    {
        var r=WarlockSimRequest.CreatePreset(spec);
        r.DurationSeconds=32m;
        r.TargetMaximumHealth=10000000m;
        r.AdditionalTargets=2;
        r.EnablePet=true;
        r.PetType="imp";
        r.PrePullSacrificedDemon=null;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.DemonTuning.DemonSpellHitPercent=100m;
        r.UseLifeTap=false;
        r.CaptureTimeline=true;
        if(custom)
            r.CustomPriority=spec switch {
                "affliction" => ["corruption","bane-of-agony","shadow-bolt"],
                "demonology" => ["soul-fire","shadow-bolt"],
                _ => ["immolate","conflagrate","incinerate","shadow-bolt"]
            };
        return r;
    }

    [Theory]
    [InlineData("affliction",false)]
    [InlineData("affliction",true)]
    [InlineData("demonology",false)]
    [InlineData("demonology",true)]
    [InlineData("destruction",false)]
    [InlineData("destruction",true)]
    public void DefaultsAndCustomPrioritiesReconcileOwnerPetResourceAndThreat(
        string spec,bool custom)
    {
        var request=Setup(spec,custom);
        var run=new WarlockSimRunner().Run(request);
        Assert.True(run.WarlockDamage>0m);
        Assert.True(run.PetDamage>0m);
        Assert.Equal(run.WarlockDamage+run.PetDamage,run.DamageDone);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
        Assert.InRange(run.EndingMana,0m,run.MaximumMana);
        Assert.InRange(run.EndingHealth,0m,request.WarlockMaximumHealth);
        var resource=run.Run.Summary.ActorSummaries["warlock"].EndingResources;
        Assert.InRange(resource["soul-shard"],0m,20m);
        Assert.True(run.Run.Timeline.Any(e=>e.Type==CombatEventType.ThreatChanged &&
            e.SourceActorKey=="warlock" && e.TargetActorKey=="target" &&
            e.Amount>0m));
        var json=JsonSerializer.Serialize(run);
        Assert.Contains("DamageByAbility",json);
        Assert.Contains("PetDamage",json);
        var roundTrip=JsonSerializer.Deserialize<WarlockSimRequest>(
            JsonSerializer.Serialize(request));
        Assert.NotNull(roundTrip);
        Assert.Equal(spec,roundTrip.Specialization);
        Assert.Equal(request.AdditionalTargets,roundTrip.AdditionalTargets);
    }

    [Theory]
    [InlineData("affliction")]
    [InlineData("demonology")]
    [InlineData("destruction")]
    public void CrossSpecSharedCursesAndBanesApplyIndependently(string spec)
    {
        var request=Setup(spec,true);
        request.CustomPriority=["curse-of-the-elements","bane-of-agony","shadow-bolt"];
        request.EnablePet=false;
        request.DurationSeconds=12m;
        var run=new WarlockSimRunner().Run(request);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="curse-of-the-elements" && e.TargetActorKey=="target");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="bane-of-agony" && e.TargetActorKey=="target");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="bane-of-agony" && e.Amount>0m);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }
}
