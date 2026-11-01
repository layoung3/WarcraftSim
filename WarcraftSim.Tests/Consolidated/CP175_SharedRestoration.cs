using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Items;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175SharedRestorationTests
{
    private sealed class Unmitigated : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext ctx, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }

    private static (CombatUseItemProcessor Item, SimulationRunResult Run) Simulate(
        string classKey, CombatUseItemsOptions inventory, decimal manaMissing=0m,
        decimal healthMissing=0m, decimal duration=2m)
    {
        var context=new SimulationContext(new SimulationRunOptions {
            Seed=1752026,DurationSeconds=duration,PrimaryActorKey="owner",
            CaptureTimeline=true,MaximumProcessedEvents=30000
        });
        var owner=new SimulationActorState {
            Key="owner",Name="Owner",Level=60,TeamKey="raid",MaximumHealth=5000m
        };
        owner.InitializeHealth(5000m,5000m-healthMissing);
        if(classKey!="warrior")
            owner.AddResource(new ResourceState("mana",5000m,5000m-manaMissing));
        context.AddActor(owner);
        var target=new SimulationActorState {
            Key="target",Name="Target",Level=63,TeamKey="enemy",MaximumHealth=1000000m
        };
        target.InitializeHealth(1000000m);
        context.AddActor(target);
        var aura=new AuraManager();
        var executor=new AbilityExecutor(aura,new SimpleCombatRollResolver(),new Unmitigated());
        var item=new CombatUseItemProcessor(inventory,executor,owner,"target",classKey);
        var engine=new SimulationEngine([executor,aura,item]);
        var run=engine.Run(context,start=>{
            for(decimal t=0m;t<duration;t+=1m)
                start.ScheduleEvent(new CombatEvent {
                    TimeSeconds=t,Type=CombatEventType.RotationDecision,
                    SourceActorKey="owner",TargetActorKey="target"
                });
        });
        return(item,run);
    }

    [Theory]
    [InlineData("mage",2249,0)]
    [InlineData("mage",2250,1)]
    [InlineData("warlock",2250,1)]
    [InlineData("hunter",2250,1)]
    public void MajorManaPotionRequiresMaximumDeficitAndPreparedInventory(string cls,int deficit,int expected)
    {
        var (item,run)=Simulate(cls,new CombatUseItemsOptions{
            MajorManaPotionCharges=1,MajorManaPotionBelowManaPercent=100m
        },manaMissing:deficit);
        Assert.Equal(expected,item.Uses.GetValueOrDefault(ForeverCombatItemCatalog.MajorManaPotion));
        Assert.Equal(1-expected,item.Remaining.GetValueOrDefault(ForeverCombatItemCatalog.MajorManaPotion));
        var restore=run.Timeline.Where(e=>e.AbilityKey==ForeverCombatItemCatalog.MajorManaPotion &&
            e.Type==CombatEventType.ResourceChanged && e.Amount>0m).ToArray();
        Assert.Equal(expected,restore.Length);
        if(expected>0)Assert.InRange(restore[0].Amount!.Value,1350m,2250m);
    }

    [Theory]
    [InlineData("mage")]
    [InlineData("warlock")]
    [InlineData("hunter")]
    [InlineData("warrior")]
    public void MajorHealthstoneRestoresActualHealthOnlyOnce(string cls)
    {
        var (item,run)=Simulate(cls,new CombatUseItemsOptions{
            MajorHealthstoneCharges=1,HealthstoneBelowHealthPercent=100m
        },healthMissing:2000m,duration:125m);
        Assert.Equal(1,item.Uses.GetValueOrDefault(ForeverCombatItemCatalog.MajorHealthstone));
        Assert.Equal(0,item.Remaining.GetValueOrDefault(ForeverCombatItemCatalog.MajorHealthstone));
        Assert.Single(run.Timeline,e=>e.AbilityKey==ForeverCombatItemCatalog.MajorHealthstone &&
            e.Amount==1440m);
    }

    [Fact]
    public void NoPreparedInventoryMeansNoRestoration()
    {
        var (item,run)=Simulate("warlock",new CombatUseItemsOptions{
            MajorManaPotionBelowManaPercent=100m,HealthstoneBelowHealthPercent=100m
        },manaMissing:3000m,healthMissing:3000m);
        Assert.Empty(item.Uses);
        Assert.DoesNotContain(run.Timeline,e=>e.AbilityKey is
            ForeverCombatItemCatalog.MajorManaPotion or ForeverCombatItemCatalog.MajorHealthstone);
    }

    [Fact]
    public void ManaGemAndHealthstoneShareCooldownButPotionDoesNot()
    {
        var (item,_)=Simulate("mage",new CombatUseItemsOptions{
            MajorManaPotionCharges=1,MajorManaPotionBelowManaPercent=100m,
            ManaRubyCharges=1,ManaRubyBelowManaPercent=100m,
            MajorHealthstoneCharges=1,HealthstoneBelowHealthPercent=100m
        },manaMissing:3500m,healthMissing:2000m,duration:30m);
        Assert.Equal(1,item.Uses.GetValueOrDefault(ForeverCombatItemCatalog.MajorManaPotion));
        Assert.Equal(1,item.Uses.GetValueOrDefault(ForeverCombatItemCatalog.ManaRuby)+
            item.Uses.GetValueOrDefault(ForeverCombatItemCatalog.MajorHealthstone));
    }

    [Fact]
    public void InvalidDuplicateHealthstoneInventoryIsRejected()
    {
        Assert.Throws<ArgumentException>(()=>
            new CombatUseItemsOptions{MajorHealthstoneCharges=2}.Validate("warlock"));
    }

    [Fact]
    public void MageLegacyPotionIsConvertedToOneSharedUse()
    {
        var mage=MageSimRequest.CreatePreset("fire");
        mage.StartingManaPercent=10m;
        mage.DurationSeconds=12m;
        mage.UseEvocation=false;
        mage.UseMajorManaPotion=true;
        mage.MajorManaPotionCharges=1;
        mage.MajorManaPotionBelowManaPercent=100m;
        mage.CombatItems=new();
        var result=new MageSimRunner().Run(mage);
        Assert.Equal(1,result.MajorManaPotionUses);
        Assert.Equal(1,result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.MajorManaPotion));
        Assert.Single(result.Run.Timeline,e=>e.AbilityKey==ForeverCombatItemCatalog.MajorManaPotion &&
            e.Type==CombatEventType.ResourceChanged && e.Amount is >=1350m and <=2250m);
    }
}
