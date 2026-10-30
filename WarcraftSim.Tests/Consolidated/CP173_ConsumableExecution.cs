using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp173ConsumableExecution
{
    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect,decimal rawAmount) =>
            DamageMitigationResult.Unmitigated(rawAmount);
    }

    [Fact]
    public void MajorHealingPotionUsesRealSharedAbilityHealingResourceAndPotionCooldown()
    {
        var context=new SimulationContext(new SimulationRunOptions{
            Seed=173,DurationSeconds=12m,PrimaryActorKey="mage",
            CaptureTimeline=true,MaximumProcessedEvents=5000
        });
        var mage=new SimulationActorState {
            Key="mage",Name="Mage",TeamKey="raid",Level=60,MaximumHealth=3000m
        };
        mage.InitializeHealth(3000m,1000m);
        context.AddActor(mage);
        var boss=new SimulationActorState {
            Key="target",Name="Boss",TeamKey="enemy",Level=63,MaximumHealth=100000m
        };
        boss.InitializeHealth(boss.MaximumHealth);
        context.AddActor(boss);
        var aura=new AuraManager();
        var executor=new AbilityExecutor(aura,new SimpleCombatRollResolver(),new NoMitigation());
        var items=new CombatUseItemProcessor(new CombatUseItemsOptions {
            MajorHealingPotionCharges=1,
            MajorHealingPotionBelowHealthPercent=55m
        },executor,mage,"target","mage");
        var run=new SimulationEngine([executor,aura,items]).Run(context,started=>
            started.ScheduleEvent(new CombatEvent {
                TimeSeconds=0m,Type=CombatEventType.RotationDecision,
                SourceActorKey="mage",TargetActorKey="target"
            }));
        Assert.Equal(1,items.Uses["major-healing-potion"]);
        var healed=run.Timeline.Where(x=>x.Type==CombatEventType.Healing &&
            x.SourceActorKey=="mage" && x.AbilityKey=="major-healing-potion")
            .Sum(x=>x.Amount??0m);
        Assert.InRange(healed,1050m,1750m);
        Assert.Equal(1000m+healed,mage.CurrentHealth);
        Assert.False(mage.IsSharedCooldownReady("combat-potion",15m));
        Assert.True(mage.IsSharedCooldownReady("combat-potion",120m));
    }
}
