using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168DemonologyBrandLifecycle
{
    private sealed class RemoveBrand(AuraManager auras) : ICombatEventProcessor
    {
        public void Process(SimulationContext ctx, CombatEvent ev)
        {
            if(ev.Type==CombatEventType.RotationDecision &&
               ev.EffectKey=="remove-brand")
                auras.RemoveAuras(ctx,ctx.GetActor("boss")!,"demonic-brand",
                    "warlock","early dispel");
        }
    }

    private static (SimulationRunResult Run,WarlockDemonicBrandProcessor Brand) Scenario(
        bool dispel=false,bool petActive=true,bool offTarget=false)
    {
        var ctx=new SimulationContext(new SimulationRunOptions{
            DurationSeconds=5m,CaptureTimeline=true,Seed=168001,
            PrimaryActorKey="warlock"
        });
        var owner=new SimulationActorState{
            Key="warlock",Name="Warlock",TeamKey="raid",MaximumHealth=2500m
        };
        var pet=new SimulationActorState{
            Key="warlock-pet",Name="Felhunter",TeamKey="raid",MaximumHealth=2500m
        };
        var boss=new SimulationActorState{
            Key="boss",Name="Boss",TeamKey="enemy",MaximumHealth=100000m
        };
        var add=new SimulationActorState{
            Key="add",Name="Add",TeamKey="enemy",MaximumHealth=100000m
        };
        foreach(var actor in new[]{owner,pet,boss,add}){
            actor.InitializeHealth(actor.MaximumHealth);
            ctx.AddActor(actor);
        }
        var auras=new AuraManager();
        var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition{
                RulesetKey="cp168-brand",Version="1"
            }));
        var brand=new WarlockDemonicBrandProcessor(3,auras,executor,owner,200m,
            new Dictionary<string,string>{{"warlock-pet","felhunter"}},_=>petActive);
        var run=new SimulationEngine([executor,auras,brand,new RemoveBrand(auras)])
            .Run(ctx,start=>{
                start.ScheduleEvent(new CombatEvent{
                    TimeSeconds=1m,Type=CombatEventType.Damage,
                    SourceActorKey="warlock",TargetActorKey="boss",
                    AbilityKey="searing-pain",ResultKey="hit",Amount=100m
                });
                if(dispel)start.ScheduleEvent(new CombatEvent{
                    TimeSeconds=1.5m,Type=CombatEventType.RotationDecision,
                    EffectKey="remove-brand",IsInternal=true
                });
                start.ScheduleEvent(new CombatEvent{
                    TimeSeconds=2m,Type=CombatEventType.Damage,
                    SourceActorKey="warlock-pet",TargetActorKey=offTarget?"add":"boss",
                    AbilityKey="demon-shadow-bite",ResultKey="hit",Amount=100m
                });
            });
        return (run,brand);
    }

    [Fact]
    public void EarlyBrandRemovalPreventsCachedChargesFromProducingPetDamage()
    {
        var intact=Scenario();
        Assert.Equal(1,intact.Brand.Triggers);
        Assert.Contains(intact.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="demonic-brand-shadow" && e.SourceActorKey=="warlock-pet");
        var dispelled=Scenario(dispel:true);
        Assert.Equal(0,dispelled.Brand.Triggers);
        Assert.DoesNotContain(dispelled.Run.Timeline,e=>e.AbilityKey=="demonic-brand-shadow");
    }

    [Fact]
    public void InactiveCompanionAndWrongEnemyCannotConsumeTargetBrand()
    {
        foreach(var scenario in new[]{Scenario(petActive:false),Scenario(offTarget:true)})
        {
            Assert.Equal(0,scenario.Brand.Triggers);
            Assert.DoesNotContain(scenario.Run.Timeline,e=>
                e.AbilityKey=="demonic-brand-shadow");
        }
    }
}
