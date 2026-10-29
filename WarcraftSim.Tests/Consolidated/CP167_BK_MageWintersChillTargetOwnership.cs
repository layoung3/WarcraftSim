using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BkMageWintersChillTargetOwnership
{
    private static MageSimRequest Frost(int additional)
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.DurationSeconds=30m;
        r.CaptureTimeline=true;
        r.AdditionalTargets=additional;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseFingersOfFrost=false;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.Tuning.ManaRegenPerSecond=1000m;
        r.CustomPriority=["cone-of-cold","frostbolt"];
        r.Talents.Ranks["winters-chill"]=5;
        r.Character.BaseStats.Values["spell-power"]=0m;
        return r;
    }

    [Fact]
    public void ConeOfColdReadsAndConsumesWintersChillIndependentlyForEachEnemy()
    {
        var run=new MageSimRunner().Run(Frost(1));
        var cones=run.Run.Timeline.Where(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="cone-of-cold" &&
            e.Amount is >0m).OrderBy(e=>e.TimeSeconds).ToArray();
        Assert.True(cones.Length>=4,"Need both targets struck by first and second Cone.");
        var firstTime=cones[0].TimeSeconds;
        var later=cones.Where(e=>e.TimeSeconds>firstTime).ToArray();
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="frostbolt" && e.TargetActorKey=="target" &&
            e.TimeSeconds>firstTime && e.TimeSeconds<later[0].TimeSeconds);
        var primary=Assert.Single(later,e=>e.TargetActorKey=="target" &&
            e.TimeSeconds==later[0].TimeSeconds);
        var add=Assert.Single(later,e=>e.TargetActorKey=="target-1" &&
            e.TimeSeconds==later[0].TimeSeconds);
        Assert.True(primary.Amount>add.Amount,
            $"Primary Winter stacks must not leak to secondary: {primary.Amount} vs {add.Amount}");
        var initialAdd=Assert.Single(cones,e=>e.TargetActorKey=="target-1" &&
            e.TimeSeconds==firstTime);
        Assert.Equal(initialAdd.Amount,add.Amount);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void NoWintersChillRanksMeansNoUnownedConeBonus()
    {
        var r=Frost(1);
        r.Talents.Ranks["winters-chill"]=0;
        var run=new MageSimRunner().Run(r);
        var cones=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="cone-of-cold" && e.Amount is >0m).ToArray();
        Assert.True(cones.Length>=4);
        Assert.All(cones,e=>Assert.Equal(cones[0].Amount,e.Amount));
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="winters-chill");
    }
}
