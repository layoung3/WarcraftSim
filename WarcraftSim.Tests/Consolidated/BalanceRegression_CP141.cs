using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Priest;
using Xunit;

namespace WarcraftSim.Tests;

/// <summary>
/// Targeted regression coverage for the October 2026 Forever mechanics review.
/// Tests assert mechanics/invariants, not unverified absolute DPS predictions.
/// </summary>
public sealed class BalanceRegressionCp141Tests
{
    private static WarlockSimRequest Destruction()
    {
        var request = WarlockSimRequest.CreatePreset("destruction");
        request.CaptureTimeline = true;
        request.DurationSeconds = 55m;
        request.BaseSpellHitPercent = 100m;
        request.SpellCritPercent = 0m;
        return request;
    }

    [Fact]
    public void ConflagrateConsumesOwnImmolateWithoutShadowAndFlame()
    {
        var request = Destruction();
        Assert.Equal(0, request.Talents.Rank("shadow-and-flame"));
        var result = new WarlockSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev => ev.Type == CombatEventType.Damage &&
            ev.AbilityKey == "conflagrate" && ev.ResultKey == "hit");
        Assert.Contains(result.Run.Timeline, ev => ev.Type == CombatEventType.AuraRemoved &&
            ev.AbilityKey == "immolate" && ev.Description is not null &&
            ev.Description.Contains("consumed by Conflagrate", StringComparison.Ordinal));
    }

    [Fact]
    public void ShadowAndFlameRankFiveRetainsImmolateOnEveryConflagrate()
    {
        var request = Destruction();
        request.Talents.Ranks["shadow-and-flame"] = 5;
        var result = new WarlockSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, ev => ev.Type == CombatEventType.Damage &&
            ev.AbilityKey == "conflagrate" && ev.ResultKey == "hit");
        Assert.DoesNotContain(result.Run.Timeline, ev => ev.Type == CombatEventType.AuraRemoved &&
            ev.Description is not null &&
            ev.Description.Contains("consumed by Conflagrate", StringComparison.Ordinal));
    }

    [Fact]
    public void SameSeedStillReproducesConflagrateProcOutcomes()
    {
        var request = Destruction();
        request.Talents.Ranks["shadow-and-flame"] = 3;
        var a = new WarlockSimRunner().Run(request);
        var b = new WarlockSimRunner().Run(request);
        Assert.Equal(a.DamageDone, b.DamageDone);
        Assert.Equal(a.EndingMana, b.EndingMana);
        Assert.Equal(CountConflagrateConsumes(a), CountConflagrateConsumes(b));
    }

    private static int CountConflagrateConsumes(WarlockSimResponse result) =>
        result.Run.Timeline.Count(ev => ev.Type == CombatEventType.AuraRemoved &&
            ev.Description is not null &&
            ev.Description.Contains("consumed by Conflagrate", StringComparison.Ordinal));

    [Theory]
    [InlineData(0, 1, 1.0)]
    [InlineData(2, 1, 1.0)]
    [InlineData(2, 2, 1.2)]
    [InlineData(2, 3, 1.44)]
    [InlineData(1, 3, 1.21)]
    public void ImprovedMindFlayGrowsSuccessiveTicks(int talentRank, int tickNumber, double expected)
    {
        Assert.Equal((decimal)expected,
            ShadowPriestProgressiveMindFlayProcessor.MultiplierForTick(talentRank, tickNumber));
    }

    [Fact]
    public void MindFlayTickMultiplierRejectsUnsupportedRanksAndTickIndices()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ShadowPriestProgressiveMindFlayProcessor.MultiplierForTick(3, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ShadowPriestProgressiveMindFlayProcessor.MultiplierForTick(2, 0));
    }

    [Fact]
    public void ModeledMindFlayChannelDamageIncreasesPerTick()
    {
        var request = new ShadowPriestSimRequest
        {
            DurationSeconds = 50m, CaptureTimeline = true,
            BaseSpellHitPercent = 100m, SpellCritOverridePercent = 0m
        };
        // Disable the independent, stacking Shadow Weaving damage modifier so
        // the channel progression can be compared on its own.
        request.Talents.Ranks["shadow-weaving"] = 0;
        var response = new ShadowPriestSimRunner().Run(request);
        var firstCompleteChannel = response.Run.Timeline
            .Where(ev => ev.Type == CombatEventType.Damage &&
                ev.AbilityKey == "mind-flay" && ev.IsChannelTick && ev.Amount > 0m)
            .GroupBy(ev => ev.AbilityExecutionId)
            .First(group => group.Count() >= 3)
            .Take(3).ToArray();
        Assert.Equal(firstCompleteChannel[0].Amount!.Value * 1.20m,
            firstCompleteChannel[1].Amount!.Value);
        Assert.Equal(firstCompleteChannel[1].Amount!.Value * 1.20m,
            firstCompleteChannel[2].Amount!.Value);
    }

    [Fact]
    public void VampiricEmbraceUsesOctoberThirtySecondCooldown()
    {
        var talents = new ForeverPriestShadowTalentBuild();
        var spell = Assert.Single(ForeverPriestShadowSpellbook.Create(
            new ForeverPriestShadowTuning(), talents, 0m),
            ability => ability.Key == "vampiric-embrace");
        Assert.Equal(30m, spell.CooldownSeconds);
    }

    [Fact]
    public void DevouringPlagueAndShadowWordDeathUseOctoberCooldowns()
    {
        var spells = ForeverPriestShadowSpellbook.Create(
            new ForeverPriestShadowTuning(), new ForeverPriestShadowTalentBuild(), 0m);
        Assert.Equal(24m, Assert.Single(spells, x => x.Key == "devouring-plague").CooldownSeconds);
        Assert.Equal(18m, Assert.Single(spells, x => x.Key == "shadow-word-death").CooldownSeconds);
    }

    [Fact]
    public void OffHandAutoAttackGeneratesOnlyItsNormalizedRage()
    {
        // Dual Wield Specialization no longer modifies Rage: this is the
        // authoritative baseline before any class-specific modifiers.
        var swing = ForeverWarriorAutoAttackFactory.CreateDualWieldOffHand(
            "offhand", "Off-hand", 45m, 60m, 2m, "off-hand-weapon-skill");
        var generation = Assert.Single(swing.ResourceGenerations);
        Assert.Equal(2m * ForeverWarriorAutoAttackFactory.OffHandRagePerSecond,
            generation.AmountPerLandedSwing);
        Assert.Equal(2m * ForeverWarriorAutoAttackFactory.OffHandCriticalBonusRagePerSecond,
            generation.CriticalBonusAmountPerLandedSwing);
    }
}
