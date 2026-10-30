using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169SharedFoxHasteRefresh
{
    [Fact]
    public void RecastingTrickstersDanceRefreshesHasteInsteadOfStackingIt()
    {
        var context=new SimulationContext(new SimulationRunOptions{
            DurationSeconds=60m,CaptureTimeline=true
        });
        var hunter=new SimulationActorState {
            Key="hunter",TeamKey="raid",Level=60,MaximumHealth=3000m,
            Stats=new StatCollection()
        };
        hunter.InitializeHealth(3000m);
        hunter.Stats.Set("companion-attack-power",1000m);
        var boss=new SimulationActorState{
            Key="target",TeamKey="enemy",Level=63,MaximumHealth=100000m
        };
        boss.InitializeHealth(100000m);
        context.AddActor(hunter);
        context.AddActor(boss);
        var family=ForeverHunterPetFamilies.Get("fox");
        var tuning=new ForeverBeastMasteryHunterTuning();
        var pet=HunterCompanionComposition.Create(context,hunter,family,tuning);
        var auras=new AuraManager();
        var executor=new AbilityExecutor(auras,new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition{
                RulesetKey="fox-refresh-test",Version="1"
            }));
        var autos=new AutoAttackProcessor(executor);
        var runtime=new HunterSharedCompanionRuntime(
            hunter,pet,boss,family,tuning,auras,autos,
            HunterCompanionComposition.Melee(family,tuning),60m);
        void Cast(decimal at)=>runtime.Process(context,new CombatEvent{
            TimeSeconds=at,Type=CombatEventType.AbilityCastCompleted,
            SourceActorKey=pet.Key,TargetActorKey=boss.Key,
            AbilityKey="pet-tricksters-dance"
        });
        Cast(0m);
        Assert.Equal(30m,pet.Stats.Get("haste-percent"));
        Cast(1m);
        Assert.Equal(30m,pet.Stats.Get("haste-percent"));
        Cast(2m);
        Assert.Equal(30m,pet.Stats.Get("haste-percent"));
    }
}
