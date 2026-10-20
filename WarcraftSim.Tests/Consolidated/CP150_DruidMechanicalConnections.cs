using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class Cp150DruidMechanicalConnectionsTests
{
    [Fact]
    public void FerocityReducesEveryNamedCatAndBearAbilityCost()
    {
        var plain = ForeverFeralDruidSpellbook.Create(new(), new());
        var talents = new ForeverFeralDruidTalents();
        talents.Ranks["ferocity"] = 5;
        talents.Ranks["savage-fury"] = 1;
        talents.Ranks["primal-bite"] = 1;
        var improved = ForeverFeralDruidSpellbook.Create(talents, new());
        foreach (var key in new[] { "claw", "rake", "maul", "swipe" })
            Assert.Equal(5m,
                Assert.Single(plain, a => a.Key == key).ResourceCosts[0].Amount -
                Assert.Single(improved, a => a.Key == key).ResourceCosts[0].Amount);
        Assert.Equal(15m,
            Assert.Single(improved, a => a.Key == "primal-bite").ResourceCosts[0].Amount);
    }

    [Fact]
    public void BerserkRequiresLeaderOfThePackAndAnimalForm()
    {
        var talents = new ForeverFeralDruidTalents();
        talents.Ranks["berserk"] = 1;
        Assert.Throws<ArgumentException>(() => talents.Validate());
        talents.Ranks["leader-of-the-pack"] = 1;
        var berserk = Assert.Single(ForeverFeralDruidSpellbook.Create(talents, new()),
            x => x.Key == "berserk");
        Assert.Equal(180m, berserk.CooldownSeconds);
        Assert.Contains("druid-cat-form", berserk.RequiredAnySourceAuraKeys);
        Assert.Contains("druid-dire-bear-form", berserk.RequiredAnySourceAuraKeys);
    }

    [Fact]
    public void BerserkGrantsAnExecutableCombatWindow()
    {
        var request = new FeralDruidSimRequest {
            DurationSeconds = 24m, CaptureTimeline = true, UseFeralCooldowns = true,
            Role = "cat", Tuning = new ForeverFeralDruidTuning {
                MeleeHitPercent = 100m, MeleeCritPercent = 0m
            }
        };
        request.Talents.Ranks["leader-of-the-pack"] = 1;
        request.Talents.Ranks["berserk"] = 1;
        var result = new FeralDruidSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, e =>
            e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "berserk");
        Assert.Contains(result.Run.Timeline, e =>
            e.Type == CombatEventType.Damage && e.IsCritical &&
            (e.AbilityKey == "claw" || e.AbilityKey == "shred" || e.AbilityKey == "rake"));
    }

    [Fact]
    public void FrenziedRegenerationConsumesRageAndRecordsRecovery()
    {
        var request = new FeralDruidSimRequest {
            Role = "bear", DurationSeconds = 40m,
            UseFeralCooldowns = true, CaptureTimeline = true,
            Tuning = new ForeverFeralDruidTuning { MeleeHitPercent = 100m }
        };
        var result = new FeralDruidSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted &&
            e.AbilityKey == "frenzied-regeneration");
        Assert.True(result.FrenziedRegenerationHealing > 0m);
        Assert.Equal(result.FrenziedRegenerationHealing,
            result.Run.CustomMetrics["feral-frenzied-regeneration-healing"]);
    }

    [Fact]
    public void BerserkPrimalBiteHitsSecondaryTargets()
    {
        var request = new FeralDruidSimRequest {
            Role = "bear", DurationSeconds = 30m, AdditionalTargets = 2,
            UseFeralCooldowns = true, CaptureTimeline = true,
            Tuning = new ForeverFeralDruidTuning { MeleeHitPercent = 100m }
        };
        request.Talents.Ranks["savage-fury"] = 1;
        request.Talents.Ranks["primal-bite"] = 1;
        request.Talents.Ranks["leader-of-the-pack"] = 1;
        request.Talents.Ranks["berserk"] = 1;
        var result = new FeralDruidSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "primal-bite-secondary" && e.Amount > 0m);
    }

    [Fact]
    public void ImprovedRegrowthIsRankCappedAndRequiresExplicitTalentConfiguration()
    {
        var t = new ForeverRestorationDruidTalents();
        t.Ranks["improved-regrowth"] = 5;
        t.Validate();
        t.Ranks["improved-regrowth"] = 6;
        Assert.Throws<ArgumentException>(() => t.Validate());
    }
}
