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
            Role="cat",DurationSeconds=24m,AdditionalTargets=1,
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
    public void RejectsScriptedTargetSwitchToAnUnregisteredEnemy()
    {
        var request=new FeralDruidSimRequest {
            Role="cat",AdditionalTargets=1,
            TargetTransitions=[new() {AtSeconds=4m,TargetKey="target-2"}]
        };
        Assert.Throws<ArgumentException>(()=>new FeralDruidSimRunner().Run(request));
    }
}
