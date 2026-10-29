using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168AfflictionIndependentTargetDebuffs
{
    private static WarlockSimRequest Setup()
    {
        var req=WarlockSimRequest.CreatePreset("affliction");
        req.EnablePet=false;
        req.PrePullSacrificedDemon=null;
        req.UseLifeTap=false;
        req.AdditionalTargets=2;
        req.TargetMaximumHealth=1000000m;
        req.DurationSeconds=18m;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.CustomPriority=["curse-of-the-elements","bane-of-agony","shadow-bolt"];
        req.AfflictionAdditionalTargetPriority["target-1"]=
            ["curse-of-recklessness","bane-of-doom"];
        req.AfflictionAdditionalTargetPriority["target-2"]=
            ["corruption","bane-of-agony"];
        req.CaptureTimeline=true;
        return req;
    }

    [Fact]
    public void ThreeTargetsCanRetainDifferentSourceOwnedCursesAndBanes()
    {
        var result=new WarlockSimRunner().Run(Setup());
        void Applied(string target,string spell)=>
            Assert.Contains(result.Run.Timeline,e=>
                e.Type==CombatEventType.AuraApplied &&
                e.SourceActorKey=="warlock" &&
                e.TargetActorKey==target && e.AbilityKey==spell);
        Applied("target","curse-of-the-elements");
        Applied("target","bane-of-agony");
        Applied("target-1","curse-of-recklessness");
        Applied("target-1","bane-of-doom");
        Applied("target-2","corruption");
        Applied("target-2","bane-of-agony");
        Assert.DoesNotContain(result.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied &&
            e.TargetActorKey=="target-1" && e.AbilityKey=="bane-of-agony");
        Assert.DoesNotContain(result.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied &&
            e.TargetActorKey=="target-2" && e.AbilityKey=="curse-of-recklessness");
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void ExplicitTargetDebuffsSerializeAndRejectUnknownTargetOrAbility()
    {
        var req=Setup();
        var copy=JsonSerializer.Deserialize<WarlockSimRequest>(JsonSerializer.Serialize(req));
        Assert.NotNull(copy);
        Assert.Equal("bane-of-doom",copy.AfflictionAdditionalTargetPriority["target-1"][1]);
        req.AfflictionAdditionalTargetPriority["target-9"]=["corruption"];
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(req));
        req.AfflictionAdditionalTargetPriority.Remove("target-9");
        req.AfflictionAdditionalTargetPriority["target-1"]=["shadow-bolt"];
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(req));
    }
}
