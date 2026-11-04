using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// CP175-F3C3 70753 source-backed Destruction action and control-event tests.
/// Control auras are explicit encounter fixtures: they DO NOT assert that an
/// enemy's unmodeled movement or AI has been interrupted.
/// </summary>
public sealed class Cp175F3C3DestructionMechanicalCompletion
{
    private static WarlockSimRequest Request(params string[] actions)
    {
        var r=WarlockSimRequest.CreatePreset("destruction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.TargetMaximumHealth=1_000_000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.DurationSeconds=10m;
        r.CaptureTimeline=true;
        r.StartingSoulShards=5;
        r.CustomPriority=actions.ToList();
        return r;
    }

    [Theory]
    [InlineData(0,2.0)]
    [InlineData(1,1.9)]
    [InlineData(3,1.7)]
    [InlineData(5,1.5)]
    public void BaneRankReducesImmolateRealCastDuration(int rank,double expected)
    {
        var req=Request("immolate");
        req.Talents.Ranks["bane"]=rank;
        req.DurationSeconds=7m;
        var spell=Assert.Single(ForeverWarlockDpsSpellbook.Create(
            "destruction",req.Talents,req.Tuning,0m,0m),a=>a.Key=="immolate");
        Assert.Equal((decimal)expected,spell.CastTimeSeconds);
        var run=new WarlockSimRunner().Run(req);
        var started=Assert.Single(run.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastStarted && e.AbilityKey=="immolate");
        var completed=Assert.Single(run.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastCompleted && e.AbilityKey=="immolate");
        Assert.Equal((decimal)expected,completed.TimeSeconds-started.TimeSeconds);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="immolate" && e.ResourceKey=="mana" && e.Amount<0m);
    }

    [Theory]
    [InlineData(0,0)]
    [InlineData(1,20)]
    [InlineData(3,60)]
    [InlineData(5,100)]
    public void AftermathRankMapsToClientDazeProbability(int rank,int chance) =>
        Assert.Equal((decimal)chance,
            WarlockDestructionControlProcProcessor.AftermathDazeChancePercent(rank));

    [Theory]
    [InlineData(0,0)]
    [InlineData(1,13)]
    [InlineData(2,26)]
    public void PyroclasmRankMapsToClientChannelLifetimeChance(int rank,int chance) =>
        Assert.Equal((decimal)chance,
            WarlockDestructionControlProcProcessor.PyroclasmStunChancePercent(rank));

