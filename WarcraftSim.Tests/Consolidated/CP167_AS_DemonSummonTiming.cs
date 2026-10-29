using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AsDemonSummonTiming
{
    [Fact]
    public void CurrentFivePermanentDemonSummonsHaveRealTimeManaAndShardContracts()
    {
        foreach(var family in new[]{"imp","voidwalker","succubus","incubus","felhunter"})
        {
            var ranks=ForeverWarlockTalentBuild.Default("demonology");
            var normal=ForeverWarlockSummonSpellbook.Replacement(family,ranks,1000m,false);
            Assert.Equal(10m,normal.CastTimeSeconds);
            Assert.Equal(family=="imp"?800m:1000m,
                normal.ResourceCosts.Single(x=>x.ResourceKey=="mana").Amount);
            Assert.Equal(family=="imp"?0:1,
                normal.ResourceCosts.Count(x=>x.ResourceKey=="soul-shard"));
            ranks.Ranks["master-summoner"]=2;
            ranks.Ranks["fel-domination"]=1;
            ranks.Validate(60,"demonology");
            var enhanced=ForeverWarlockSummonSpellbook.Replacement(family,ranks,1000m,true);
            Assert.Equal(.5m,enhanced.CastTimeSeconds);
            Assert.Equal((family=="imp"?800m:1000m)*.3m,
                enhanced.ResourceCosts.Single(x=>x.ResourceKey=="mana").Amount);
        }
    }

    [Fact]
    public void FelDominationRequiresBothMasterSummonerRanks()
    {
        var t=ForeverWarlockTalentBuild.Default("demonology");
        t.Ranks["fel-domination"]=1;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"demonology"));
        t.Ranks["master-summoner"]=2;
        t.Validate(60,"demonology");
        t.Ranks["master-summoner"]=3;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"demonology"));
        t.Ranks["master-summoner"]=2;
        t.Ranks["fel-domination"]=2;
        Assert.Throws<ArgumentException>(()=>t.Validate(60,"demonology"));
        Assert.Equal(300m,ForeverWarlockSummonSpellbook.FelDomination().CooldownSeconds);
    }

    private static WarlockSimRequest Replacement(int master,bool fel,
        string replaceWith="imp",int shards=3)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType="voidwalker";
        r.PrePullSacrificedDemon=null;
        r.EnablePet=true;
        r.UseLifeTap=false;
        r.DurationSeconds=35m;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.CustomPriority=["bane-of-doom"]; // one initial cast, frees owner's subsequent action window
        r.ModelReplacementSummon=true;
        r.UseFelDominationOnReplacement=fel;
        r.SummonBaseMana=1000m;
        r.DemonReplacementAtSeconds=3m;
        r.ReplacementDemonType=replaceWith;
        r.StartingSoulShards=shards;
        if(master>0)r.Talents.Ranks["master-summoner"]=master;
        if(fel)r.Talents.Ranks["fel-domination"]=1;
        return r;
    }

    [Fact]
    public void CompletedOwnerSummonGatesReplacementPetEngagementAndDpsUptime()
    {
        var slow=new WarlockSimRunner().Run(Replacement(0,false));
        var fast=new WarlockSimRunner().Run(Replacement(2,true));
        Assert.Equal(1m,slow.Run.CustomMetrics["warlock-replacement-summons-completed"]);
        Assert.Equal(1m,fast.Run.CustomMetrics["warlock-replacement-summons-completed"]);
        Assert.Contains(fast.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="fel-domination" && e.SourceActorKey=="warlock");
        Assert.Contains(fast.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="fel-domination" && e.TargetActorKey=="warlock");
        var normalSummon=Assert.Single(slow.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastCompleted && e.AbilityKey=="summon-imp");
        var enhancedSummon=Assert.Single(fast.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastCompleted && e.AbilityKey=="summon-imp");
        Assert.True(enhancedSummon.TimeSeconds < normalSummon.TimeSeconds);
        Assert.True(fast.DemonUptimeSeconds>slow.DemonUptimeSeconds);
        Assert.True(fast.ReplacementDemonDamage>slow.ReplacementDemonDamage);
        Assert.InRange(Math.Abs(fast.DamageDone-fast.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void ShardlessFelhunterReplacementCannotAppearWithoutSuccessfulCast()
    {
        var r=Replacement(2,false,"felhunter",0);
        var result=new WarlockSimRunner().Run(r);
        Assert.Equal(0m,result.Run.CustomMetrics["warlock-replacement-summons-completed"]);
        Assert.Equal(0m,result.ReplacementDemonDamage);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="summon-felhunter");
    }

    [Fact]
    public void LegacyScriptedReplacementRemainsAvailableForExistingProfiles()
    {
        var r=Replacement(0,false);
        r.ModelReplacementSummon=false;
        var result=new WarlockSimRunner().Run(r);
        Assert.Equal(0m,result.Run.CustomMetrics["warlock-replacement-summons-completed"]);
        Assert.True(result.ReplacementDemonDamage>0m);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.AbilityKey=="summon-imp");
    }
}
