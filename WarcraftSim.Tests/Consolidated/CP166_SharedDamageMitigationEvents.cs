using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// CP166 stage 1: the same mitigation metadata contract must be honored by
/// combat abilities, boss attacks and scripted incoming damage. Rage from
/// taking damage must not vary with the defender's actual Armor in Forever.
/// </summary>
public sealed class Cp166SharedDamageMitigationEventTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(400)]
    [InlineData(2000)]
    public void ArmorMitigatedAbilityDamagePreservesTypeAndNormalizedIncomingRage(int armor)
    {
        var (result,warrior)=RunCase(DamageMitigationTypes.Armor,armor);
        var damage=Assert.Single(result.Timeline,e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="incoming-ability");

        Assert.Equal(DamageMitigationTypes.Armor,damage.MitigationType);
        Assert.Equal(200m,damage.RawAmount);
        var reduction=armor==0 ? 0m : (decimal)armor /
            ((decimal)armor+400m+85m*30m);
        Assert.InRange(Math.Abs((damage.Amount??0m)-200m*(1m-reduction)),
            0m,.000001m);
        Assert.InRange(Math.Abs((damage.MitigationPercent??0m)-reduction*100m),
            0m,.000001m);

        // 200 pre-Armor damage, 30% level-calibrated replacement reduction,
        // 1000 reference health, 20 Rage per reference-health share.
        Assert.Equal(2.8m,warrior.Resources["rage"].Current);
        var eventRage=Assert.Single(result.Timeline,e=>
            e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey==ForeverWarriorDamageTakenRageFactory.DefinitionKey);
        Assert.Equal(2.8m,eventRage.Amount);
        Assert.Equal("rage",eventRage.ResourceKey);
        Assert.Equal(2.8m,eventRage.ResourceTotalAfter);
    }

    [Fact]
    public void NoMitigationAbilityIsNotMisclassifiedAsArmor()
    {
        var (result,warrior)=RunCase(DamageMitigationTypes.None,2000);
        var damage=Assert.Single(result.Timeline,e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="incoming-ability");
        Assert.Equal(DamageMitigationTypes.None,damage.MitigationType);
        Assert.Equal(200m,damage.RawAmount);
        Assert.Equal(200m,damage.Amount);
        Assert.Equal(4m,warrior.Resources["rage"].Current);
    }

    [Fact]
    public void MissedDamageStillReportsDeclaredMitigationTypeWithoutGrantingRage()
    {
        var (result,warrior)=RunCase(DamageMitigationTypes.Armor,400,
            hitChance:0m);
        var damage=Assert.Single(result.Timeline,e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="incoming-ability");
        Assert.Equal(DamageMitigationTypes.Armor,damage.MitigationType);
        Assert.Equal(0m,damage.Amount);
        Assert.Equal("miss",damage.ResultKey);
        Assert.Equal(0m,warrior.Resources["rage"].Current);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("armor")]
    public void DamageAndMitigationComponentsReconcileAtBoundaries(string mitigation)
    {
        var (result,_)=RunCase(mitigation,400);
        var damage=Assert.Single(result.Timeline,e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="incoming-ability");
        Assert.True(damage.RawAmount>=0m);
        Assert.True(damage.MitigatedAmount>=0m);
        Assert.InRange(Math.Abs(
            (damage.RawAmount??0m) - (damage.MitigatedAmount??0m) -
            (damage.BlockedAmount??0m) - (damage.AbsorbedAmount??0m) -
            (damage.Amount??0m)),0m,.000001m);
    }

    private static (SimulationRunResult Result,SimulationActorState Warrior)
        RunCase(string mitigation,decimal armor,decimal hitChance=100m)
    {
        var context=new SimulationContext(new SimulationRunOptions{
            Seed=166001,DurationSeconds=1m,
            PrimaryActorKey="warrior",CaptureTimeline=true
        });
        var enemy=new SimulationActorState{
            Key="enemy",Name="Enemy",TeamKey="enemy",Level=30
        };
        enemy.InitializeHealth(1000m);
        var warrior=new SimulationActorState{
            Key="warrior",Name="Warrior",TeamKey="raid",Level=30
        };
        warrior.InitializeHealth(1000m);
        warrior.Stats.Set(ForeverCombatStatKeys.Armor,armor);
        warrior.AddResource(new ResourceState("rage",100m,0m));
        ForeverWarriorDamageTakenRageFactory.Configure(
            warrior,expectedCreatureHealth:1000m,
            expectedArmorReductionPercent:30m);
        context.AddActor(enemy);
        context.AddActor(warrior);
        var ruleset=new CombatRulesetDefinition{
            RulesetKey="cp166-mitigation-events",Version="1",
            MitigationRules={
                [DamageMitigationTypes.Armor]=new DamageMitigationRuleDefinition{
                    MitigationType=DamageMitigationTypes.Armor,
                    FormulaType=DamageMitigationFormulaTypes.RationalLevelScaled,
                    DefenseStatKey=ForeverCombatStatKeys.Armor,
                    BaseConstant=400m,PerAttackerLevelConstant=85m,
                    MaximumReductionPercent=75m
                }
            }
        };
        var executor=new AbilityExecutor(new AuraManager(),
            new SimpleCombatRollResolver(hitChancePercent:hitChance),
            new RulesetDamageMitigationResolver(ruleset));
        var effect=new AbilityEffectDefinition{
            Key="damage",EffectType=AbilityEffectTypes.DirectDamage,
            TargetType=AbilityTargetTypes.Enemy,
            ResolutionType=CombatResolutionTypes.Spell,
            MitigationType=mitigation,SchoolKey="physical",
            CanMiss=true,CanCrit=false,MinimumValue=200m,MaximumValue=200m
        };
        var result=new SimulationEngine([
            new DamageTakenResourceGenerationProcessor()
        ]).Run(context,started=>{
            executor.ExecuteBackgroundDirectDamage(started,enemy,warrior,
                "incoming-ability","Incoming Ability",effect);
        });
        return (result,warrior);
    }
}