    [Fact]
    public void RankFiveAftermathRequiresLandedConflagrateAndExpiresAfterFiveSeconds()
    {
        var r=Request("immolate","conflagrate");
        r.Talents.Ranks["aftermath"]=5;
        r.TargetAllowsControlAuras=true;
        r.DurationSeconds=9m;
        var run=new WarlockSimRunner().Run(r);
        var hit=Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="conflagrate" && e.Amount is >0m);
        var daze=Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="aftermath" && e.TargetActorKey=="target");
        Assert.Equal(hit.TimeSeconds,daze.TimeSeconds);
        Assert.Equal(1m,run.Run.CustomMetrics["warlock-aftermath-dazes"]);
        Assert.Contains(run.Run.Timeline,e=>
            e.Type==CombatEventType.AuraRemoved && e.TargetActorKey=="target" &&
            e.AbilityKey=="aftermath" && e.TimeSeconds==daze.TimeSeconds+5m);
    }

    [Fact]
    public void AftermathRequiresHitRankAndExplicitSusceptibility()
    {
        WarlockSimResponse Execute(int rank,bool eligible,decimal hitChance)
        {
            var r=Request("immolate","conflagrate");
            r.Talents.Ranks["aftermath"]=rank;
            r.TargetAllowsControlAuras=eligible;
            r.BaseSpellHitPercent=hitChance;
            r.DurationSeconds=7m;
            return new WarlockSimRunner().Run(r);
        }
        Assert.Equal(0m,Execute(0,true,100m).Run.CustomMetrics["warlock-aftermath-dazes"]);
        Assert.Equal(0m,Execute(5,false,100m).Run.CustomMetrics["warlock-aftermath-dazes"]);
        Assert.Equal(0m,Execute(5,true,0m).Run.CustomMetrics["warlock-aftermath-dazes"]);
    }

    [Fact]
    public void PyroclasmDoesNotTurnDirectOrSelfDamageIntoStunProcs()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{Seed=109,DurationSeconds=20m});
        var owner=new SimulationActorState {Key="warlock",Name="Warlock",TeamKey="raid",MaximumHealth=2000m};
        owner.InitializeHealth(2000m);
        var boss=new SimulationActorState {Key="target",Name="Boss",TeamKey="enemy",MaximumHealth=10000m};
        boss.InitializeHealth(10000m);
        ctx.AddActor(owner);ctx.AddActor(boss);
        var auras=new AuraManager();
        var proc=new WarlockDestructionControlProcProcessor(auras,0,2,true,100m);
        void Damage(string spell,CombatEffectDeliveryType delivery,string victim="target",
            Guid? id=null,string actor="warlock",decimal amount=100m)
        {
            proc.Process(ctx,new CombatEvent {
                Type=CombatEventType.Damage,SourceActorKey=actor,TargetActorKey=victim,
                AbilityKey=spell,ResultKey="hit",Amount=amount,
                EffectDeliveryType=delivery,AbilityExecutionId=id
            });
        }
        Damage("soul-fire",CombatEffectDeliveryType.Periodic);
        Damage("rain-of-fire",CombatEffectDeliveryType.Direct);
        Damage("hellfire",CombatEffectDeliveryType.ChannelTick,"warlock",Guid.NewGuid());
        Damage("soul-fire",CombatEffectDeliveryType.Direct,"target",null,"other-warlock");
        Assert.Equal(0,proc.PyroclasmStuns);
        Damage("soul-fire",CombatEffectDeliveryType.Direct);
        Assert.Equal(1,proc.PyroclasmStuns);
        Assert.Contains(boss.ActiveAuras,a=>a.Definition.Key==
            WarlockDestructionControlProcProcessor.PyroclasmAura &&
            a.SourceActorKey=="warlock");
    }

    [Fact]
    public void PyroclasmChannelRollsOncePerEnemyPerCastNotOnEveryTick()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{Seed=110,DurationSeconds=20m});
        var owner=new SimulationActorState {Key="warlock",TeamKey="raid",MaximumHealth=2000m};
        owner.InitializeHealth(2000m);
        ctx.AddActor(owner);
        foreach(var key in new[]{"target","target-1"})
        {
            var enemy=new SimulationActorState {Key=key,TeamKey="enemy",MaximumHealth=10000m};
            enemy.InitializeHealth(10000m);
            ctx.AddActor(enemy);
        }
        var proc=new WarlockDestructionControlProcProcessor(
            new AuraManager(),0,2,true,100m);
        var cast=Guid.NewGuid();
        foreach(var key in new[]{"target","target","target-1","target","target-1"})
            proc.Process(ctx,new CombatEvent {
                Type=CombatEventType.Damage,SourceActorKey="warlock",
                TargetActorKey=key,AbilityKey="rain-of-fire",ResultKey="hit",
                Amount=50m,EffectDeliveryType=CombatEffectDeliveryType.ChannelTick,
                AbilityExecutionId=cast
            });
        Assert.Equal(2,proc.PyroclasmChannelRolls);
        Assert.Equal(2,proc.PyroclasmStuns);
        Assert.All(new[]{"target","target-1"},key=>
            Assert.Single(ctx.GetActor(key)!.ActiveAuras,a=>a.Definition.Key==
                WarlockDestructionControlProcProcessor.PyroclasmAura));
        proc.Process(ctx,new CombatEvent {
            Type=CombatEventType.Damage,SourceActorKey="warlock",
            TargetActorKey="target",AbilityKey="rain-of-fire",ResultKey="miss",
            Amount=0m,EffectDeliveryType=CombatEffectDeliveryType.ChannelTick,
            AbilityExecutionId=Guid.NewGuid()
        });
        Assert.Equal(2,proc.PyroclasmChannelRolls);
    }

    [Fact]
    public void SelectedPyroclasmHonorsIntensityPrerequisiteAndSourceStunGate()
    {
        var r=Request("soul-fire");
        r.Talents.Ranks["pyroclasm"]=2;
        r.Talents.Ranks["intensity"]=3;
        r.TargetAllowsControlAuras=false;
        var result=new WarlockSimRunner().Run(r);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="soul-fire" && e.Amount is >0m);
        Assert.Equal(0m,result.Run.CustomMetrics["warlock-pyroclasm-stuns"]);
        r.Talents.Ranks["intensity"]=2;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(r));
    }

    [Theory]
    [InlineData(0,1.00)]
    [InlineData(1,1.04)]
    [InlineData(3,1.12)]
    [InlineData(5,1.20)]
    public void ShadowAndFlameRanksActuallyScaleNextShadowBolt(int rank,double factor)
    {
        WarlockSimRequest Setup(int selected)
        {
            var r=Request("immolate","conflagrate","shadow-bolt");
            r.Talents.Ranks["shadow-and-flame"]=selected;
            r.DurationSeconds=15m;
            return r;
        }
        var baseRun=new WarlockSimRunner().Run(Setup(0));
        var selectedRun=new WarlockSimRunner().Run(Setup(rank));
        decimal FirstBolt(WarlockSimResponse run)=>run.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="shadow-bolt" &&
            e.Amount is >0m).Amount!.Value;
        Assert.InRange(Math.Abs(FirstBolt(selectedRun)-
            FirstBolt(baseRun)*(decimal)factor),0m,.000001m);
        if(rank>0)Assert.True(selectedRun.Run.CustomMetrics[
            "warlock-shadow-and-flame-shadow-procs"]>0m);
    }

    [Fact]
    public void ConflagrateStillConsumesOwnImmolateAtRankZeroAndPreservesAtRankFive()
    {
        WarlockSimRequest Setup(int rank)
        {
            var r=Request("immolate","conflagrate","shadow-bolt");
            r.Talents.Ranks["shadow-and-flame"]=rank;
            r.DurationSeconds=9m;
            return r;
        }
        var unranked=new WarlockSimRunner().Run(Setup(0));
        var maxed=new WarlockSimRunner().Run(Setup(5));
        var unrankedHit=Assert.Single(unranked.Run.Timeline,e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="conflagrate"&&e.Amount is >0m);
        var maxedHit=Assert.Single(maxed.Run.Timeline,e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="conflagrate"&&e.Amount is >0m);
        Assert.Equal(unrankedHit.Amount,maxedHit.Amount);
        Assert.Contains(unranked.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="immolate" && e.TargetActorKey=="target" &&
            e.TimeSeconds==unrankedHit.TimeSeconds);
        Assert.DoesNotContain(maxed.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="immolate" && e.TimeSeconds==maxedHit.TimeSeconds);
        Assert.Contains(maxed.Run.Timeline,e=>e.AbilityKey=="shadow-and-flame" &&
            e.Type==CombatEventType.AuraApplied);
    }

    [Fact]
    public void RemovingConflagrateOrShadowAndFlameFailsClosed()
    {
        var req=Request("immolate","conflagrate");
        req.Talents.Ranks["conflagrate"]=0;
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(req));
        var noRank=Request("immolate","conflagrate","shadow-bolt");
        noRank.Talents.Ranks["shadow-and-flame"]=0;
        var result=new WarlockSimRunner().Run(noRank);
        Assert.Equal(0m,result.Run.CustomMetrics["warlock-shadow-and-flame-shadow-procs"]);
        Assert.DoesNotContain(result.Run.Timeline,e=>e.AbilityKey=="shadow-and-flame");
    }

    private sealed class Unmitigated : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect,decimal amount) =>
            DamageMitigationResult.Unmitigated(amount);
    }

    [Fact]
    public void ConflagrateCastGateRejectsOtherWarlocksImmolateBeforeManaOrCooldown()
    {
        var context=new SimulationContext(new SimulationRunOptions {
            Seed=175336,DurationSeconds=20m
        });
        var warlock=new SimulationActorState {
            Key="warlock",TeamKey="raid",MaximumHealth=2000m
        };
        warlock.InitializeHealth(2000m);
        warlock.AddResource(new ResourceState("mana",10000m,10000m));
        var other=new SimulationActorState {
            Key="another-warlock",TeamKey="raid",MaximumHealth=2000m
        };
        other.InitializeHealth(2000m);
        var boss=new SimulationActorState {
            Key="target",TeamKey="enemy",MaximumHealth=100000m
        };
        boss.InitializeHealth(boss.MaximumHealth);
        context.AddActor(warlock);context.AddActor(other);context.AddActor(boss);
        var definition=Assert.Single(ForeverWarlockDpsSpellbook.Create(
            "destruction",ForeverWarlockTalentBuild.Default("destruction"),
            new ForeverWarlockTuning(),0m,0m),x=>x.Key=="conflagrate");
        warlock.AddAbility(definition);
        var auras=new AuraManager();
        auras.ApplyAura(context,boss,new AuraDefinition {
            Key="immolate",DurationSeconds=15m
        },other.Key,"immolate","other-caster-immolate");
        var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),
            new Unmitigated());
        var original=warlock.Resources["mana"].Current;
        Assert.False(executor.TryStartAbility(context,"warlock","target","conflagrate").Success);
        Assert.Equal(original,warlock.Resources["mana"].Current);
        auras.ApplyAura(context,boss,new AuraDefinition {
            Key="immolate",DurationSeconds=15m
        },warlock.Key,"immolate","own-immolate");
        Assert.True(executor.TryStartAbility(context,"warlock","target","conflagrate").Success);
    }

    [Fact]
    public void AfflictionStartingPresetExecutesOffTreeDestructionPassives()
    {
        var r=WarlockSimRequest.CreatePreset("affliction");
        r.Talents=ForeverWarlockTalentBuild.Default("destruction");
        r.Talents.Ranks["aftermath"]=5;
        r.Talents.Ranks["shadow-and-flame"]=3;
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.TargetMaximumHealth=1_000_000m;
        r.DurationSeconds=9m;
        r.TargetAllowsControlAuras=true;
        r.CustomPriority=["immolate","conflagrate","shadow-bolt"];
        r.Talents.Validate(60,"affliction");
        var run=new WarlockSimRunner().Run(r);
        Assert.Contains(run.Run.Timeline,e=>e.AbilityKey=="conflagrate" &&
            e.Type==CombatEventType.Damage && e.Amount is >0m);
        Assert.Equal(1m,run.Run.CustomMetrics["warlock-aftermath-dazes"]);
        Assert.True(run.Run.CustomMetrics["warlock-shadow-and-flame-shadow-procs"]>0m);
        var replay=new WarlockSimRunner().Run(r);
        Assert.Equal(run.WarlockDamage,replay.WarlockDamage);
        Assert.Equal(run.Run.CustomMetrics["warlock-aftermath-dazes"],
            replay.Run.CustomMetrics["warlock-aftermath-dazes"]);
    }

    [Fact]
    public void DestructionControlProcAndDamageRunReplaysFromSeed()
    {
        var r=Request("immolate","conflagrate","soul-fire");
        r.Talents.Ranks["aftermath"]=5;
        r.Talents.Ranks["pyroclasm"]=2;
        r.Talents.Ranks["intensity"]=3;
        r.TargetAllowsControlAuras=true;
        r.DurationSeconds=15m;
        r.Seed=175335;
        var a=new WarlockSimRunner().Run(r);
        var b=new WarlockSimRunner().Run(r);
        static string[] Receipts(WarlockSimResponse s) =>
            s.Run.Timeline.Where(x=>x.AbilityKey is "aftermath" or "pyroclasm" or "conflagrate" or "soul-fire")
            .Select(x=>$"{x.TimeSeconds}:{x.Type}:{x.AbilityKey}:{x.TargetActorKey}:{x.Amount}:{x.EffectKey}").ToArray();
        Assert.Equal(Receipts(a),Receipts(b));
    }
}
