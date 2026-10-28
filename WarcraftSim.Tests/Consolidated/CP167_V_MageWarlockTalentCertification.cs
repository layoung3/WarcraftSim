using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167VMageWarlockTalentCertification
{
    private static MageSimRequest Scorch()
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.DurationSeconds=15m;
        r.CaptureTimeline=true;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CustomPriority=["scorch"];
        return r;
    }

    [Theory]
    [InlineData(0,0)]
    [InlineData(1,15)]
    [InlineData(2,30)]
    [InlineData(3,45)]
    public void MasterOfElementsReturnsRankScaledBaseManaOnDirectFireCrit(int rank,int refund)
    {
        var req=Scorch();
        req.SpellCritPercent=100m;
        req.Talents.Ranks["master-of-elements"]=rank;
        var run=new MageSimRunner().Run(req);
        var refunds=run.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="master-of-elements" && e.ResourceKey=="mana").ToList();
        if(rank==0)Assert.Empty(refunds);
        else
        {
            Assert.NotEmpty(refunds);
            Assert.All(refunds,e=>Assert.Equal(refund,e.Amount));
            Assert.True(run.Run.CustomMetrics["mage-master-of-elements-refund"]>0m);
        }
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void MasterOfElementsRequiresCurrentFireTierAndValidRank()
    {
        var talents=ForeverMageTalentBuild.Default("fire");
        talents.Ranks["master-of-elements"]=3;
        talents.Validate("fire");
        talents.FirePointsSpent=15;
        Assert.Throws<ArgumentException>(()=>talents.Validate("fire"));
        talents.FirePointsSpent=41;
        talents.Ranks["master-of-elements"]=4;
        Assert.Throws<ArgumentException>(()=>talents.Validate("fire"));
    }

    [Fact]
    public void ImprovedScorchStackingIsPerTargetAndBuffAppliesOnlyAfterLandedHit()
    {
        var control=Scorch();
        var talented=Scorch();
        talented.Talents.Ranks["improved-scorch"]=3;
        var without=new MageSimRunner().Run(control);
        var with=new MageSimRunner().Run(talented);
        var ordinary=without.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="scorch" && e.Amount>0m).ToList();
        var boosted=with.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="scorch" && e.Amount>0m).ToList();
        Assert.True(ordinary.Count>=3);
        Assert.True(boosted.Count>=3);
        Assert.Equal(ordinary[0].Amount,boosted[0].Amount);
        Assert.Equal(ordinary[1].Amount!.Value*1.03m,boosted[1].Amount!.Value);
        Assert.Equal(ordinary[2].Amount!.Value*1.06m,boosted[2].Amount!.Value);
        Assert.True(with.Run.CustomMetrics["mage-improved-scorch-applications"]>=3m);
        Assert.Contains(with.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.EffectKey=="improved-scorch-stack" && e.TargetActorKey=="target");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ImprovedBaneOfAgonyIncreasesRealDotTickDamagePerRank(int rank)
    {
        WarlockSimRequest setup(int r)
        {
            var req=WarlockSimRequest.CreatePreset("affliction");
            req.Talents.Ranks["improved-bane-of-agony"]=r;
            req.EnablePet=false;
            req.PrePullSacrificedDemon=null;
            req.UseLifeTap=false;
            req.BaseSpellHitPercent=100m;
            req.SpellCritPercent=0m;
            req.DurationSeconds=12m;
            req.CustomPriority=["bane-of-agony"];
            return req;
        }
        var baseRun=new WarlockSimRunner().Run(setup(0));
        var enhanced=new WarlockSimRunner().Run(setup(rank));
        var original=baseRun.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="bane-of-agony" && e.Amount is >0m);
        var tick=enhanced.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="bane-of-agony" && e.Amount is >0m);
        Assert.Equal(original.Amount!.Value*(1m+.05m*rank),tick.Amount!.Value);
        Assert.InRange(Math.Abs(enhanced.DamageDone-enhanced.DamageByAbility.Values.Sum()),0m,.000001m);
        var json=JsonSerializer.Deserialize<WarlockSimRequest>(
            JsonSerializer.Serialize(setup(rank)));
        Assert.NotNull(json);
        Assert.Equal(rank,json.Talents.Rank("improved-bane-of-agony"));
    }

    [Fact]
    public void ImprovedBaneOfAgonyRequiresTenEarlierAfflictionPoints()
    {
        var talents=ForeverWarlockTalentBuild.Default("affliction");
        talents.Ranks["improved-bane-of-agony"]=2;
        talents.Validate(60,"affliction");
        talents.AfflictionPointsSpent=10;
        talents.DemonologyPointsSpent=0;
        Assert.Throws<ArgumentException>(()=>talents.Validate(60,"affliction"));
        talents.AfflictionPointsSpent=41;
        talents.Ranks["improved-bane-of-agony"]=3;
        Assert.Throws<ArgumentException>(()=>talents.Validate(60,"affliction"));
    }
}
