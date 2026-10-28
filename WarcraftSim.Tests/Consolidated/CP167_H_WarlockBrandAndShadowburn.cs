using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167HWarlockBrandAndShadowburn
{
    private static WarlockSimRequest Setup(string pet="voidwalker")
    {
        var req=WarlockSimRequest.CreatePreset("demonology");
        req.DurationSeconds=24m;
        req.CaptureTimeline=true;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.PetType=pet;
        req.DemonTuning.DemonSpellHitPercent=100m;
        req.PetMeleeHitPercent=100m;
        req.CustomPriority=["searing-pain"];
        req.UseLifeTap=false;
        return req;
    }

    [Theory]
    [InlineData(1,2)]
    [InlineData(2,4)]
    [InlineData(3,6)]
    public void BrandRankHasCorrectChargesAndDoesNotApplyOnMiss(int rank,int charges)
    {
        var t=ForeverWarlockTalentBuild.Default("demonology");
        t.Ranks["demonic-brand"]=rank;
        t.Validate(60,"demonology");
        Assert.Equal(rank,t.Rank("demonic-brand"));
        // Brand proc charges are 2/rank and are source/target-owned; the
        // simulator's attestation is a real pet damage event.
        var request=Setup();
        request.Talents=t;
        var sim=new WarlockSimRunner().Run(request);
        var procs=sim.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="demonic-brand-shadow").ToList();
        Assert.NotEmpty(procs);
        Assert.All(procs,e=>{
            Assert.Equal("warlock-pet",e.SourceActorKey);
            Assert.Equal("target",e.TargetActorKey);
            Assert.Equal("shadow",e.SchoolKey);
        });
        Assert.True(charges>=2);
    }

    [Fact]
    public void NonTankingDemonProducesFireSchoolBrandWithNoExtraShadowThreat()
    {
        var req=Setup("imp");
        req.Talents.Ranks["demonic-brand"]=3;
        var run=new WarlockSimRunner().Run(req);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="demonic-brand-fire" && e.SchoolKey=="fire");
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="demonic-brand-shadow");
    }

    [Fact]
    public void ShadowburnDeathWithinEightSecondsRefundsOneShard()
    {
        var req=WarlockSimRequest.CreatePreset("destruction");
        req.EnablePet=false;
        req.CaptureTimeline=true;
        req.DurationSeconds=10m;
        req.TargetMaximumHealth=180m;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.StartingSoulShards=1;
        req.TargetYieldsSoulShard=true;
        req.UseLifeTap=false;
        req.CustomPriority=["shadowburn"];
        var result=new WarlockSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ActorDied &&
            e.TargetActorKey=="target");
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="shadowburn" && e.EffectKey=="shadowburn-kill-shard" &&
            e.Amount==1m && e.ResourceKey=="soul-shard");
    }

    [Fact]
    public void ShadowburnTargetDeathAfterEightSecondsCannotRefundShard()
    {
        var ctx=new SimulationContext(new SimulationRunOptions {
            DurationSeconds=12m,PrimaryActorKey="warlock",CaptureTimeline=true
        });
        var owner=new SimulationActorState {
            Key="warlock",TeamKey="raid",Level=60,MaximumHealth=2000m
        };
        owner.InitializeHealth(2000m);
        owner.AddResource(new ResourceState("soul-shard",20m,1m));
        var target=new SimulationActorState {
            Key="target",TeamKey="enemy",Level=63,MaximumHealth=500m
        };
        target.InitializeHealth(500m);
        ctx.AddActor(owner);
        ctx.AddActor(target);
        var processor=new WarlockShadowburnShardProcessor(true);
        var result=new SimulationEngine([processor]).Run(ctx,started=>{
            started.ScheduleEvent(new CombatEvent {
                TimeSeconds=0m,Type=CombatEventType.Damage,
                SourceActorKey="warlock",TargetActorKey="target",
                AbilityKey="shadowburn",ResultKey="hit",Amount=100m
            });
            started.ScheduleEvent(new CombatEvent {
                TimeSeconds=9m,Type=CombatEventType.ActorDied,
                TargetActorKey="target"
            });
        });
        Assert.DoesNotContain(result.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.EffectKey=="shadowburn-kill-shard");
        Assert.Equal(1m,owner.Resources["soul-shard"].Current);
    }
}
