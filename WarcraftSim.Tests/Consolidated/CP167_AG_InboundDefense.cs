using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AgInboundDefense
{
    private static MageSimRequest Mage(int shield=0,int burning=0)
    {
        var r=MageSimRequest.CreatePreset(shield>0?"frost":"fire");
        r.DurationSeconds=8m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CaptureTimeline=true;
        r.IncomingDamageAtSeconds=1m;
        r.IncomingDamageAmount=200m;
        r.IncomingDamageSchool="fire";
        r.CustomPriority=shield>0?["ice-barrier","frostbolt"]:["fireball"];
        if(shield>0)r.Talents.Ranks["ice-barrier"]=1;
        r.Talents.Ranks["burning-soul"]=burning;
        return r;
    }

    [Fact]
    public void IceBarrierActuallyAbsorbsExplicitIncomingHit()
    {
        var r=Mage(1);
        var result=new MageSimRunner().Run(r);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AbsorbConsumed &&
            e.AbilityKey=="ice-barrier" && e.Amount==200m);
        Assert.Equal(r.MageMaximumHealth,result.EndingHealth);
    }

    [Fact]
    public void UnshieldedMageTakesRealDamageAndTracksPushback()
    {
        var result=new MageSimRunner().Run(Mage());
        Assert.Equal(2800m,result.EndingHealth);
        Assert.True(result.Run.CustomMetrics["mage-cast-pushback-seconds"]>0m);
    }

    [Fact]
    public void BurningSoulRanksReduceRealHardCastPushbackAcrossDeterministicSeeds()
    {
        decimal baseline=0m,protectedTicks=0m;
        for(var seed=1;seed<=30;seed++)
        {
            var low=Mage(0,0);
            low.Seed=seed;
            var high=Mage(0,3);
            high.Seed=seed;
            baseline+=new MageSimRunner().Run(low).Run.CustomMetrics["mage-cast-pushback-seconds"];
            protectedTicks+=new MageSimRunner().Run(high).Run.CustomMetrics["mage-cast-pushback-seconds"];
        }
        Assert.True(baseline>0m);
        Assert.True(protectedTicks<baseline);
    }

    private static WarlockSimRequest Warlock(int molten)
    {
        var r=WarlockSimRequest.CreatePreset("destruction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.DurationSeconds=6m;
        r.CustomPriority=["shadow-bolt"];
        r.IncomingDamageAtSeconds=1m;
        r.IncomingDamageAmount=1000m;
        r.Talents.Ranks["molten-skin"]=molten;
        return r;
    }

    [Theory]
    [InlineData(0,2500)]
    [InlineData(1,2520)]
    [InlineData(3,2560)]
    [InlineData(5,2600)]
    public void MoltenSkinReducesExplicitIncomingDamage(int rank,int health)
    {
        var result=new WarlockSimRunner().Run(Warlock(rank));
        Assert.Equal((decimal)health,result.EndingHealth);
    }

    [Fact]
    public void MoltenSkinReducesHellfireSelfDamageWithoutWeakeningEnemies()
    {
        WarlockSimRequest Setup(int rank)
        {
            var r=Warlock(rank);
            r.DurationSeconds=5m;
            r.CustomPriority=["hellfire"];
            r.IncomingDamageAtSeconds=null;
            r.IncomingDamageAmount=0m;
            r.Talents.Ranks["molten-skin"]=rank;
            return r;
        }
        var baseRun=new WarlockSimRunner().Run(Setup(0));
        var protectedRun=new WarlockSimRunner().Run(Setup(5));
        decimal Self(WarlockSimResponse run)=>run.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="hellfire" &&
            e.TargetActorKey=="warlock" && e.Amount is >0m).Amount!.Value;
        decimal Enemy(WarlockSimResponse run)=>run.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="hellfire" &&
            e.TargetActorKey=="target" && e.Amount is >0m).Amount!.Value;
        Assert.Equal(Self(baseRun)*.9m,Self(protectedRun));
        Assert.Equal(Enemy(baseRun),Enemy(protectedRun));
    }

    [Fact]
    public void FireAndBrimstoneCritBonusChangesRealConflagrateOutcomes()
    {
        var observed=false;
        for(var seed=1;seed<=40;seed++)
        {
            var req=Warlock(0);
            req.Seed=seed;
            req.DurationSeconds=7m;
            req.CustomPriority=["immolate","conflagrate","shadow-bolt"];
            req.IncomingDamageAtSeconds=null;
            req.IncomingDamageAmount=0m;
            req.Talents.Ranks["fire-and-brimstone"]=3;
            var run=new WarlockSimRunner().Run(req);
            if(run.Run.Timeline.Any(e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey=="conflagrate" && e.IsCritical)) observed=true;
        }
        Assert.True(observed);
    }
}
