using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167SWarlockDecimation
{
    private static WarlockSimRequest Request(int rank, decimal initialHealth=30m)
    {
        var req=WarlockSimRequest.CreatePreset("demonology");
        req.Talents.Ranks["decimation"]=rank;
        req.TargetMaximumHealth=1000000m;
        req.TargetInitialHealthPercent=initialHealth;
        req.DurationSeconds=35m;
        req.EnablePet=false;
        req.PrePullSacrificedDemon=null;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.UseLifeTap=false;
        req.CaptureTimeline=true;
        req.StartingSoulShards=1;
        req.CustomPriority=["soul-fire","shadow-bolt"];
        return req;
    }

    [Theory]
    [InlineData(0,60)]
    [InlineData(1,33)]
    [InlineData(2,6)]
    public void RankReducesSoulFireCooldownAndMaintainsNormalShardRequirement(int rank,int seconds)
    {
        var t=ForeverWarlockTalentBuild.Default("demonology");
        t.Ranks["decimation"]=rank;
        t.Validate(60,"demonology");
        var s=Assert.Single(ForeverWarlockDpsSpellbook.Create("demonology",t,
            new ForeverWarlockTuning(),0m,180m), a=>a.Key=="soul-fire");
        Assert.Equal(seconds,s.CooldownSeconds);
        Assert.Equal(1m,Assert.Single(s.ResourceCosts,c=>c.ResourceKey=="soul-shard").Amount);
    }

    [Fact]
    public void RankTwoSoulFireConsumesShardBeforeProcThenBecomesFreeAndFaster()
    {
        var run=new WarlockSimRunner().Run(Request(2));
        var starts=run.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastStarted &&
            e.AbilityKey=="soul-fire").ToList();
        var ends=run.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="soul-fire").ToList();
        Assert.True(starts.Count>=2);
        Assert.True(ends.Count>=2);
        Assert.Equal(6m,ends[0].TimeSeconds-starts[0].TimeSeconds);
        Assert.Equal(3.6m,ends[1].TimeSeconds-starts[1].TimeSeconds);
        Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="soul-fire" && e.ResourceKey=="soul-shard" && e.Amount==-1m);
        Assert.Equal(0m,run.Run.Summary.ActorSummaries["warlock"].EndingResources["soul-shard"]);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="decimation");
        Assert.True(run.Run.CustomMetrics["warlock-decimation-shard-free-casts"]>=1m);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void AboveThresholdDoesNotGrantShardFreeWindow()
    {
        var run=new WarlockSimRunner().Run(Request(2,80m));
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="decimation");
        Assert.Equal(0m,run.Run.CustomMetrics["warlock-decimation-shard-free-casts"]);
        Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="soul-fire");
    }

    [Theory]
    [InlineData(1,"shadow-bolt")]
    [InlineData(2,"shadow-bolt")]
    [InlineData(1,"searing-pain")]
    [InlineData(2,"searing-pain")]
    public void EligibleDirectSpellAddsRankScaledDamageAndRefreshesWindow(int rank,string key)
    {
        WarlockSimRequest setup(int decimation)
        {
            var req=Request(decimation);
            req.DurationSeconds=8m;
            req.CustomPriority=[key];
            return req;
        }
        var baseRun=new WarlockSimRunner().Run(setup(0));
        var enhanced=new WarlockSimRunner().Run(setup(rank));
        Assert.Contains(enhanced.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="decimation");
        var first=baseRun.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey==key && e.Amount>0m);
        var increased=enhanced.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey==key && e.Amount>0m);
        Assert.Equal(first.Amount!.Value*(1m+.03m*rank),increased.Amount!.Value);
    }

    [Fact]
    public void RejectedRankAndSerializationAreDeterministic()
    {
        var r=Request(3);
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(r));
        var valid=Request(2);
        var serialized=JsonSerializer.Deserialize<WarlockSimRequest>(
            JsonSerializer.Serialize(valid));
        Assert.NotNull(serialized);
        Assert.Equal(30m,serialized.TargetInitialHealthPercent);
        Assert.Equal(2,serialized.Talents.Rank("decimation"));
    }
}
