using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3C1AfflictionMechanics
{
    private static WarlockSimRequest ManaRequest(decimal? targetMana, int improved = 0)
    {
        var r=WarlockSimRequest.CreatePreset("affliction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.DurationSeconds=5.2m;
        r.TargetMaximumHealth=1_000_000m;
        r.TargetStartingMana=targetMana;
        r.StartingManaPercent=20m;
        r.Talents.Ranks["improved-drains"]=improved;
        r.CustomPriority=["drain-mana"];
        r.CaptureTimeline=true;
        r.Tuning.ManaRegenerationPerSecond=0m;
        return r;
    }

    [Theory]
    [InlineData(0,136)]
    [InlineData(1,145.52)]
    [InlineData(2,153.68)]
    [InlineData(3,163.2)]
    public void ImprovedDrainsTransfersExactlyEachRankPerChannelTick(int rank,double expected)
    {
        var run=new WarlockSimRunner().Run(ManaRequest(2000m,rank));
        var transfers=run.Run.Timeline.Where(e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="drain-mana" && e.EffectKey=="drain-mana-caster").ToArray();
        Assert.Equal(5,transfers.Length);
        Assert.Equal(new decimal[]{1m,2m,3m,4m,5m},transfers.Select(x=>x.TimeSeconds).ToArray());
        Assert.All(transfers,x=>Assert.Equal((decimal)expected,x.Amount));
        Assert.Equal((decimal)expected*5m,run.Run.CustomMetrics["warlock-drain-mana-transferred"]);
    }

    [Fact]
    public void TargetExhaustionCapsActualResourceEvents()
    {
        var run=new WarlockSimRunner().Run(ManaRequest(200m));
        var outgoing=run.Run.Timeline.Where(e=>e.AbilityKey=="drain-mana" &&
            e.EffectKey=="drain-mana-target" && e.Type==CombatEventType.ResourceChanged).ToArray();
        var incoming=run.Run.Timeline.Where(e=>e.AbilityKey=="drain-mana" &&
            e.EffectKey=="drain-mana-caster" && e.Type==CombatEventType.ResourceChanged).ToArray();
        Assert.Equal(new decimal[]{-136m,-64m},outgoing.Select(e=>e.Amount!.Value).ToArray());
        Assert.Equal(new decimal[]{136m,64m},incoming.Select(e=>e.Amount!.Value).ToArray());
        Assert.Equal(0m,outgoing[^1].ResourceTotalAfter);
        Assert.Equal(200m,run.Run.CustomMetrics["warlock-drain-mana-transferred"]);
    }

    [Fact]
    public void UnconfiguredTargetNeverGrantsPhantomMana()
    {
        var run=new WarlockSimRunner().Run(ManaRequest(null));
        Assert.Equal(0m,run.Run.CustomMetrics["warlock-drain-mana-transferred"]);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="drain-mana" && e.EffectKey=="drain-mana-caster");
    }

    [Theory]
    [InlineData(1,95,42)]
    [InlineData(2,155,68)]
    [InlineData(3,225,99)]
    [InlineData(4,310,136)]
    public void TrainedDrainManaRanksAndCastAreFinite(int rank,int mana,int tick)
    {
        Assert.Equal(mana,ForeverWarlockDrainMana.ManaCost(rank));
        Assert.Equal(tick,ForeverWarlockDrainMana.BasePerTick(rank));
        var book=ForeverWarlockDpsSpellbook.Create("destruction",
            ForeverWarlockTalentBuild.Default("destruction"),
            new ForeverWarlockTuning(),0m,0m);
        var spell=Assert.Single(book,x=>x.Key=="drain-mana");
        Assert.Equal(5m,spell.ChannelDurationSeconds);
        Assert.Equal(1m,spell.ChannelTickIntervalSeconds);
        Assert.Equal(310m,spell.ResourceCosts.Single().Amount);
    }

    [Fact]
    public void DrainSoulShardsRequireDamageAndChanceNotTargetDeath()
    {
        var ctx=new SimulationContext(new SimulationRunOptions {Seed=99,DurationSeconds=5m});
        var owner=new SimulationActorState {Key="warlock",TeamKey="raid",Name="Warlock"};
        owner.AddResource(new ResourceState("soul-shard",20m,0m));
        ctx.AddActor(owner);
        ctx.AddActor(new SimulationActorState {Key="target",TeamKey="enemy",Name="Enemy"});
        var zero=new WarlockDrainSoulShardProcessor(true,0m);
        var tick=new CombatEvent {Type=CombatEventType.Damage,AbilityKey="drain-soul",
            Amount=10m,ResultKey="hit",EffectDeliveryType=CombatEffectDeliveryType.ChannelTick,
            SourceActorKey="warlock",TargetActorKey="target"};
        zero.Process(ctx,tick);
        zero.Process(ctx,new CombatEvent {Type=CombatEventType.ActorDied,AbilityKey="drain-soul",
            SourceActorKey="warlock",TargetActorKey="target"});
        Assert.Equal(0,zero.Generated);
        var guaranteed=new WarlockDrainSoulShardProcessor(true,100m);
        guaranteed.Process(ctx,tick);
        guaranteed.Process(ctx,new CombatEvent {Type=CombatEventType.ActorDied,
            AbilityKey="drain-soul",SourceActorKey="warlock",TargetActorKey="target"});
        Assert.Equal(1,guaranteed.Generated);
        Assert.Equal(1m,owner.Resources["soul-shard"].Current);
    }

    [Fact]
    public void NightfallAcceptsPeriodicOrChannelButNeverDirect()
    {
        var ctx=new SimulationContext(new SimulationRunOptions {Seed=1,DurationSeconds=20m});
        var owner=new SimulationActorState {Key="warlock",TeamKey="raid",Name="Warlock"};
        ctx.AddActor(owner);
        ctx.AddActor(new SimulationActorState {Key="target",TeamKey="enemy",Name="Enemy"});
        var auras=new AuraManager();
        var nightfall=new WarlockNightfallProcessor(owner,auras,
            new WarlockDecimationProcessor(owner,auras,0),2,testChance:100m);
        var bolt=new AbilityDefinition {Key="shadow-bolt",CastTimeSeconds=3m};
        void Damage(string key,CombatEffectDeliveryType delivery) =>
            nightfall.Process(ctx,new CombatEvent {
                Type=CombatEventType.Damage,AbilityKey=key,Amount=10m,ResultKey="hit",
                SourceActorKey="warlock",TargetActorKey="target",EffectDeliveryType=delivery
            });
        Damage("corruption",CombatEffectDeliveryType.Direct);
        Damage("drain-soul",CombatEffectDeliveryType.Direct);
        Damage("shadow-bolt",CombatEffectDeliveryType.Periodic);
        Assert.Equal(0,nightfall.Procs);
        Assert.Equal(3m,nightfall.Resolve(ctx,owner,bolt).CastTimeSeconds);
        Damage("drain-soul",CombatEffectDeliveryType.ChannelTick);
        Assert.Equal(1,nightfall.Procs);
        Assert.Equal(0m,nightfall.Resolve(ctx,owner,bolt).CastTimeSeconds);
        nightfall.Process(ctx,new CombatEvent {Type=CombatEventType.AbilityCastStarted,
            AbilityKey="shadow-bolt",SourceActorKey="warlock",TargetActorKey="target"});
        Assert.Equal(1,nightfall.Consumed);
    }

    [Fact]
    public void BaneAgonyPeriodicsUseLiveSpellPowerOnlyForThisDot()
    {
        var t=ForeverWarlockTalentBuild.Default("affliction");
        t.Ranks["shadow-mastery"]=0;
        t.Ranks["improved-bane-of-agony"]=0;
        var tune=new ForeverWarlockTuning();
        var spells=ForeverWarlockDpsSpellbook.Create("affliction",t,tune,200m,0m).ToList();
        var stats=new WarcraftSim.Core.Stats.StatCollection();
        ForeverEquipmentDynamicSpellScaling.Enable(
            spells.Where(x=>x.Key=="bane-of-agony").ToList(),
            ForeverWarlockDpsSpellbook.Create("affliction",t,tune,0m,0m),
            ForeverWarlockDpsSpellbook.Create("affliction",t,tune,1m,0m),200m,stats,
            dynamicAgonyTicks:true);
        var agony=spells.Single(x=>x.Key=="bane-of-agony").Effects.Single(x=>x.Key=="bane-tick");
        Assert.Equal("spell-power",agony.ScalingStatKey);
        Assert.Equal(.10m,agony.ScalingCoefficient);
        Assert.Equal(46m,agony.MinimumValue);
        stats.Set("spell-power",400m);
        Assert.Equal(86m,agony.MinimumValue+
            stats.Get(agony.ScalingStatKey!)*agony.ScalingCoefficient);
        Assert.Null(spells.Single(x=>x.Key=="corruption").Effects.Single().ScalingStatKey);
    }

    [Fact]
    public void SeededDrainManaReceiptsReplayExactly()
    {
        var r=ManaRequest(400m,3);
        r.Seed=141519;
        var a=new WarlockSimRunner().Run(r);
        var b=new WarlockSimRunner().Run(r);
        static string[] Receipts(WarlockSimResponse sim) =>
            sim.Run.Timeline.Where(e=>e.AbilityKey=="drain-mana" &&
            e.Type==CombatEventType.ResourceChanged)
            .Select(e=>$"{e.TimeSeconds}:{e.TargetActorKey}:{e.EffectKey}:{e.Amount}:{e.ResourceTotalAfter}").ToArray();
        Assert.Equal(Receipts(a),Receipts(b));
    }
}
