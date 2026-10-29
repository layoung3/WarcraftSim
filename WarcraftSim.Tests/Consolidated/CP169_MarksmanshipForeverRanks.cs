using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169MarksmanshipForeverRankCorrections
{
    private static ForeverMarksmanshipHunterTalents Talents(int efficiency)
    {
        var t=new ForeverMarksmanshipHunterTalents();
        t.Ranks["efficiency"]=efficiency;
        return t;
    }

    [Fact]
    public void RankEightArcaneRankNineSerpentAndCurrentMultiShotUseForever70532BaseValues()
    {
        var t=new ForeverMarksmanshipHunterTuning();
        var talent=Talents(0);
        talent.Ranks["lone-wolf"]=0; // Assert untalented client rank values.
        talent.Ranks["improved-stings"]=0;
        var b=ForeverMarksmanshipHunterSpellbook.Create(talent,t,1000m);
        var arcane=Assert.Single(b,a=>a.Key=="arcane-shot");
        Assert.Equal(190m,arcane.ResourceCosts.Single().Amount);
        Assert.Equal(217m,arcane.Effects.Single().MinimumValue);
        Assert.Equal(6m,arcane.CooldownSeconds);

        var sting=Assert.Single(b,a=>a.Key=="serpent-sting");
        Assert.Equal(250m,sting.ResourceCosts.Single().Amount);
        var tick=Assert.Single(sting.Effects,e=>e.Key=="serpent-sting-tick");
        Assert.Equal(111m,tick.MinimumValue);
        Assert.Equal(15m,tick.DurationSeconds);
        Assert.Equal(3m,tick.TickIntervalSeconds);
        Assert.Equal(555m,tick.MinimumValue*5m);

        var multi=Assert.Single(b,a=>a.Key=="multi-shot");
        var aimed=Assert.Single(b,a=>a.Key=="aimed-shot");
        Assert.Equal(6m,multi.CooldownSeconds);
        Assert.Equal(6m,aimed.CooldownSeconds);
        Assert.Equal(aimed.SharedCooldownGroupKey,multi.SharedCooldownGroupKey);
        Assert.Equal(3,multi.Effects.Single().MaxTargets);
        // Single surviving Forever rank no longer adds Classic +150 flat damage.
        Assert.Equal(0m,t.MultiShotBonusDamage);
    }

    [Theory]
    [InlineData(0,1.00)]
    [InlineData(1,.97)]
    [InlineData(2,.94)]
    [InlineData(3,.91)]
    [InlineData(4,.88)]
    [InlineData(5,.85)]
    public void EfficiencyRanksDiscountRealRangedAndStingManaCosts(int rank,double factor)
    {
        var book=ForeverMarksmanshipHunterSpellbook.Create(Talents(rank),
            new ForeverMarksmanshipHunterTuning(),1000m);
        decimal Cost(string key)=>book.Single(a=>a.Key==key).ResourceCosts.Single().Amount;
        Assert.Equal(190m*(decimal)factor,Cost("arcane-shot"));
        Assert.Equal(250m*(decimal)factor,Cost("serpent-sting"));
        Assert.Equal(310m*(decimal)factor,Cost("aimed-shot"));
        Assert.Equal(365m*(decimal)factor,Cost("sniper-shot"));
    }

    [Fact]
    public void CorrectedRanksProduceActualSerpentTicksAndSpendManaInCombat()
    {
        var request=new MarksmanshipHunterSimRequest {
            DurationSeconds=16m,CaptureTimeline=true,
            UseRapidFire=false,UseSniperShot=false,UseAimedShot=false,
            UseArcaneShot=false,UseMultiShot=false,CustomPriority=["serpent-sting"]
        };
        request.Tuning.HitChancePercent=100m;
        var run=new MarksmanshipHunterSimRunner().Run(request);
        var ticks=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="serpent-sting" && e.IsPeriodic && e.Amount>0m).ToArray();
        Assert.Equal(5,ticks.Length);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="serpent-sting" && e.ResourceKey=="mana" &&
            e.Amount==-212.5m);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }
}
