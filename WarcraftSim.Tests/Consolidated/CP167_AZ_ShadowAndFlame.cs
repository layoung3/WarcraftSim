using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AzShadowAndFlame
{
    private static WarlockSimRequest Request(int ranks,params string[] order)
    {
        var r=WarlockSimRequest.CreatePreset("destruction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.Talents.Ranks["shadow-and-flame"]=ranks;
        r.DurationSeconds=30m;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.StartingSoulShards=3;
        r.TargetYieldsSoulShard=false;
        r.CustomPriority=order.ToList();
        r.CaptureTimeline=true;
        return r;
    }

    private static decimal Damage(WarlockSimResponse result,string spell)=>
        result.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey==spell && e.SourceActorKey=="warlock" &&
            e.Amount is >0m).Amount!.Value;

    [Fact]
    public void LandedShadowburnGrantsFireDamageAndImmediateShardRefundAtRankFive()
    {
        var noTalent=new WarlockSimRunner().Run(Request(0,"shadowburn","immolate","shadow-bolt"));
        var talented=new WarlockSimRunner().Run(Request(5,"shadowburn","immolate","shadow-bolt"));
        Assert.Equal(Damage(noTalent,"immolate")*1.10m,Damage(talented,"immolate"));
        Assert.Equal(1m,talented.Run.CustomMetrics["warlock-shadow-and-flame-fire-procs"]);
        Assert.Equal(1m,talented.Run.CustomMetrics["warlock-shadow-and-flame-instant-shards"]);
        Assert.Contains(talented.Run.Timeline,e=>
            e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="shadowburn" && e.ResourceKey=="soul-shard" &&
            e.Amount==-1m);
        Assert.Contains(talented.Run.Timeline,e=>
            e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="shadow-and-flame" &&
            e.EffectKey=="shadowburn-immediate-shard-refund" &&
            e.ResourceKey=="soul-shard" && e.Amount==1m);
        Assert.InRange(Math.Abs(talented.DamageDone-talented.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void LandedConflagrateGrantsNextShadowSpellBonusButNotItsOwnHit()
    {
        var noTalent=new WarlockSimRunner().Run(Request(0,"immolate","conflagrate","shadow-bolt"));
        var talented=new WarlockSimRunner().Run(Request(5,"immolate","conflagrate","shadow-bolt"));
        Assert.Equal(Damage(noTalent,"shadow-bolt")*1.10m,Damage(talented,"shadow-bolt"));
        Assert.Equal(Damage(noTalent,"conflagrate"),Damage(talented,"conflagrate"));
        Assert.True(talented.Run.CustomMetrics["warlock-shadow-and-flame-shadow-procs"]>0m);
        Assert.Contains(talented.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="shadow-and-flame" && e.TargetActorKey=="warlock");
    }

    [Fact]
    public void MissedShadowburnNeverCreatesBuffOrInstantShardRefund()
    {
        var req=Request(5,"shadowburn","immolate");
        req.BaseSpellHitPercent=0m;
        var run=new WarlockSimRunner().Run(req);
        Assert.Equal(0m,run.Run.CustomMetrics["warlock-shadow-and-flame-fire-procs"]);
        Assert.Equal(0m,run.Run.CustomMetrics["warlock-shadow-and-flame-instant-shards"]);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.AbilityKey=="shadow-and-flame");
    }

    [Fact]
    public void LevelSixtyTalentRejectsRankSix()
    {
        var req=Request(6,"immolate");
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(req));
    }
}
