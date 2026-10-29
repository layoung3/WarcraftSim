using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BqMageIgniteMultipleTargets
{
    [Fact]
    public void IndependentTargetsDoNotShareUnpaidIgniteDamageWhenCritsOverlap()
    {
        var context=new SimulationContext(new SimulationRunOptions {
            DurationSeconds=7m,Seed=167221,PrimaryActorKey="mage",CaptureTimeline=true
        });
        SimulationActorState Actor(string key,string team)
        {
            var a=new SimulationActorState{Key=key,TeamKey=team,Level=60,MaximumHealth=2000m};
            a.InitializeHealth(2000m);
            return a;
        }
        var mage=Actor("mage","raid");
        mage.AddAbility(ForeverMageDpsSpellbook.Create(
            ForeverMageTalentBuild.Default("fire"),new ForeverMageTuning(),0m)
            .Single(a=>a.Key=="mage-ignite"));
        var first=Actor("target","enemy");
        var other=Actor("target-1","enemy");
        context.AddActor(mage);context.AddActor(first);context.AddActor(other);
        var auras=new AuraManager();
        var executor=new AbilityExecutor(auras,
            new SimpleCombatRollResolver(hitChancePercent:100m,criticalChancePercent:0m),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition{
                RulesetKey="cp167-ignite-per-target",Version="1"
            }));
        var processor=new MageIgniteRollingProcessor(auras,5);
        var run=new SimulationEngine([executor,auras,processor]).Run(context,start=>
        {
            foreach(var (time,target,damage) in new[]{
                (.1m,"target",100m),(.2m,"target-1",200m),(.3m,"target",300m)})
                start.ScheduleEvent(new CombatEvent{
                    TimeSeconds=time,Type=CombatEventType.Damage,
                    SourceActorKey="mage",TargetActorKey=target,
                    AbilityKey="fireball",SchoolKey="fire",
                    EffectDeliveryType=CombatEffectDeliveryType.Direct,
                    IsCritical=true,ResultKey="critical",
                    RawAmount=damage,Amount=damage
                });
        });
        var ticks=run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="mage-ignite" && e.IsPeriodic).ToArray();
        Assert.Equal(2,ticks.Count(x=>x.TargetActorKey=="target"));
        Assert.Equal(2,ticks.Count(x=>x.TargetActorKey=="target-1"));
        Assert.Equal(160m,ticks.Where(x=>x.TargetActorKey=="target").Sum(x=>x.Amount??0m));
        Assert.Equal(80m,ticks.Where(x=>x.TargetActorKey=="target-1").Sum(x=>x.Amount??0m));
        Assert.Equal(240m,run.Summary.ActorSummaries["mage"].DamageDoneByAbility["mage-ignite"]);
        Assert.Equal(1840m,first.CurrentHealth);
        Assert.Equal(1920m,other.CurrentHealth);
    }
}