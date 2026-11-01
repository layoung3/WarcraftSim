using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Items;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175ConservativeConsumablePolicyTests
{
    private sealed class Unmitigated : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext ctx,
            SimulationActorState source,SimulationActorState target,
            AbilityDefinition ability,AbilityEffectDefinition effect,decimal rawAmount)=>
            DamageMitigationResult.Unmitigated(rawAmount);
    }

    private static (CombatUseItemProcessor Item,SimulationActorState Actor) Run(
        string cls,CombatUseItemsOptions inventory,decimal manaDeficit=0m,
        decimal healthDeficit=0m,decimal rageDeficit=0m)
    {
        var context=new SimulationContext(new SimulationRunOptions{
            Seed=1752026,DurationSeconds=2m,PrimaryActorKey="owner",
            CaptureTimeline=true,MaximumProcessedEvents=5000
        });
        var owner=new SimulationActorState{
            Key="owner",Name="Owner",Level=60,TeamKey="raid",MaximumHealth=5000m
        };
        owner.InitializeHealth(5000m-healthDeficit);
        if(cls is "mage" or "warlock" or "hunter")
            owner.AddResource(new ResourceState("mana",3000m,3000m-manaDeficit));
        if(cls=="warrior")
            owner.AddResource(new ResourceState("rage",100m,100m-rageDeficit));
        context.AddActor(owner);
        var boss=new SimulationActorState{
            Key="target",Name="Boss",Level=63,TeamKey="enemy",MaximumHealth=1000000m};
        boss.InitializeHealth(1000000m);context.AddActor(boss);
        var aura=new AuraManager();
        var executor=new AbilityExecutor(aura,new SimpleCombatRollResolver(),new Unmitigated());
        var item=new CombatUseItemProcessor(inventory,executor,owner,"target",cls);
        var engine=new SimulationEngine([executor,aura,item]);
        engine.Run(context,start=>start.ScheduleEvent(new CombatEvent{
            TimeSeconds=0m,Type=CombatEventType.RotationDecision,
            SourceActorKey="owner",TargetActorKey="target"
        }));
        return(item,owner);
    }

    [Theory]
    [InlineData(1199,0)]
    [InlineData(1200,1)]
    [InlineData(1500,1)]
    public void RubyRequiresMissingManaAtLeastItsMaximumRestoration(
        int deficit,int expected)
    {
        var inventory=new CombatUseItemsOptions{ManaRubyCharges=1,
            ManaRubyBelowManaPercent=100m};
        var (item,actor)=Run("mage",inventory,manaDeficit:deficit);
        Assert.Equal(expected,item.Uses.GetValueOrDefault(ForeverCombatItemCatalog.ManaRuby));
        Assert.Equal(1-expected,item.Remaining.GetValueOrDefault(ForeverCombatItemCatalog.ManaRuby));
        Assert.True(actor.Resources["mana"].Current<=3000m);
    }

    [Theory]
    [InlineData(1499,0)]
    [InlineData(1500,1)]
    public void RunesUseTheirMaximumManaRestoreAndHealthSafety(int deficit,int uses)
    {
        var inventory=new CombatUseItemsOptions{
            DarkRuneCharges=1,RuneBelowManaPercent=100m
        };
        var (item,actor)=Run("warlock",inventory,manaDeficit:deficit);
        Assert.Equal(uses,item.Uses.GetValueOrDefault(ForeverCombatItemCatalog.DarkRune));
        Assert.True(actor.Resources["mana"].Current<=3000m);
        if(uses>0)Assert.True(actor.CurrentHealth<5000m);
    }

    [Fact]
    public void HealingPotionNeedsMaximumHealthDeficitExceptExplicitEmergencyPolicy()
    {
        var inventory=new CombatUseItemsOptions{
            MajorHealingPotionCharges=1,
            MajorHealingPotionBelowHealthPercent=100m
        };
        var at1400=Run("warrior",inventory,healthDeficit:1400m);
        Assert.Equal(0,at1400.Item.Uses.GetValueOrDefault(ForeverCombatItemCatalog.MajorHealingPotion));
        var at1750=Run("warrior",inventory,healthDeficit:1750m);
        Assert.Equal(1,at1750.Item.Uses.GetValueOrDefault(ForeverCombatItemCatalog.MajorHealingPotion));
        inventory.AllowEmergencyHealingOvercap=true;
        var emergency=Run("warrior",inventory,healthDeficit:1400m);
        Assert.Equal(1,emergency.Item.Uses.GetValueOrDefault(ForeverCombatItemCatalog.MajorHealingPotion));
        Assert.True(emergency.Actor.CurrentHealth<=5000m);
    }

    [Fact]
    public void RagePotionRequiresMissingMaximumRageRange()
    {
        var inventory=new CombatUseItemsOptions{
            MightyRagePotionCharges=1,MightyRageBelowRage=100m
        };
        Assert.Equal(0,Run("warrior",inventory,rageDeficit:74m).Item.Uses
            .GetValueOrDefault(ForeverCombatItemCatalog.MightyRagePotion));
        var charged=Run("warrior",inventory,rageDeficit:75m);
        Assert.Equal(1,charged.Item.Uses.GetValueOrDefault(ForeverCombatItemCatalog.MightyRagePotion));
        Assert.True(charged.Actor.Resources["rage"].Current<=100m);
    }

    [Fact]
    public void LegacyHeuristicRequiresExplicitPolicySelection()
    {
        var options=new CombatUseItemsOptions{ManaRubyCharges=1,
            ManaRubyBelowManaPercent=100m,RestorationPolicy="legacy-half-mean"};
        Assert.Equal(1,Run("mage",options,manaDeficit:1100m).Item.Uses
            .GetValueOrDefault(ForeverCombatItemCatalog.ManaRuby));
        Assert.Throws<ArgumentException>(()=>
            new CombatUseItemsOptions{RestorationPolicy="unknown"}.Validate("mage"));
    }

    [Fact]
    public void UnselectedItemsNeverExecuteEvenWithLargeDeficits()
    {
        var empty=new CombatUseItemsOptions{
            ManaRubyBelowManaPercent=100m,RuneBelowManaPercent=100m
        };
        var (item,actor)=Run("mage",empty,manaDeficit:2500m);
        Assert.Empty(item.Uses);
        Assert.Equal(500m,actor.Resources["mana"].Current);
    }
}
