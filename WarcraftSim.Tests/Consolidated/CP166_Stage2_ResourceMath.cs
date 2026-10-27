using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp166Stage2ResourceMathTests
{
    [Fact]
    public void MultipleSameResourceCostComponentsMustBeAffordableTogether()
    {
        var actor=Actor(100m,10m);
        var costs=Costs((60m,false),(60m,false));
        Assert.Equal(2m,ResourceAvailabilityCalculator.GetNextAffordableTime(
            actor,costs,0m));
        Assert.Equal(120m,ResourceAvailabilityCalculator
            .ResolveCombinedCosts(actor,costs)!["mana"]);
        Assert.False(Execute(actor,costs).Started);
        // SimulationEnded occurs at t=1. The cast must spend no Mana,
        // while normal passive regeneration still advances 100 -> 110.
        Assert.Equal(110m,actor.Resources["mana"].Current);
    }

    [Fact]
    public void MixedFlatAndPercentOfMaximumCostsAreCombinedWithoutRounding()
    {
        var actor=Actor(149m,0m);
        var costs=Costs((100m,false),(25m,true));
        // 25% of maximum 200 = 50, not 25 Mana.
        Assert.Equal(150m,ResourceAvailabilityCalculator
            .ResolveCombinedCosts(actor,costs)!["mana"]);
        Assert.Null(ResourceAvailabilityCalculator.GetNextAffordableTime(
            actor,costs,0m));
        Assert.False(Execute(actor,costs).Started);
        Assert.Equal(149m,actor.Resources["mana"].Current);
    }

    [Fact]
    public void AffordableCombinedCostsSpendAtomicallyAndReportBalance()
    {
        var actor=Actor(150m,0m);
        var costs=Costs((60m,false),(60m,false));
        var (started,run)=Execute(actor,costs);
        Assert.True(started);
        Assert.Equal(30m,actor.Resources["mana"].Current);
        var spent=Assert.Single(run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="split-cost" && e.ResourceKey=="mana");
        Assert.Equal(-120m,spent.Amount);
        Assert.Equal(30m,spent.ResourceTotalAfter);
    }

    [Fact]
    public void InsufficientCombinedCostDoesNotConsumeChargesOrSpendPartialMana()
    {
        var actor=Actor(100m,0m);
        var costs=Costs((60m,false),(60m,false));
        var (started,run)=Execute(actor,costs);
        Assert.False(started);
        Assert.Equal(100m,actor.Resources["mana"].Current);
        Assert.DoesNotContain(run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="split-cost");
        Assert.DoesNotContain(run.Timeline,e=>e.Type==CombatEventType.AbilityCastStarted &&
            e.AbilityKey=="split-cost");
    }

    [Fact]
    public void ResourceRegenerationAndCapRemainExactAtFractionalTimes()
    {
        var mana=new ResourceState("mana",200m,195m,12m);
        Assert.Equal(3m,mana.AdvanceTo(.25m));
        Assert.Equal(198m,mana.Current);
        Assert.Equal(2m,mana.AdvanceTo(.5m));
        Assert.Equal(200m,mana.Current);
        Assert.Equal(0m,mana.AdvanceTo(5m));
        Assert.False(mana.Spend(201m));
        Assert.Equal(200m,mana.Current);
        Assert.True(mana.Spend(30m));
        Assert.Equal(30m,mana.Gain(100m));
        Assert.Equal(200m,mana.Current);
    }

    [Fact]
    public void ImpossibleAndInvalidCombinedCostsFailExplicitly()
    {
        var actor=Actor(100m,0m);
        Assert.Null(ResourceAvailabilityCalculator.GetNextAffordableTime(
            actor,Costs((201m,false)),0m));
        Assert.Null(ResourceAvailabilityCalculator.GetNextAffordableTime(
            actor,[new AbilityResourceCost{ResourceKey="rage",Amount=1m}],0m));
        Assert.Throws<ArgumentOutOfRangeException>(()=>
            ResourceAvailabilityCalculator.ResolveCombinedCosts(actor,Costs((-1m,false))));
    }

    private static List<AbilityResourceCost> Costs(params (decimal Amount,bool Percent)[] items)=>
        items.Select(item=>new AbilityResourceCost{
            ResourceKey="mana",Amount=item.Amount,IsPercentOfMaximum=item.Percent
        }).ToList();

    private static SimulationActorState Actor(decimal starting,decimal regen)
    {
        var a=new SimulationActorState{Key="caster",Name="Caster",TeamKey="raid",Level=60};
        a.InitializeHealth(1000m);
        a.AddResource(new ResourceState("mana",200m,starting,regen));
        return a;
    }

    private static (bool Started,SimulationRunResult Run) Execute(
        SimulationActorState actor,List<AbilityResourceCost> costs)
    {
        var target=new SimulationActorState{
            Key="target",Name="Target",TeamKey="enemy",Level=60
        };
        target.InitializeHealth(1000m);
        var ability=new AbilityDefinition{
            Key="split-cost",Name="Split cost",ClassKey="mage",
            CastTimeSeconds=0m,GlobalCooldownSeconds=1.5m,
            ResourceCosts=costs,
            Effects=[new AbilityEffectDefinition{
                Key="damage",EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,
                CanMiss=false,CanCrit=false,MinimumValue=10m,MaximumValue=10m,
                MitigationType=DamageMitigationTypes.None
            }]
        };
        actor.AddAbility(ability);
        var context=new SimulationContext(new SimulationRunOptions{
            DurationSeconds=1m,PrimaryActorKey=actor.Key,
            CaptureTimeline=true,Seed=166002
        });
        context.AddActor(actor);
        context.AddActor(target);
        var executor=new AbilityExecutor(new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(new CombatRulesetDefinition{
                RulesetKey="cp166-resources",Version="1"
            }));
        var started=false;
        var run=new SimulationEngine([executor]).Run(context,at=>{
            started=executor.TryStartAbility(at,actor.Key,target.Key,"split-cost").Success;
        });
        return (started,run);
    }
}
