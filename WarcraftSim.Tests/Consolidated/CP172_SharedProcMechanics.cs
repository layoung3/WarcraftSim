using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp172SharedProcMechanics
{
    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect,decimal amount) =>
            DamageMitigationResult.Unmitigated(amount);
    }

    private static (SimulationRunResult Run,SimulationActorState Owner)
        Execute(ForeverExecutableEquipmentEffect[] definitions,
            params CombatEvent[] inputs)
    {
        var context=new SimulationContext(new SimulationRunOptions{
            Seed=172,DurationSeconds=10m,PrimaryActorKey="owner",
            CaptureTimeline=true,MaximumProcessedEvents=2000
        });
        SimulationActorState Actor(string key,string team)
        {
            var actor=new SimulationActorState{
                Key=key,Name=key,TeamKey=team,Level=60,MaximumHealth=10000m
            };
            actor.InitializeHealth(10000m);
            context.AddActor(actor);
            return actor;
        }
        var owner=Actor("owner","raid");
        Actor("pet","raid");
        Actor("target","enemy");
        var auras=new AuraManager();
        var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),
            new NoMitigation());
        var processor=new ForeverEquipmentEffectProcessor(
            new CharacterEquipmentLoadout(),"owner","target",auras,executor,172,
            explicitBindings:definitions.Select(x=>new ForeverEquipmentEffectBinding(
                "trinket1",x)).ToArray());
        var engine=new SimulationEngine([executor,auras,processor]);
        var run=engine.Run(context,started=>{
            foreach(var ev in inputs)started.ScheduleEvent(ev);
        });
        processor.WriteMetrics(run);
        return (run,owner);
    }

    private static ForeverExecutableEquipmentEffect StatProc(string key,string trigger) =>
        new(){
            Key=key,Kind="proc-stats",Trigger=trigger,Modeled=true,
            SourceBuild="synthetic-regression",SourceUrl="https://example.test/no-ingestion",
            ProcRateMode="chance",ProcChancePercent=100m,DurationSeconds=2m,
            StatBonuses=new Dictionary<string,decimal>{{"spell-power",10m}}
        };
    private static CombatEvent Hit(decimal time,string source="owner",
        string target="target",bool periodic=false,bool crit=false,
        string? ability=null,string hand="",
        string resolution="spell") =>
        new(){
            TimeSeconds=time,Type=CombatEventType.Damage,
            SourceActorKey=source,TargetActorKey=target,
            AbilityKey=ability??"synthetic-hit",Amount=10m,
            SchoolKey=resolution=="spell"?"fire":"physical",
            ResolutionType=resolution,WeaponHandKey=hand,
            EffectDeliveryType=periodic?CombatEffectDeliveryType.Periodic:
                CombatEffectDeliveryType.Direct,IsCritical=crit
        };

    [Fact]
    public void PeriodicOwnershipChargesStacksExpirationAndProcRecursionStayIsolated()
    {
        var e=StatProc("synthetic-periodic","periodic-damage");
        e.MaxCharges=2;e.MaxStacks=2;
        var (run,owner)=Execute([e],
            Hit(1m),Hit(2m,periodic:true),Hit(3m,periodic:true),
            Hit(4m,source:"pet",periodic:true),
            Hit(4.5m,periodic:true,ability:"equipment-other@trinket2"),
            Hit(5m,periodic:true));
        Assert.Equal(2m,run.CustomMetrics["equipment-synthetic-periodic@trinket1-activations"]);
        Assert.Equal(0m,run.CustomMetrics["equipment-synthetic-periodic@trinket1-charges-left"]);
        Assert.Equal(3m,run.CustomMetrics["equipment-synthetic-periodic@trinket1-uptime-seconds"]);
        Assert.Equal(0m,owner.Stats.Get("spell-power"));
        Assert.Contains(run.Timeline,x=>x.Type==CombatEventType.AuraRemoved &&
            x.EffectKey=="synthetic-periodic@trinket1");
    }

    [Fact]
    public void SpellCriticalAndDamageTakenAreDistinctOwnedTriggers()
    {
        var spell=StatProc("synthetic-spell-crit","spell-crit");
        var taken=StatProc("synthetic-damage-taken","damage-taken");
        spell.MaxCharges=1;taken.MaxCharges=1;
        var (run,_) = Execute([spell,taken],
            Hit(1m,crit:false),Hit(2m,crit:true),
            Hit(3m,source:"target",target:"owner",resolution:"melee"),
            Hit(4m,source:"pet",crit:true),
            Hit(5m,source:"owner",target:"owner",crit:true));
        Assert.Equal(1m,run.CustomMetrics["equipment-synthetic-spell-crit@trinket1-activations"]);
        Assert.Equal(1m,run.CustomMetrics["equipment-synthetic-damage-taken@trinket1-activations"]);
    }

    [Fact]
    public void InternalCooldownUsesEventClockNotGlobalCooldown()
    {
        var e=StatProc("synthetic-icd","spell-hit");
        e.InternalCooldownSeconds=3m;
        var (run,_) = Execute([e],Hit(1m),Hit(2m),Hit(4m),Hit(4.5m));
        Assert.Equal(2m,run.CustomMetrics["equipment-synthetic-icd@trinket1-activations"]);
    }

    [Fact]
    public void OnUseChargeRecoveryAndPersonalCooldownProduceExactActivations()
    {
        var e=new ForeverExecutableEquipmentEffect{
            Key="synthetic-on-use",Kind="on-use",Trigger="manual",Modeled=true,
            SourceBuild="synthetic-regression",SourceUrl="https://example.test/no-ingestion",
            AutoUse=true,CooldownSeconds=1m,DurationSeconds=1m,
            MaxCharges=1,ChargeRecoverySeconds=4m,
            StatBonuses=new Dictionary<string,decimal>{{"spell-power",5m}}
        };
        var (run,owner)=Execute([e]);
        var starts=run.Timeline.Where(x=>x.Type==CombatEventType.AuraApplied &&
            x.EffectKey=="synthetic-on-use@trinket1").Select(x=>x.TimeSeconds).ToArray();
        Assert.Equal(new[]{0m,4m,8m},starts);
        Assert.Equal(3m,run.CustomMetrics["equipment-synthetic-on-use@trinket1-activations"]);
        Assert.Equal(3m,run.CustomMetrics["equipment-synthetic-on-use@trinket1-uptime-seconds"]);
        Assert.Equal(0m,owner.Stats.Get("spell-power"));
    }

    [Fact]
    public void RealPpmTimePolicyUsesElapsedEligibleTimeAndFiniteCharges()
    {
        var e=StatProc("synthetic-rppm","spell-hit");
        e.ProcRateMode="rppm";e.ProcChancePercent=0m;e.RealProcsPerMinute=60m;
        e.MaxCharges=1;
        var (run,_) = Execute([e],Hit(2m),Hit(3m),Hit(4m));
        Assert.Equal(1m,run.CustomMetrics["equipment-synthetic-rppm@trinket1-activations"]);
    }

    [Fact]
    public void SetProcDefinitionAppearsOnlyAtRequiredUniquePieceThreshold()
    {
        var effect=StatProc("synthetic-devilsaur-two-piece","melee-hit");
        effect.SetKey="devilsaur-armor";
        effect.RequiredPieces=2;
        var one=new CharacterEquipmentLoadout{Items=[
            new CharacterEquipmentItem{SlotKey="legs",ItemId=15062}
        ]};
        var two=new CharacterEquipmentLoadout{Items=[
            new CharacterEquipmentItem{SlotKey="legs",ItemId=15062},
            new CharacterEquipmentItem{SlotKey="hands",ItemId=15063}
        ]};
        var duplicate=new CharacterEquipmentLoadout{Items=[
            new CharacterEquipmentItem{SlotKey="legs",ItemId=15062},
            new CharacterEquipmentItem{SlotKey="hands",ItemId=15062}
        ]};
        Assert.Empty(ForeverEquipmentEffectsCatalog.ActiveSetEffects(one,[effect]));
        Assert.Single(ForeverEquipmentEffectsCatalog.ActiveSetEffects(two,[effect]));
        Assert.Empty(ForeverEquipmentEffectsCatalog.ActiveSetEffects(duplicate,[effect]));
        // This is a synthetic activation-policy test. The published Devilsaur
        // two-piece is STATIC and no new live proc rate has been invented.
    }

    [Fact]
    public void ExtraProcDoesNotPerturbFirstProcRngSequence()
    {
        var first=StatProc("synthetic-a","spell-hit");
        first.ProcChancePercent=37m;
        var second=StatProc("synthetic-b","spell-hit");
        second.ProcChancePercent=61m;
        var events=Enumerable.Range(1,9).Select(i=>Hit(i)).ToArray();
        var solo=Execute([first],events).Run;
        var together=Execute([first,second],events).Run;
        var a="equipment-synthetic-a@trinket1-activations";
        Assert.Equal(solo.CustomMetrics.GetValueOrDefault(a),
            together.CustomMetrics.GetValueOrDefault(a));
    }
}
