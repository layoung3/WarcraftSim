using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3D3BMendPetDispel
{
    private static (SimulationContext Context,SimulationActorState Pet,
        AuraManager Auras) Scenario()
    {
        var ctx=new SimulationContext(new SimulationRunOptions {
            DurationSeconds=60m,Seed=1753350
        });
        var pet=new SimulationActorState {
            Key="hunter-pet",TeamKey="raid",Level=60,MaximumHealth=2000m
        };
        pet.InitializeHealth(2000m,1000m);
        var enemy=new SimulationActorState {
            Key="target",TeamKey="enemy",Level=63,MaximumHealth=100000m
        };
        enemy.InitializeHealth(100000m);
        var hunter=new SimulationActorState {
            Key="hunter",TeamKey="raid",Level=60,MaximumHealth=3000m
        };
        hunter.InitializeHealth(3000m);
        ctx.AddActor(hunter);
        ctx.AddActor(pet);
        ctx.AddActor(enemy);
        return (ctx,pet,new AuraManager());
    }

    private static void Apply(SimulationContext ctx,SimulationActorState pet,
        AuraManager auras,string key,string tag,string source="target")
        => auras.ApplyAura(ctx,pet,new AuraDefinition {
            Key=key,Name=key,DurationSeconds=30m,Tags=[tag]
        },source,key,key);

    [Fact]
    public void RankSpecificCleanseUsesFifteenAndFiftyPercentRatherThanLinearThirty()
    {
        Assert.Equal(0m,HunterMendPetDispel.Chance(0));
        Assert.Equal(.15m,HunterMendPetDispel.Chance(1));
        Assert.Equal(.50m,HunterMendPetDispel.Chance(2));
        Assert.Throws<ArgumentOutOfRangeException>(()=>HunterMendPetDispel.Chance(3));
        foreach(var (rank,roll,expected) in new [] {
            (0,.01m,false),(1,.149m,true),(1,.15m,false),
            (2,.49m,true),(2,.50m,false)
        }){
            var (ctx,pet,auras)=Scenario();
            Apply(ctx,pet,auras,"magic-debuff","dispel:magic");
            Assert.Equal(expected,HunterMendPetDispel.TryCleanse(ctx,pet,auras,rank,roll));
            Assert.Equal(!expected,pet.ActiveAuras.Any(a=>a.Definition.Key=="magic-debuff"));
        }
    }

    [Theory]
    [InlineData("curse")][InlineData("disease")]
    [InlineData("magic")][InlineData("poison")]
    public void SupportedHostileDebuffsAreCleanseEligible(string type)
    {
        var (ctx,pet,auras)=Scenario();
        Apply(ctx,pet,auras,"eligible","dispel:"+type);
        Assert.True(HunterMendPetDispel.HasEligibleDebuff(ctx,pet));
        Assert.True(HunterMendPetDispel.TryCleanse(ctx,pet,auras,2,0m));
        Assert.DoesNotContain(pet.ActiveAuras,a=>a.Definition.Key=="eligible");
    }

    [Fact]
    public void CleanseRemovesOnlyOneEligibleDebuffAndNeverFriendlyOrUnsupportedAuras()
    {
        var (ctx,pet,auras)=Scenario();
        Apply(ctx,pet,auras,"enemy-curse","dispel:curse");
        Apply(ctx,pet,auras,"enemy-poison","dispel:poison");
        Apply(ctx,pet,auras,"enemy-bleed","dispel:bleed");
        Apply(ctx,pet,auras,"friendly-magic","dispel:magic","hunter");
        Assert.True(HunterMendPetDispel.TryCleanse(ctx,pet,auras,2,.25m));
        Assert.DoesNotContain(pet.ActiveAuras,a=>a.Definition.Key=="enemy-curse");
        Assert.Contains(pet.ActiveAuras,a=>a.Definition.Key=="enemy-poison");
        Assert.Contains(pet.ActiveAuras,a=>a.Definition.Key=="enemy-bleed");
        Assert.Contains(pet.ActiveAuras,a=>a.Definition.Key=="friendly-magic");
        Assert.False(HunterMendPetDispel.TryCleanse(ctx,pet,auras,1,.9m));
    }

    [Fact]
    public void ImprovedMendRanksKeepCorrectResourceCostsAndRealHealing()
    {
        foreach(var (rank,cost) in new [] {(0,210m),(1,189m),(2,168m)})
        {
            var talents=new ForeverBeastMasteryHunterTalents();
            talents.Ranks["improved-mend-pet"]=rank;
            var mend=ForeverBeastMasteryHunterSpellbook.Hunter(talents,
                new ForeverBeastMasteryHunterTuning())
                .Single(a=>a.Key=="mend-pet");
            Assert.Equal(cost,mend.ResourceCosts.Single().Amount);
        }
        var req=new BeastMasteryHunterSimRequest {
            DurationSeconds=40m,Seed=1753351,PetFamily="bear",
            UseMendPet=true,UseRevivePet=false,
            PetIncomingHitDamage=270m,PetIncomingHitEverySeconds=3m,
            UseRapidFire=false,UseSummonHawk=false,
            TargetMaximumHealth=10000000m,CaptureTimeline=true
        };
        var run=new BeastMasteryHunterSimRunner().Run(req);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Healing &&
            e.SourceActorKey=="hunter" && e.TargetActorKey=="hunter-pet" &&
            e.AbilityKey=="mend-pet" && e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="mend-pet" &&
            e.ResourceKey=="mana" && e.Amount==-168m);
    }
    [Fact]
    public void ImprovedReviveRanksChangeRealSpellCostCastTimeAndRestoredPetAttacks()
    {
        foreach(var (rank,cost,cast) in new [] {
            (0,500m,10m),(1,400m,7m),(2,300m,4m)
        }){
            var talents=new ForeverBeastMasteryHunterTalents();
            talents.Ranks["improved-revive-pet"]=rank;
            var reviveAbility=ForeverBeastMasteryHunterSpellbook.Hunter(talents,
                new ForeverBeastMasteryHunterTuning())
                .Single(a=>a.Key=="revive-pet");
            Assert.Equal(cost,reviveAbility.ResourceCosts.Single().Amount);
            Assert.Equal(cast,reviveAbility.CastTimeSeconds);
        }
        var req=new BeastMasteryHunterSimRequest {
            DurationSeconds=38m,Seed=1753352,PetFamily="bear",
            UseRevivePet=true,UseMendPet=false,
            UseBestialWrath=false,UseIntimidation=false,UseRapidFire=false,
            UseSummonHawk=false,UsePetBite=false,UsePetClaw=false,
            UsePetSpecial=false,UseAimedShot=false,UseArcaneShot=false,
            UseSerpentSting=false,
            PetIncomingHitDamage=100000m,PetIncomingHitEverySeconds=6m,
            TargetMaximumHealth=10000000m,CaptureTimeline=true,
            CustomPriority=["revive-pet"],
            Tuning=new ForeverBeastMasteryHunterTuning {
                PetHitPercent=100m,PetAvoidancePercent=0m
            }
        };
        var run=new BeastMasteryHunterSimRunner().Run(req);
        var death=run.Run.Timeline.First(e=>e.Type==CombatEventType.ActorDied &&
            e.TargetActorKey=="hunter-pet");
        var revive=run.Run.Timeline.First(e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="revive-pet" && e.SourceActorKey=="hunter");
        Assert.True(revive.TimeSeconds>death.TimeSeconds);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="revive-pet" &&
            e.ResourceKey=="mana" && e.Amount==-300m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.AbilityKey=="pet-melee" &&
            e.TimeSeconds>revive.TimeSeconds+.5m && e.Amount>0m);
        var replay=new BeastMasteryHunterSimRunner().Run(req);
        Assert.Equal(run.PetDamage,replay.PetDamage);
        Assert.Equal(run.Run.Timeline.Where(e=>e.AbilityKey=="revive-pet")
            .Select(e=>(e.TimeSeconds,e.Type,e.Amount)).ToArray(),
            replay.Run.Timeline.Where(e=>e.AbilityKey=="revive-pet")
            .Select(e=>(e.TimeSeconds,e.Type,e.Amount)).ToArray());
    }

}
