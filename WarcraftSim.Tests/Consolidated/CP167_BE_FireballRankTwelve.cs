using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BeFireballRankTwelve
{
    private static MageSimRequest Request(decimal hit)
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.DurationSeconds=13m;
        r.CaptureTimeline=true;
        r.BaseSpellHitPercent=hit;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.UseHeatingUp=false;
        r.UseIgnite=false;
        r.Talents.Ranks["critical-mass"]=0;
        r.Talents.Ranks["combustion"]=0;
        r.Talents.Ranks["fire-power"]=0;
        r.StartingManaPercent=11m;
        r.Character.BaseStats.Values["spell-power"]=0m;
        r.CustomPriority=["fireball"];
        return r;
    }

    [Fact]
    public void CurrentClientRankTwelveHasCorrectDirectPeriodicAndManaDefinitions()
    {
        var r=Request(100m);
        var spell=Assert.Single(ForeverMageDpsSpellbook.Create(
            r.Talents,r.Tuning,0m),x=>x.Key=="fireball");
        Assert.Equal(410m,Assert.Single(spell.ResourceCosts).Amount);
        Assert.Equal(483m,spell.Effects[0].MinimumValue);
        Assert.Equal(15m,spell.Effects[1].MinimumValue);
        Assert.Equal(8m,spell.Effects[1].DurationSeconds);
        Assert.Equal(2m,spell.Effects[1].TickIntervalSeconds);
        Assert.Equal("fireball-impact",spell.Effects[1].DependsOnEffectKey);
        Assert.False(spell.Effects[1].CanMiss);
        Assert.False(spell.Effects[1].CanCrit);
    }

    [Fact]
    public void LandedFireballActuallyDeliversFourSeparatePeriodicDamageTicks()
    {
        var run=new MageSimRunner().Run(Request(100m));
        var hits=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="fireball" && e.Amount is >0m).ToArray();
        Assert.Single(hits,e=>!e.IsPeriodic);
        Assert.Equal(4,hits.Count(e=>e.IsPeriodic));
        Assert.Equal(483m,hits.Single(e=>!e.IsPeriodic).Amount);
        Assert.Equal(60m,hits.Where(e=>e.IsPeriodic).Sum(e=>e.Amount??0m));
        Assert.Equal(543m,run.DamageDone);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void MissedFireballAppliesNeitherImpactNorDot()
    {
        var run=new MageSimRunner().Run(Request(0m));
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="fireball" && e.Amount is >0m);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="fireball");
        Assert.Equal(0m,run.DamageDone);
    }
}
