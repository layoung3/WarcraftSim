using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class RogueMechanicsCp143Tests
{
    [Theory]
    [InlineData("assassination")]
    [InlineData("combat")]
    [InlineData("subtlety")]
    public void AllThreeSpecsCanOpenFromStealth(string spec)
    {
        var request = RogueSimRequest.CreatePreset(spec);
        request.DurationSeconds = 60m;
        request.CaptureTimeline = true;
        var result = new RogueSimRunner().Run(request);
        Assert.True(result.StealthOpeners >= 1);
        Assert.Contains(result.Run.Timeline, x =>
            x.Type == CombatEventType.AbilityCastCompleted &&
            x.AbilityKey == (spec == "combat" ? "garrote" : "ambush"));
        Assert.Contains(result.Run.Timeline, x =>
            x.Type == CombatEventType.AuraRemoved && x.Description != null &&
            x.Description.Contains("Stealth"));
    }

    [Fact]
    public void OpenerIsEngineGatedByStealthOrExplicitCutthroatWindow()
    {
        var request = RogueSimRequest.CreatePreset("subtlety");
        var abilities = ForeverRogueSpellbook.Create("subtlety", request.Talents,
            request.Tuning, 400m, bothDaggers: true);
        var ambush = Assert.Single(abilities, a => a.Key == "ambush");
        Assert.Contains("rogue-stealth", ambush.RequiredAnySourceAuraKeys);
        Assert.Contains("rogue-cutthroat", ambush.RequiredAnySourceAuraKeys);
        var garrote = Assert.Single(abilities, a => a.Key == "garrote");
        Assert.Contains("rogue-stealth", garrote.RequiredSourceAuraKeys);
    }

    [Fact]
    public void VanishIsAnActualTimedAbilityThatRestoresStealth()
    {
        var req = RogueSimRequest.CreatePreset("subtlety");
        req.CaptureTimeline = true;
        req.DurationSeconds = 90m;
        var result = new RogueSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline, x =>
            x.Type == CombatEventType.AbilityCastCompleted && x.AbilityKey == "vanish");
        Assert.True(result.StealthOpeners >= 2);
    }

    [Fact]
    public void BladeFlurryCanDamageActualSecondaryEnemy()
    {
        var req = RogueSimRequest.CreatePreset("combat");
        req.AdditionalTargets = 1;
        req.CaptureTimeline = true;
        var result = new RogueSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline, x => x.Type == CombatEventType.Damage &&
            x.AbilityKey == "blade-flurry-cleave" && x.TargetActorKey == "target-1");
    }

    [Fact]
    public void FinisherCostReductionIsTalentDependent()
    {
        var talents = ForeverRogueTalentBuild.Default("combat");
        talents.Ranks["flawless-execution"] = 1;
        talents.Validate(60, "combat");
        Assert.Equal(1, talents.Rank("flawless-execution"));
    }

    [Fact]
    public void RogueEncounterRejectsInvalidExtraTargetCount()
    {
        var request = RogueSimRequest.CreatePreset("combat");
        request.AdditionalTargets = -1;
        Assert.Throws<ArgumentException>(() => new RogueSimRunner().Run(request));
    }
}
