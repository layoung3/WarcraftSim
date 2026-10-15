using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class WarlockDpsFoundationsCp131Tests
{
    [Theory]
    [InlineData("affliction")]
    [InlineData("demonology")]
    [InlineData("destruction")]
    public void EachWarlockSpecDealsDamageWithARealPetActor(string spec)
    {
        var result = new WarlockSimRunner().Run(WarlockSimRequest.CreatePreset(spec));
        Assert.True(result.Dps > 0m);
        Assert.True(result.PetDamage > 0m);
        Assert.Contains("warlock-pet", result.Run.Summary.ActorSummaries.Keys);
        Assert.Equal(result.DamageDone, result.Run.Summary.ActorSummaries["warlock"].DamageDone +
            result.Run.Summary.ActorSummaries["warlock-pet"].DamageDone);
    }

    [Fact]
    public void AfflictionChannelsWrackAndTicksCorruption()
    {
        var result = new WarlockSimRunner().Run(WarlockSimRequest.CreatePreset("affliction"));
        Assert.True(result.Run.Summary.Abilities["wrack"].ChannelStartedCount > 0);
        Assert.True(result.Run.Summary.Abilities["corruption"].PeriodicOccurrenceCount > 0);
    }

    [Fact]
    public void DemonologySoulFireAndPetDamageAreAttributedSeparately()
    {
        var result = new WarlockSimRunner().Run(WarlockSimRequest.CreatePreset("demonology"));
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "soul-fire" &&
            ev.Type == CombatEventType.Damage);
        Assert.Contains(result.Run.Timeline, ev => ev.SourceActorKey == "warlock-pet" &&
            ev.AbilityKey == "pet-melee" && ev.Type == CombatEventType.Damage);
    }

    [Fact]
    public void DestructionCastsIncinerateAndConflagrateWithImmolate()
    {
        var result = new WarlockSimRunner().Run(WarlockSimRequest.CreatePreset("destruction"));
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "incinerate" &&
            ev.Type == CombatEventType.Damage);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "conflagrate" &&
            ev.Type == CombatEventType.Damage);
    }

    [Fact]
    public void ConflagrateRequiresImmolate()
    {
        var request = WarlockSimRequest.CreatePreset("destruction");
        request.UseImmolate = false;
        var result = new WarlockSimRunner().Run(request);
        Assert.DoesNotContain(result.Run.Timeline, ev => ev.AbilityKey == "conflagrate" &&
            ev.Type == CombatEventType.AbilityCastCompleted);
    }

    [Fact]
    public void LifeTapTradesHealthForMana()
    {
        var request = WarlockSimRequest.CreatePreset("affliction");
        request.StartingManaPercent = 10m;
        request.AssumeExternalHealingForLifeTap = false; // Explicit legacy/no-healer mode.
        var result = new WarlockSimRunner().Run(request);
        Assert.True(result.LifeTapCasts > 0);
        Assert.True(result.EndingHealth < request.WarlockMaximumHealth);
        Assert.Contains(result.Run.Timeline, ev => ev.Type == CombatEventType.HealthChanged &&
            ev.AbilityKey == "life-tap");
    }

    [Fact]
    public void LifeTapCanBeDisabled()
    {
        var request = WarlockSimRequest.CreatePreset("affliction");
        request.StartingManaPercent = 10m;
        request.UseLifeTap = false;
        var result = new WarlockSimRunner().Run(request);
        Assert.Equal(0, result.LifeTapCasts);
    }

    [Fact]
    public void PetCanBeDisabledWithoutTurningOffCasterDamage()
    {
        var request = WarlockSimRequest.CreatePreset("demonology");
        request.EnablePet = false;
        var result = new WarlockSimRunner().Run(request);
        Assert.Equal(0m, result.PetDamage);
        Assert.True(result.DamageDone > 0m);
    }

    [Fact]
    public void DamageByAbilityReconcilesWithReportedCombinedDamage()
    {
        var result = new WarlockSimRunner().Run(WarlockSimRequest.CreatePreset("destruction"));
        Assert.InRange(Math.Abs(result.DamageByAbility.Values.Sum() - result.DamageDone), 0m, .001m);
    }

    [Fact]
    public void InvalidCapstonePrerequisitesAreRejected()
    {
        var request = WarlockSimRequest.CreatePreset("destruction");
        request.Talents.Ranks.Remove("bane-of-havoc");
        Assert.Throws<ArgumentException>(() => new WarlockSimRunner().Run(request));
    }

    [Fact]
    public void DamageAndManaAreDeterministicForEqualSeeds()
    {
        var one = new WarlockSimRunner().Run(WarlockSimRequest.CreatePreset("affliction"));
        var two = new WarlockSimRunner().Run(WarlockSimRequest.CreatePreset("affliction"));
        Assert.Equal(one.DamageDone, two.DamageDone);
        Assert.Equal(one.EndingMana, two.EndingMana);
    }

    [Fact]
    public void TuningRejectsNegativeDamage()
    {
        var request = WarlockSimRequest.CreatePreset("affliction");
        request.Tuning.CorruptionTick = -1m;
        Assert.Throws<ArgumentException>(() => new WarlockSimRunner().Run(request));
    }
}
