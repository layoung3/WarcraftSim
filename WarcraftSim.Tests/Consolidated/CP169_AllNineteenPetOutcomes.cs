using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169AllNineteenPetOutcomes
{
    [Theory]
    [InlineData("bat","sonic-blast")]
    [InlineData("bear","swipe")]
    [InlineData("bird-of-prey","mine")]
    [InlineData("boar","charge")]
    [InlineData("carrion-bird","demoralizing-screech")]
    [InlineData("cat","prowl")]
    [InlineData("crab","pinch")]
    [InlineData("crocolisk","dismember")]
    [InlineData("fox","tricksters-dance")]
    [InlineData("gorilla","thunderstomp")]
    [InlineData("hyena","tendon-rip")]
    [InlineData("raptor","savage-rend")]
    [InlineData("scorpid","scorpid-poison")]
    [InlineData("spider","web")]
    [InlineData("tallstrider","dust-cloud")]
    [InlineData("turtle","shell-shield")]
    [InlineData("wind-serpent","lightning-breath")]
    [InlineData("wolf","furious-howl")]
    [InlineData("core-hound","lava-breath")]
    public void EveryCurrentFamilySpendsFocusExecutesSignatureAndReconcilesPetDamage(
        string family,string signature)
    {
        Assert.Equal(signature,ForeverHunterPetFamilies.Get(family).SpecialAbility);
        var req=new BeastMasteryHunterSimRequest {
            PetFamily=family,DurationSeconds=62m,
            PetEnabled=true,UsePetSpecial=true,
            TargetMaximumHealth=10000000m,
            CaptureTimeline=true,Seed=169001,
            UseRapidFire=false,UseMendPet=false,UseSummonHawk=false
        };
        var result=new BeastMasteryHunterSimRunner().Run(req);
        var special="pet-"+signature;
        var pet=result.Run.Summary.ActorSummaries["hunter-pet"];
        Assert.True(pet.DamageDone>0m, $"{family} generated no offensive damage");
        Assert.True(result.PetAutoDamage>0m, $"{family} has no actual melee auto");
        Assert.Contains(result.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey==special &&
            e.Type==CombatEventType.AbilityCastCompleted);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.SourceActorKey=="hunter-pet" && e.AbilityKey==special &&
            e.ResourceKey=="focus" && e.Amount<0m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged &&
            e.SourceActorKey=="hunter-pet" && e.TargetActorKey=="target" && e.Amount>0m);
        Assert.InRange(result.PetFocus,0m,100m);
        Assert.InRange(Math.Abs(result.PetDamage-result.PetAutoDamage-result.PetSpecialDamage),
            0m,.000001m);
        Assert.InRange(Math.Abs(result.DamageDone-
            result.HunterDamage-result.PetDamage-result.HawkDamage),0m,.000001m);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void AllNineteenFamiliesAreActuallyPresentInClientBackedRoster()
    {
        var keys=ForeverHunterPetFamilies.All.Select(x=>x.Key).ToArray();
        Assert.Equal(19,keys.Length);
        Assert.Equal(19,keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("bat")][InlineData("bear")][InlineData("bird-of-prey")]
    [InlineData("boar")][InlineData("carrion-bird")][InlineData("cat")]
    [InlineData("crab")][InlineData("crocolisk")][InlineData("fox")]
    [InlineData("gorilla")][InlineData("hyena")][InlineData("raptor")]
    [InlineData("scorpid")][InlineData("spider")][InlineData("tallstrider")]
    [InlineData("turtle")][InlineData("wind-serpent")][InlineData("wolf")]
    [InlineData("core-hound")]
    public void LethalPetDamageStopsSubsequentFamilyAttacks(string family)
    {
        var req=new BeastMasteryHunterSimRequest{
            PetFamily=family,DurationSeconds=18m,
            PetEnabled=true,UsePetSpecial=true,UseRevivePet=false,
            PetIncomingHitDamage=100000m,
            PetIncomingHitEverySeconds=3m,TargetMaximumHealth=10000000m,
            CaptureTimeline=true,Seed=169002,
            UseSummonHawk=false,UseRapidFire=false
        };
        var run=new BeastMasteryHunterSimRunner().Run(req);
        Assert.True(run.PetDied,$"{family} never died from lethal test hit");
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.TargetActorKey=="target" &&
            e.Amount>0m && e.TimeSeconds>5m);
    }
}
