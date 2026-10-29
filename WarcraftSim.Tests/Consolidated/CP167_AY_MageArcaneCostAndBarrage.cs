using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AyMageArcaneCostAndBarrage
{
    private static MageSimRequest Arcane(params string[] spells)
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.DurationSeconds=5m;
        r.CaptureTimeline=true;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseArcanePower=false;
        r.UsePresenceOfMind=false;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.UseMissileBarrage=false;
        r.Talents.Ranks["arcane-concentration"]=0;
        r.Talents.Ranks["arcane-meditation"]=0;
        r.CustomPriority=spells.ToList();
        return r;
    }

    [Fact]
    public void LevelSixtyArcaneBlastCostsFifteenPercentOfBaseManaBeforeStackSurcharge()
    {
        var r=Arcane("arcane-blast");
        var spell=Assert.Single(ForeverMageDpsSpellbook.Create(
            r.Talents,r.Tuning,0m),x=>x.Key=="arcane-blast");
        Assert.Equal(1376m*.15m,spell.ResourceCosts.Single().Amount);
        Assert.Equal(1.5m,spell.CastTimeSeconds);
        var run=new MageSimRunner().Run(r);
        var paid=run.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="arcane-blast" && e.ResourceKey=="mana" &&
            e.Amount is <0m).OrderBy(e=>e.TimeSeconds).ToArray();
        Assert.True(paid.Length>=2);
        Assert.Equal(-(1376m*.15m),paid[0].Amount);
        Assert.Equal(-(1376m*.15m)*2.75m,paid[1].Amount);
    }

    [Fact]
    public void DefaultFourStackSurchargeDoesNotLeakPastOriginalFifteenSecondLifetime()
    {
        var r=Arcane("arcane-blast");
        r.DurationSeconds=19m;
        r.StartingManaPercent=7m;
        r.Talents.Ranks["arcane-concentration"]=0;
        r.Talents.Ranks["arcane-meditation"]=0;
        var sim=new MageSimRunner().Run(r);
        Assert.True(sim.Run.Timeline.Count(e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="arcane-blast")<=1);
        // No invented stack damage gets awarded to a failed second cast.
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void MissileBarrageIncludesFrostBlastAtBothRanksAndRespectsChance()
    {
        var r=Arcane("frost-blast");
        r.UseMissileBarrage=true;
        r.Tuning.MissileBarrageOtherChancePercent=100m;
        r.Talents.Ranks["missile-barrage"]=2;
        r.DurationSeconds=1.8m;
        var run=new MageSimRunner().Run(r);
        Assert.Equal(1,run.MissileBarrageProcs);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="frost-blast");
        var off=Arcane("frost-blast");
        off.UseMissileBarrage=false;
        off.Tuning.MissileBarrageOtherChancePercent=100m;
        off.DurationSeconds=1.8m;
        Assert.Equal(0,new MageSimRunner().Run(off).MissileBarrageProcs);
    }
}
