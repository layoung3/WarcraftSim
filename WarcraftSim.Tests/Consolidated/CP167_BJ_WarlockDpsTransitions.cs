using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BjWarlockDpsTransitionClosure
{
    private static WarlockSimRequest Demonology(string family="succubus")
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType=family;
        r.EnablePet=true;
        r.PrePullSacrificedDemon="imp";
        r.TargetMaximumHealth=1000000m;
        r.DurationSeconds=28m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseLifeTap=false;
        r.CaptureTimeline=true;
        r.CustomPriority=["shadow-bolt"];
        return r;
    }

    [Fact]
    public void PactRetainsOtherFamilySacrificeThenCancelsWhenOriginalFamilyIsSummoned()
    {
        var retained=Demonology();
        retained.DemonReplacementAtSeconds=10m;
        retained.ReplacementDemonType="felhunter";
        var unchanged=new WarlockSimRunner().Run(retained);
        Assert.True(unchanged.DemonicPactTalented);
        Assert.Equal("imp",unchanged.SacrificeBuffAtPull);
        Assert.Equal("imp",unchanged.SacrificeBuffAtEnd);
        Assert.True(unchanged.ReplacementDemonDamage>0m);

        var canceled=Demonology();
        canceled.DemonReplacementAtSeconds=10m;
        canceled.ReplacementDemonType="imp";
        var changed=new WarlockSimRunner().Run(canceled);
        Assert.Equal("imp",changed.SacrificeBuffAtPull);
        Assert.Null(changed.SacrificeBuffAtEnd);
        Assert.Contains(changed.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="demonic-sacrifice" && e.TargetActorKey=="warlock");
    }

    [Fact]
    public void SoulLinkOwnerDamageBonusDisappearsAfterCompanionDeath()
    {
        var linked=Demonology("imp");
        linked.PrePullSacrificedDemon=null;
        linked.DemonDeathAtSeconds=12m;
        var unlinked=Demonology("imp");
        unlinked.PrePullSacrificedDemon=null;
        unlinked.Talents.Ranks["soul-link"]=0;
        // Pact requires Soul Link; without a pre-pull Sacrifice its removal
        // changes no combat effect. Keep both configurations legal.
        unlinked.Talents.Ranks["demonic-pact"]=0;
        unlinked.DemonDeathAtSeconds=12m;
        var with=new WarlockSimRunner().Run(linked);
        var without=new WarlockSimRunner().Run(unlinked);
        decimal First(WarlockSimResponse run,bool after)=>
            run.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey=="shadow-bolt" && e.SourceActorKey=="warlock" &&
                e.Amount is >0m && (after?e.TimeSeconds>12m:e.TimeSeconds<12m))
                .Amount!.Value;
        Assert.Equal(First(without,false)*1.03m,First(with,false));
        Assert.Equal(First(without,true),First(with,true));
        Assert.True(with.DemonDied);
        Assert.InRange(Math.Abs(with.DamageDone-with.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void ShadowburnRequiresActualShardAndMissNeverRefundsWithoutKill()
    {
        var empty=WarlockSimRequest.CreatePreset("destruction");
        empty.EnablePet=false;empty.PrePullSacrificedDemon=null;
        empty.StartingSoulShards=0;
        empty.UseLifeTap=false;
        empty.BaseSpellHitPercent=100m;
        empty.TargetYieldsSoulShard=true;
        empty.CustomPriority=["shadowburn"];
        empty.DurationSeconds=8m;
        var noShard=new WarlockSimRunner().Run(empty);
        Assert.DoesNotContain(noShard.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="shadowburn");
        var missed=WarlockSimRequest.CreatePreset("destruction");
        missed.EnablePet=false;missed.PrePullSacrificedDemon=null;
        missed.StartingSoulShards=1;
        missed.BaseSpellHitPercent=0m;
        missed.UseLifeTap=false;
        missed.TargetYieldsSoulShard=true;
        missed.CustomPriority=["shadowburn"];
        missed.DurationSeconds=8m;
        var result=new WarlockSimRunner().Run(missed);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="shadowburn" && e.ResourceKey=="soul-shard" && e.Amount==-1m);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.EffectKey=="shadowburn-kill-shard");
        Assert.Equal(0m,result.Run.Summary.ActorSummaries["warlock"].EndingResources["soul-shard"]);
    }
}
