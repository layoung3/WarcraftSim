using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp163PetFamilyCorrectionsTests
{
    [Fact]
    public void ThunderstompRespectsSixtySecondCooldownAndFocusCost()
    {
        var spell = ForeverBeastMasteryHunterSpellbook.Pet(
            ForeverHunterPetFamilies.Get("gorilla"),new ForeverBeastMasteryHunterTuning())
            .Single(x => x.Key=="pet-thunderstomp");
        Assert.Equal(60m,spell.CooldownSeconds);
        Assert.Equal(60m,spell.ResourceCosts.Single(x => x.ResourceKey=="focus").Amount);
        Assert.Equal(9,spell.Effects.Single().MaxTargets);

        var simulation = new BeastMasteryHunterSimRunner().Run(new BeastMasteryHunterSimRequest{
            PetFamily="gorilla",AdditionalTargets=2,DurationSeconds=120m,
            CaptureTimeline=true,Seed=163001
        });
        var summary=simulation.Run.Summary.ActorSummaries["hunter-pet"];
        Assert.InRange(summary.Abilities["pet-thunderstomp"].CastCompletedCount,1,2);
        Assert.True(summary.Abilities["pet-thunderstomp"].DamageOccurrenceCount>=3);
        Assert.True(simulation.PetDamage>0m);
    }

    [Theory]
    [InlineData("raptor","pet-savage-rend")]
    [InlineData("hyena","pet-tendon-rip")]
    [InlineData("scorpid","pet-scorpid-poison")]
    [InlineData("spider","pet-web")]
    public void FamilyDotActuallyTicksAndCreditsPet(string family,string ability)
    {
        var result=new BeastMasteryHunterSimRunner().Run(new BeastMasteryHunterSimRequest {
            PetFamily=family,DurationSeconds=120m,Seed=163001,CaptureTimeline=true
        });
        var pet=result.Run.Summary.ActorSummaries["hunter-pet"];
        var special=pet.Abilities[ability];
        Assert.True(special.CastCompletedCount>0,ability+" never cast");
        Assert.True(special.PeriodicOccurrenceCount>0,ability+" never ticked");
        Assert.True(special.DamageDone>0m,ability+" has no damage");
        Assert.True(pet.DamageDoneByAbility[ability]>0m);
        Assert.Contains(result.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey==ability && e.IsPeriodic && e.Amount>0m);
    }

    [Fact]
    public void FocusRegenerationAppearsAsPositivePetResourceEvents()
    {
        var result=new BeastMasteryHunterSimRunner().Run(new BeastMasteryHunterSimRequest {
            PetFamily="wind-serpent",DurationSeconds=45m,Seed=163001,CaptureTimeline=true
        });
        Assert.Contains(result.Run.Timeline,e=>e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey=="pet-focus-regeneration" && e.Amount>0m);
    }

    [Fact]
    public void FamilySweepFailsChecksWhenPeriodicDamageIsMissing()
    {
        var report=new BeastMasteryPetFamilySweep().Run(new BeastMasteryHunterSimRequest{
            DurationSeconds=30m,Seed=163001
        });
        foreach(var test in report.Cases.Where(x=>x.Family is "raptor" or "hyena" or "scorpid" or "spider"))
            Assert.True(test.MechanicsChecks["periodicFamilyDamage"],test.Family);
    }
}
