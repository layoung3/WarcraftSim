using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169HunterSharedPetCombatRules
{
    private static (SimulationContext Context,SimulationActorState Pet,
        SimulationActorState Boss,ForeverBeastMasteryHunterTuning Tuning) Setup()
    {
        var ctx=new SimulationContext(new SimulationRunOptions{DurationSeconds=10m,Seed=1691});
        var hunter=new SimulationActorState {
            Key="hunter",TeamKey="raid",Level=60,
            MaximumHealth=2000m,Stats=new StatCollection()
        };
        hunter.InitializeHealth(2000m);
        hunter.Stats.Set("companion-attack-power",1000m);
        hunter.Stats.Set("ranged-hit-bonus-percent",0m);
        hunter.Stats.Set("ranged-critical-chance-percent",0m);
        hunter.Stats.Set("melee-critical-chance-percent",0m);
        var boss=new SimulationActorState {
            Key="target",TeamKey="enemy",Level=63,MaximumHealth=100000m
        };
        boss.InitializeHealth(100000m);
        ctx.AddActor(hunter);ctx.AddActor(boss);
        var tuning=new ForeverBeastMasteryHunterTuning{
            PetHitPercent=100m,PetAvoidancePercent=0m
        };
        var pet=HunterCompanionComposition.Create(ctx,hunter,
            ForeverHunterPetFamilies.Get("wind-serpent"),tuning);
        return(ctx,pet,boss,tuning);
    }

    [Fact]
    public void PetUsesHigherEligibleLiveOwnerCriticalChanceForMagicAndMelee()
    {
        var (ctx,pet,boss,tuning)=Setup();
        var hunter=ctx.GetActor("hunter")!;
        var physical=new AbilityEffectDefinition {
            ResolutionType=CombatResolutionTypes.Melee,
            CanMiss=true,CanCrit=true,CanBeDodged=true,CanBeParried=true
        };
        var magical=new AbilityEffectDefinition {
            ResolutionType=CombatResolutionTypes.Spell,CanMiss=true,CanCrit=true
        };
        Assert.False(HunterCompanionComposition.Resolve(ctx,pet,boss,physical,tuning).IsCritical);
        Assert.False(HunterCompanionComposition.Resolve(ctx,pet,boss,magical,tuning).IsCritical);
        hunter.Stats.Set("melee-critical-chance-percent",100m);
        Assert.True(HunterCompanionComposition.Resolve(ctx,pet,boss,physical,tuning).IsCritical);
        Assert.True(HunterCompanionComposition.Resolve(ctx,pet,boss,magical,tuning).IsCritical);
        hunter.Stats.Set("melee-critical-chance-percent",0m);
        hunter.Stats.Set("ranged-critical-chance-percent",100m);
        Assert.True(HunterCompanionComposition.Resolve(ctx,pet,boss,physical,tuning).IsCritical);
    }

    [Fact]
    public void ExpertiseRemovesPhysicalPetDodgeAndParryButNeverSpellMiss()
    {
        var (ctx,pet,boss,tuning)=Setup();
        var hunter=ctx.GetActor("hunter")!;
        tuning.PetAvoidancePercent=100m;
        var melee=new AbilityEffectDefinition{
            ResolutionType=CombatResolutionTypes.Melee,CanMiss=true,
            CanBeDodged=true,CanBeParried=true
        };
        var spell=new AbilityEffectDefinition{
            ResolutionType=CombatResolutionTypes.Spell,CanMiss=true
        };
        Assert.False(HunterCompanionComposition.Resolve(ctx,pet,boss,melee,tuning).Landed);
        hunter.Stats.Set(ForeverCombatStatKeys.DodgeReductionPercent,100m);
        hunter.Stats.Set(ForeverCombatStatKeys.ParryReductionPercent,100m);
        Assert.True(HunterCompanionComposition.Resolve(ctx,pet,boss,melee,tuning).Landed);
        tuning.PetHitPercent=0m;
        Assert.False(HunterCompanionComposition.Resolve(ctx,pet,boss,spell,tuning).Landed);
    }

    [Theory]
    [InlineData("marksmanship")]
    [InlineData("survival")]
    public void OwnerAndPetCanAttackAtIdenticalTimestampWithoutLosingEitherDamage(
        string specialization)
    {
        var ownerAndPet=specialization=="marksmanship"?
            new MarksmanshipHunterSimRunner().Run(new MarksmanshipHunterSimRequest {
                HasPet=true,PetFamily="cat",
                PetEngagementDelaySeconds=.5m,DurationSeconds=24m,
                CaptureTimeline=true,PetTuning=new ForeverBeastMasteryHunterTuning {
                    PetSwingSeconds=2.8m,PetHitPercent=100m,PetAvoidancePercent=0m
                }
            }).Run:
            new SurvivalHunterSimRunner().Run(new SurvivalHunterSimRequest {
                HasPet=true,PetFamily="cat",CombatMode="melee",
                PetEngagementDelaySeconds=0m,DurationSeconds=24m,
                AdditionalTargets=0,UseFireTraps=false,UseFrostTraps=false,
                ScriptedAvoidanceEverySeconds=null,CaptureTimeline=true,
                PetTuning=new ForeverBeastMasteryHunterTuning {
                    PetSwingSeconds=2.7m,PetHitPercent=100m,PetAvoidancePercent=0m
                }
            }).Run;
        var autos=ownerAndPet.Timeline.Where(e=>
            e.Type==CombatEventType.Damage && e.Amount>0m &&
            e.AbilityKey is ("auto-shot" or "melee-auto" or "pet-melee")).ToArray();
        Assert.Contains(autos,e=>e.SourceActorKey=="hunter");
        Assert.Contains(autos,e=>e.SourceActorKey=="hunter-pet");
        // Both streams are processed by one event engine; same-time events
        // must retain their own actor and ability execution ownership.
        Assert.Contains(autos,x=>x.SourceActorKey=="hunter" &&
            autos.Any(y=>y.SourceActorKey=="hunter-pet" &&
                y.TimeSeconds==x.TimeSeconds));
    }
}
