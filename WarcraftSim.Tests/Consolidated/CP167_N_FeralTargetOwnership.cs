using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167NFeralTargetOwnership
{
    [Fact]
    public void CatKeepsIndependentComboPointsWhenReturningToFormerTarget()
    {
        var request=new FeralDruidSimRequest {
            Role="cat",DurationSeconds=24m,AdditionalTargets=1,CaptureTimeline=true,
            UseFeralCooldowns=false,TargetBehind=false,
            TargetTransitions=[
                new() {AtSeconds=8m,TargetKey="target-1"},
                new() {AtSeconds=16m,TargetKey="target"}
            ]
        };
        request.Tuning.MeleeHitPercent=100m;
        request.Tuning.MeleeCritPercent=0m;
        var run=new FeralDruidSimRunner().Run(request);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="druid" && e.TargetActorKey=="target" && e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="druid" && e.TargetActorKey=="target-1" && e.Amount>0m);
        Assert.True(run.ComboPointsByTarget.ContainsKey("target-1"),
            "Switching back to target A must not erase retained points on target B.");
        Assert.InRange(run.ComboPoints,0,5);
        Assert.InRange(run.ComboPointsByTarget["target-1"],1,5);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void ReturningToFirstTargetRestoresItsStoredComboTotal()
    {
        var request=new FeralDruidSimRequest {
            Role="cat",DurationSeconds=18m,AdditionalTargets=1,
            TargetBehind=false,
            TargetTransitions=[
                new() {AtSeconds=6m,TargetKey="target-1"},
                new() {AtSeconds=12m,TargetKey="target"}
            ]
        };
        request.Tuning.MeleeHitPercent=100m;
        request.Tuning.MeleeCritPercent=0m;
        var run=new FeralDruidSimRunner().Run(request);
        Assert.True(run.ComboPointsByTarget.GetValueOrDefault("target")>0);
        Assert.True(run.ComboPointsByTarget.GetValueOrDefault("target-1")>0);
        Assert.Equal(run.ComboPointsByTarget["target"],run.ComboPoints);
    }

    [Fact]
    public void MissedRipRetainsTargetPointsAndRefundsPaidEnergyWithoutBleed()
    {
        var req=new FeralDruidSimRequest {
            Role="cat",DurationSeconds=9m,CaptureTimeline=true,
            TargetBehind=false,
            InitialComboPointsByTarget=new(StringComparer.OrdinalIgnoreCase) {
                ["target"]=5
            }
        };
        req.Tuning.MeleeHitPercent=0m;
        req.Tuning.MeleeCritPercent=0m;
        var result=new FeralDruidSimRunner().Run(req);
        Assert.Equal(5,result.ComboPointsByTarget["target"]);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="rip" && e.ResourceKey=="energy" && e.Amount==-30m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="rip-miss-refund" && e.ResourceKey=="energy" && e.Amount==24m);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="rip");
    }

    [Fact]
    public void RejectsScriptedTargetSwitchToAnUnregisteredEnemy()
    {
        var request=new FeralDruidSimRequest {
            Role="cat",AdditionalTargets=1,
            TargetTransitions=[new() {AtSeconds=4m,TargetKey="target-2"}]
        };
        Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(request));
    }
}
