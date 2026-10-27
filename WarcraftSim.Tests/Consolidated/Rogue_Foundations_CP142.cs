using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class RogueFoundationsCp142Tests
{
    [Theory]
    [InlineData("assassination")]
    [InlineData("combat")]
    [InlineData("subtlety")]
    public void ThreeSpecsUseTheRealEngineAndSpendEnergy(string spec)
    {
        var request = RogueSimRequest.CreatePreset(spec);
        request.CaptureTimeline = true;
        var result = new RogueSimRunner().Run(request);
        Assert.True(result.DamageDone > 0m);
        Assert.True(result.Dps > 0m);
        Assert.True(result.Run.ProcessedEventCount > 0);
        Assert.Contains("rogue", result.Run.Summary.ActorSummaries.Keys);
        // Decimal aggregation is not associative at the last few precision digits.
        // Preserve meaningful damage-accounting verification without false failures.
        Assert.InRange(
            Math.Abs(result.DamageDone - result.DamageByAbility.Values.Sum()),
            0m, 0.000001m);
        Assert.InRange(result.EndingEnergy, 0m, result.MaximumEnergy);
        Assert.InRange(result.EndingComboPoints, 0, 5);
        Assert.True(result.FinishersUsed > 0);
        Assert.Contains(result.Run.Timeline, x => x.Type == CombatEventType.ResourceChanged &&
            x.ResourceKey == "energy" && x.Amount is < 0m);
    }

    [Theory]
    [InlineData("assassination", "mutilate")]
    [InlineData("combat", "sinister-strike")]
    [InlineData("subtlety", "hemorrhage")]
    public void PresetsExecuteSpecializationBuilders(string spec, string builder)
    {
        var request = RogueSimRequest.CreatePreset(spec);
        request.CaptureTimeline = true;
        var result = new RogueSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, x => x.Type == CombatEventType.AbilityCastCompleted &&
            x.AbilityKey == builder);
    }

    [Fact]
    public void AssassinationPoolsEnergyForMutilateRatherThanUsingCheaperFallback()
    {
        var request = RogueSimRequest.CreatePreset("assassination");
        request.CaptureTimeline = true;
        var result = new RogueSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, x =>
            x.Type == CombatEventType.AbilityCastCompleted && x.AbilityKey == "mutilate");
        Assert.DoesNotContain(result.Run.Timeline, x =>
            x.Type == CombatEventType.AbilityCastCompleted && x.AbilityKey == "sinister-strike");
    }

    [Fact]
    public void AssassinationRetainsFallbackBuilderWhenMutilateCannotBeEquipped()
    {
        var request = RogueSimRequest.CreatePreset("assassination");
        request.BothWeaponsDaggers = false;
        request.CaptureTimeline = true;
        var result = new RogueSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, x =>
            x.Type == CombatEventType.AbilityCastCompleted && x.AbilityKey == "sinister-strike");
        Assert.DoesNotContain(result.Run.Timeline, x =>
            x.Type == CombatEventType.AbilityCastCompleted && x.AbilityKey == "mutilate");
    }

    [Fact]
    public void MainHandAndOffHandSwingsAreBothAttributed()
    {
        var request = RogueSimRequest.CreatePreset("combat");
        var result = new RogueSimRunner().Run(request);
        Assert.Contains("rogue-main-hand", result.DamageByAbility.Keys);
        Assert.Contains("rogue-off-hand", result.DamageByAbility.Keys);
    }

    [Fact]
    public void AssassinationMutilateUsesTwoWeaponsAndCannotBeUsedWithoutDaggers()
    {
        var request = RogueSimRequest.CreatePreset("assassination");
        request.CaptureTimeline = true;
        var result = new RogueSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, x => x.AbilityKey == "mutilate" &&
            x.EffectKey == "mutilate-main" && x.Type == CombatEventType.Damage);
        Assert.Contains(result.Run.Timeline, x => x.AbilityKey == "mutilate" &&
            x.EffectKey == "mutilate-off" && x.Type == CombatEventType.Damage);
        request.BothWeaponsDaggers = false;
        var without = new RogueSimRunner().Run(request);
        Assert.DoesNotContain("mutilate", without.Run.Summary.Abilities.Keys);
    }

    [Fact]
    public void RankAndMilestoneValidationRejectsUnsupportedPointAllocations()
    {
        var build = ForeverRogueTalentBuild.Default("assassination");
        build.Ranks["unimplemented-passive"] = 1;
        Assert.Throws<ArgumentException>(() => build.Validate(60, "assassination"));
        build.Ranks.Remove("unimplemented-passive");
        build.AssassinationPointsSpent = 20;
        Assert.Throws<ArgumentException>(() => build.Validate(60, "assassination"));
    }

    [Fact]
    public void VigorIncreasesMaximumEnergyByFivePerPoint()
    {
        var baseReq = RogueSimRequest.CreatePreset("assassination");
        baseReq.Talents.Ranks["vigor"] = 0;
        var basic = new RogueSimRunner().Run(baseReq);
        var vigorReq = RogueSimRequest.CreatePreset("assassination");
        var vigor = new RogueSimRunner().Run(vigorReq);
        Assert.Equal(100m, basic.MaximumEnergy);
        Assert.Equal(110m, vigor.MaximumEnergy);
    }

    [Fact]
    public void EnergyCanStartEmptyAndRegeneratesDuringTheFight()
    {
        var req = RogueSimRequest.CreatePreset("combat");
        req.StartingEnergyPercent = 0m;
        var result = new RogueSimRunner().Run(req);
        Assert.True(result.DamageDone > 0m);
        Assert.True(result.FinishersUsed > 0);
    }

    [Fact]
    public void InstantPoisonProcsAreDamageAttributedAndCanBeDisabled()
    {
        var req = RogueSimRequest.CreatePreset("assassination");
        req.Tuning.InstantPoisonChancePercent = 100m;
        var yes = new RogueSimRunner().Run(req);
        Assert.True(yes.PoisonProcs > 0);
        Assert.Contains("instant-poison", yes.DamageByAbility.Keys);
        req.UseInstantPoison = false;
        var no = new RogueSimRunner().Run(req);
        Assert.Equal(0, no.PoisonProcs);
        Assert.DoesNotContain("instant-poison", no.DamageByAbility.Keys);
    }

    [Fact]
    public void SeededRogueSimulationIsRepeatable()
    {
        var a = new RogueSimRunner().Run(RogueSimRequest.CreatePreset("subtlety"));
        var b = new RogueSimRunner().Run(RogueSimRequest.CreatePreset("subtlety"));
        Assert.Equal(a.DamageDone, b.DamageDone);
        Assert.Equal(a.EndingEnergy, b.EndingEnergy);
        Assert.Equal(a.EndingComboPoints, b.EndingComboPoints);
        Assert.Equal(a.PoisonProcs, b.PoisonProcs);
    }

    [Fact]
    public void NonPositiveWeaponSpeedFailsFast()
    {
        var request = RogueSimRequest.CreatePreset("combat");
        request.Tuning.OffHandSpeedSeconds = 0m;
        Assert.Throws<ArgumentException>(() => new RogueSimRunner().Run(request));
    }
}
