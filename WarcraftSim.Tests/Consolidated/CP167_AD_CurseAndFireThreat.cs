using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AdCurseAndFireThreat
{
    [Fact]
    public void ElementsRankFourIsAvailableToAllWarlockSpecializations()
    {
        foreach (var spec in new[] {"affliction","demonology","destruction"})
        {
            var spells=ForeverWarlockDpsSpellbook.Create(spec,
                ForeverWarlockTalentBuild.Default(spec),new ForeverWarlockTuning(),0m,0m);
            var curse=Assert.Single(spells,s=>s.Key=="curse-of-the-elements");
            Assert.Equal(200m,Assert.Single(curse.ResourceCosts).Amount);
            Assert.Equal(0m,curse.CastTimeSeconds);
            var effect=Assert.Single(curse.Effects);
            Assert.Equal(300m,effect.DurationSeconds);
            Assert.True(effect.CanMiss);
            Assert.False(effect.CanCrit);
        }
    }

    private static WarlockSimRequest Request(params string[] spells)
    {
        var r=WarlockSimRequest.CreatePreset("affliction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.DurationSeconds=15m;
        r.CaptureTimeline=true;
        r.CustomPriority=spells.ToList();
        return r;
    }

    private static decimal FirstDamage(WarlockSimResponse result,string key) =>
        result.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey==key && e.Amount is >0m).Amount!.Value;

    [Fact]
    public void CurseIncreasesOwnerDirectDamageAndDoesNotRepeatApplication()
    {
        var baseRun=new WarlockSimRunner().Run(Request("shadow-bolt"));
        var boosted=new WarlockSimRunner().Run(Request("curse-of-the-elements","shadow-bolt"));
        Assert.Equal(FirstDamage(baseRun,"shadow-bolt")*1.10m,
            FirstDamage(boosted,"shadow-bolt"));
        Assert.Single(boosted.Run.Timeline.Where(e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="curse-of-the-elements"));
        Assert.InRange(Math.Abs(boosted.DamageDone-boosted.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void CurseIncreasesPeriodicTickDamage()
    {
        var baseRun=new WarlockSimRunner().Run(Request("corruption","shadow-bolt"));
        var boosted=new WarlockSimRunner().Run(Request("curse-of-the-elements","corruption","shadow-bolt"));
        Assert.Equal(FirstDamage(baseRun,"corruption")*1.10m,
            FirstDamage(boosted,"corruption"));
    }

    [Fact]
    public void BurningSoulRanksAreValidated()
    {
        var t=ForeverMageTalentBuild.Default("fire");
        t.Ranks["burning-soul"]=3;
        t.Validate("fire");
        t.Ranks["burning-soul"]=4;
        Assert.Throws<ArgumentException>(()=>t.Validate("fire"));
        t.Ranks["burning-soul"]=1;
        t.FirePointsSpent=10;
        Assert.Throws<ArgumentException>(()=>t.Validate("fire"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BurningSoulReducesFireThreatWithoutChangingDamage(int rank)
    {
        MageSimRequest Setup(int n)
        {
            var r=MageSimRequest.CreatePreset("fire");
            r.DurationSeconds=9m;
            r.BaseSpellHitPercent=100m;
            r.SpellCritPercent=0m;
            r.UseEvocation=false;
            r.UseMajorManaPotion=false;
            r.CombatItems.ManaRubyCharges=0;
            r.CaptureTimeline=true;
            r.CustomPriority=["scorch"];
            r.Talents.Ranks["burning-soul"]=n;
            return r;
        }
        var ordinary=new MageSimRunner().Run(Setup(0));
        var talented=new MageSimRunner().Run(Setup(rank));
        decimal Damage(MageSimResponse r)=>r.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="scorch" &&
            e.Amount is >0m).Amount!.Value;
        decimal Threat(MageSimResponse r)=>r.Run.Timeline.First(e=>
            e.Type==CombatEventType.ThreatChanged && e.AbilityKey=="scorch" &&
            e.Amount is >0m).Amount!.Value;
        Assert.Equal(Damage(ordinary),Damage(talented));
        Assert.Equal(Threat(ordinary)*(1m-.1m*rank),Threat(talented));
    }
}
