using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AeChannelsAndTalents
{
    private static WarlockSimRequest Warlock(string spec,params string[] keys)
    {
        var r=WarlockSimRequest.CreatePreset(spec);
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.CaptureTimeline=true;
        r.DurationSeconds=24m;
        r.CustomPriority=keys.ToList();
        return r;
    }

    [Theory]
    [InlineData(0,8)]
    [InlineData(1,7)]
    [InlineData(2,6)]
    public void FireBlastCooldownFollowsTwoRankForeverTalent(int rank,int seconds)
    {
        var t=ForeverMageTalentBuild.Default("fire");
        t.Ranks["improved-fire-blast"]=rank;
        t.Validate("fire");
        var book=ForeverMageDpsSpellbook.Create(t,new ForeverMageTuning(),0m);
        Assert.Equal((decimal)seconds,Assert.Single(book,a=>a.Key=="fire-blast").CooldownSeconds);
    }

    [Fact]
    public void FireBlastRejectsOldClassicThirdRank()
    {
        var t=ForeverMageTalentBuild.Default("fire");
        t.Ranks["improved-fire-blast"]=3;
        Assert.Throws<ArgumentException>(()=>t.Validate("fire"));
    }

    [Fact]
    public void DrainSoulAndHellfireHaveLevel60ChannelCostsAndEffects()
    {
        var talents=ForeverWarlockTalentBuild.Default("affliction");
        // This assertion covers the untalented level-60 tooltip base amount.
        // CP168 tests the independently ranked Improved Drains multiplier.
        talents.Ranks["improved-drains"]=0;
        talents.Ranks["shadow-mastery"]=0;
        var spells=ForeverWarlockDpsSpellbook.Create("affliction",
            talents,new ForeverWarlockTuning(),0m,0m);
        var soul=Assert.Single(spells,a=>a.Key=="drain-soul");
        Assert.Equal(290m,Assert.Single(soul.ResourceCosts).Amount);
        Assert.Equal(15m,soul.ChannelDurationSeconds);
        Assert.Equal(3m,soul.ChannelTickIntervalSeconds);
        Assert.Equal(84m,Assert.Single(soul.Effects).MinimumValue);
        var hell=Assert.Single(spells,a=>a.Key=="hellfire");
        Assert.Equal(1300m,Assert.Single(hell.ResourceCosts).Amount);
        Assert.Equal(15m,hell.ChannelDurationSeconds);
        Assert.Equal(1m,hell.ChannelTickIntervalSeconds);
        Assert.Equal(2,hell.Effects.Count);
        Assert.Equal(206m,hell.Effects[0].MinimumValue);
        Assert.Equal(206m,hell.Effects[1].MinimumValue);
    }

    [Fact]
    public void DrainSoulGeneratesGuaranteedShardOnEligibleKill()
    {
        var req=Warlock("affliction","drain-soul");
        req.TargetMaximumHealth=40m;
        req.StartingSoulShards=0;
        req.TargetYieldsSoulShard=true;
        var result=new WarlockSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.EffectKey=="drain-soul-kill-shard" && e.Amount==1m);
        Assert.Equal(1m,result.Run.CustomMetrics["warlock-drain-soul-shards"]);
    }

    [Fact]
    public void DrainSoulDoesNotCreateShardsForIneligibleTrainingTarget()
    {
        var req=Warlock("affliction","drain-soul");
        req.TargetMaximumHealth=40m;
        req.StartingSoulShards=0;
        var result=new WarlockSimRunner().Run(req);
        Assert.Equal(0m,result.Run.CustomMetrics["warlock-drain-soul-shards"]);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.EffectKey=="drain-soul-kill-shard");
    }

    [Fact]
    public void DrainSoulSupportsExplicitNonlethalShardChanceWithoutAssumingOne()
    {
        var req=Warlock("affliction","drain-soul");
        req.StartingSoulShards=0;
        req.TargetYieldsSoulShard=true;
        req.DrainSoulShardChancePercent=100m;
        var result=new WarlockSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.EffectKey=="drain-soul-tick-shard");
        Assert.True(result.Run.CustomMetrics["warlock-drain-soul-shards"]>=1m);
    }

    [Fact]
    public void HellfireDealsBothOutgoingAndSelfDamageThroughChannelTicks()
    {
        var req=Warlock("destruction","hellfire");
        req.DurationSeconds=17m;
        req.AdditionalTargets=1;
        var result=new WarlockSimRunner().Run(req);
        var timeline=result.Run.Timeline;
        Assert.Contains(timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="hellfire" && e.TargetActorKey=="target" && e.Amount is >0m);
        Assert.Contains(timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="hellfire" && e.TargetActorKey=="warlock" && e.Amount is >0m);
        Assert.True(result.EndingHealth<req.WarlockMaximumHealth);
    }

    [Fact]
    public void SoulSiphonBoostsDamageFromOwnedActiveAfflictionEffects()
    {
        WarlockSimRequest Setup(int rank)
        {
            var r=Warlock("affliction","corruption","drain-life");
            r.Talents.Ranks["soul-siphon"]=rank;
            return r;
        }
        var a=new WarlockSimRunner().Run(Setup(0));
        var b=new WarlockSimRunner().Run(Setup(3));
        decimal First(WarlockSimResponse run)=>run.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="drain-life" &&
            e.Amount is >0m).Amount!.Value;
        Assert.Equal(First(a)*1.12m,First(b));
    }

    [Fact]
    public void NewTalentPrerequisitesAndRanksRejectInvalidAllocations()
    {
        var t=ForeverWarlockTalentBuild.Default("destruction");
        t.Ranks["fire-and-brimstone"]=3;
        t.Validate(60,"destruction");
        t.Ranks["fire-and-brimstone"]=4;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"destruction"));
        t.Ranks["fire-and-brimstone"]=3;
        t.Ranks.Remove("conflagrate");
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"destruction"));
        var a=ForeverWarlockTalentBuild.Default("affliction");
        a.Ranks["soul-siphon"]=3;
        a.Validate(60,"affliction");
        a.Ranks["soul-siphon"]=4;
        Assert.Throws<ArgumentException>(()=>a.Validate(60,"affliction"));
    }
}
