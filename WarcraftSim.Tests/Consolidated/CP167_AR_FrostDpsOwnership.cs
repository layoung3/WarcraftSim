using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167ArFrostDpsOwnership
{
    private static MageSimRequest Request(int shatter,int improvedBlizzard,int seed,string spell,decimal duration)
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.Seed=seed;
        r.DurationSeconds=duration;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.Talents.Ranks["shatter"]=shatter;
        r.Talents.Ranks["improved-blizzard"]=improvedBlizzard;
        r.CustomPriority=[spell];
        return r;
    }

    [Fact]
    public void CurrentShatterAcceptsThreeRanksAndRejectsFormerFiveRankAllocation()
    {
        var r=Request(3,0,1,"frostbolt",10m);
        r.Talents.Validate("frost");
        Assert.Equal(3,r.Talents.Rank("shatter"));
        r.Talents.Ranks["shatter"]=4;
        Assert.Throws<ArgumentException>(()=>r.Talents.Validate("frost"));
    }

    [Fact]
    public void ShatterImprovesActualFrostboltCritsDuringFrozenTreatment()
    {
        int plain=0,shattered=0, procs=0;
        for(var seed=1;seed<=110;seed++)
        {
            var baseline=new MageSimRunner().Run(Request(0,0,seed,"frostbolt",19m));
            var talented=new MageSimRunner().Run(Request(3,0,seed,"frostbolt",19m));
            plain+=baseline.Run.Timeline.Count(e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey=="frostbolt" && e.IsCritical);
            shattered+=talented.Run.Timeline.Count(e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey=="frostbolt" && e.IsCritical);
            procs+=talented.FingersOfFrostProcs;
        }
        Assert.True(procs>0);
        Assert.True(shattered>plain, $"Shatter did not increase critical impacts: {plain} vs {shattered}");
    }

    [Theory]
    [InlineData(1,15)]
    [InlineData(2,25)]
    [InlineData(3,40)]
    public void ImprovedBlizzardMakesRealHitTicksApplyRankSensitiveChill(int rank,int slow)
    {
        var r=Request(3,rank,5,"blizzard",8.5m);
        var result=new MageSimRunner().Run(r);
        Assert.Contains(result.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="blizzard" &&
            e.EffectKey=="improved-blizzard-chill" &&
            e.TargetActorKey=="target");
        Assert.True(result.Run.CustomMetrics["mage-fingers-chill-triggers"]>0m);
        // Movement-control geometry is intentionally absent. Record rank in
        // source-defined aura identity, never invent reduced boss movement.
        Assert.Contains(result.Run.Timeline,e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="blizzard" &&
            e.Amount is >0m);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.000001m);
        Assert.InRange(slow,15,40);
    }

    [Fact]
    public void UntalentedBlizzardCannotProduceChillOrFingersProcs()
    {
        var r=Request(3,0,5,"blizzard",8.5m);
        var result=new MageSimRunner().Run(r);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.EffectKey=="improved-blizzard-chill");
        Assert.Equal(0,result.FingersOfFrostProcs);
    }
}
