using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169AllPetFamiliesAcrossSpecializations
{
    [Theory]
    [InlineData("bat")][InlineData("bear")][InlineData("bird-of-prey")]
    [InlineData("boar")][InlineData("carrion-bird")][InlineData("cat")]
    [InlineData("crab")][InlineData("crocolisk")][InlineData("fox")]
    [InlineData("gorilla")][InlineData("hyena")][InlineData("raptor")]
    [InlineData("scorpid")][InlineData("spider")][InlineData("tallstrider")]
    [InlineData("turtle")][InlineData("wind-serpent")][InlineData("wolf")]
    [InlineData("core-hound")]
    public void EveryPetFamilyContributesIndependentOwnerAndPetDamageInMmAndSurvival(string family)
    {
        var mm=new MarksmanshipHunterSimRequest {
            HasPet=true,PetFamily=family,DurationSeconds=38m,
            TargetMaximumHealth=10000000m,CaptureTimeline=true,
            UseRapidFire=false,UseRevivePet=false
        };
        mm.Tuning.HitChancePercent=100m;
        mm.PetTuning.PetHitPercent=100m;
        mm.PetTuning.PetAvoidancePercent=0m;
        var ranged=new MarksmanshipHunterSimRunner().Run(mm);
        var surv=new SurvivalHunterSimRequest {
            HasPet=true,PetFamily=family,DurationSeconds=38m,
            TargetMaximumHealth=10000000m,CaptureTimeline=true,
            CombatMode="melee",AdditionalTargets=0,UseRapidFire=false,
            UseFireTraps=false,UseFrostTraps=false,
            ScriptedAvoidanceEverySeconds=null
        };
        surv.Tuning.HitChancePercent=100m;
        surv.Tuning.TargetDodgePercent=0m;
        surv.Tuning.TargetParryPercent=0m;
        surv.PetTuning.PetHitPercent=100m;
        surv.PetTuning.PetAvoidancePercent=0m;
        var melee=new SurvivalHunterSimRunner().Run(surv);

        void Check(SimulationRunResult run,decimal ownerDamage,decimal petDamage,
            decimal total,decimal threat)
        {
            Assert.True(ownerDamage>0m,$"{family}: owner dealt no damage");
            Assert.True(petDamage>0m,$"{family}: companion dealt no damage");
            Assert.True(threat>0m,$"{family}: companion generated no threat");
            Assert.Equal(ownerDamage+petDamage,total);
            Assert.True(run.Summary.ActorSummaries.ContainsKey("hunter-pet"));
            Assert.Contains(run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
                e.Type==CombatEventType.Damage && e.Amount>0m);
            Assert.Contains(run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
                e.Type==CombatEventType.ResourceChanged && e.ResourceKey=="focus" &&
                e.Amount<0m);
        }
        Check(ranged.Run,ranged.HunterDamage,ranged.PetDamage,ranged.DamageDone,ranged.PetThreat);
        Check(melee.Run,melee.HunterDamage,melee.PetDamage,melee.DamageDone,melee.PetThreat);
        Assert.InRange(Math.Abs(ranged.DamageDone-ranged.DamageByAbility.Values.Sum()),0m,.000001m);
        Assert.InRange(Math.Abs(melee.DamageDone-melee.DamageByAbility.Values.Sum()),0m,.000001m);
        Assert.Contains(melee.Run.Timeline,e=>e.SourceActorKey=="hunter" &&
            e.AbilityKey is ("melee-auto" or "raptor-strike") &&
            e.Type==CombatEventType.Damage && e.Amount>0m);
    }
}
