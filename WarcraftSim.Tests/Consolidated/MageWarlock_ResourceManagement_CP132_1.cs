using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

/// <summary>
/// Resource validation with a real Evocation channel and a one-use potion.
/// The Warlock solo assumption intentionally differs from the real-HP mode.
/// </summary>
public sealed class MageWarlockResourceManagementCp1321Tests
{
    [Theory]
    [InlineData("arcane")]
    [InlineData("fire")]
    [InlineData("frost")]
    public void EveryMageTreeCanChannelEvocationAndRegainMana(string spec)
    {
        var request = MageSimRequest.CreatePreset(spec);
        request.StartingManaPercent = 10m;
        request.UseEvocation = true;
        request.EvocationBelowManaPercent = 75m;
        request.UseMajorManaPotion = false;
        request.DurationSeconds = 40m;
        var result = new MageSimRunner().Run(request);
        Assert.Equal(1, result.EvocationCasts);
        Assert.True(result.ManaRestoredByEvocation > 0m);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "evocation" &&
            ev.Type == CombatEventType.AbilityChannelStarted);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "evocation" &&
            ev.Type == CombatEventType.ResourceChanged && ev.Amount > 0m);
    }

    [Fact]
    public void EvocationCanBeDisabled()
    {
        var request = MageSimRequest.CreatePreset("arcane");
        request.StartingManaPercent = 10m;
        request.UseEvocation = false;
        request.UseMajorManaPotion = false;
        var result = new MageSimRunner().Run(request);
        Assert.Equal(0, result.EvocationCasts);
        Assert.Equal(0m, result.ManaRestoredByEvocation);
    }

    [Fact]
    public void MageMajorManaPotionIsRealResourceGainAndOneUse()
    {
        var request = MageSimRequest.CreatePreset("fire");
        request.StartingManaPercent = 10m;
        request.UseEvocation = false;
        request.UseMajorManaPotion = true;
        request.MajorManaPotionCharges = 1;
        request.MajorManaPotionBelowManaPercent = 75m;
        var result = new MageSimRunner().Run(request);
        Assert.Equal(1, result.MajorManaPotionUses);
        Assert.InRange(result.ManaRestoredByPotions, 1350m, 2250m);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "major-mana-potion" &&
            ev.Type == CombatEventType.ResourceChanged && ev.Amount > 0m);
    }

    [Fact]
    public void ZeroPotionInventoryPreventsUse()
    {
        var request = MageSimRequest.CreatePreset("frost");
        request.StartingManaPercent = 10m;
        request.UseEvocation = false;
        request.MajorManaPotionCharges = 0;
        var result = new MageSimRunner().Run(request);
        Assert.Equal(0, result.MajorManaPotionUses);
        Assert.Equal(0m, result.ManaRestoredByPotions);
    }

    [Fact]
    public void InvalidPotionConfigurationFailsValidation()
    {
        var request = MageSimRequest.CreatePreset("fire");
        request.MajorManaPotionCharges = -1;
        Assert.Throws<ArgumentException>(() => new MageSimRunner().Run(request));
    }

    [Theory]
    [InlineData("affliction")]
    [InlineData("demonology")]
    [InlineData("destruction")]
    public void SoloWarlockLifeTapIsNotArtificiallyHealthLimited(string spec)
    {
        var request = WarlockSimRequest.CreatePreset(spec);
        request.StartingManaPercent = 10m;
        request.AssumeExternalHealingForLifeTap = true;
        var result = new WarlockSimRunner().Run(request);
        Assert.True(result.LifeTapCasts > 0);
        Assert.Equal(request.WarlockMaximumHealth, result.EndingHealth);
        Assert.Contains(result.Run.Timeline, ev => ev.AbilityKey == "life-tap" &&
            ev.Type == CombatEventType.ResourceChanged && ev.Amount > 0m);
    }

    [Fact]
    public void WarlockRealHealthCostModeRemainsAvailable()
    {
        var request = WarlockSimRequest.CreatePreset("affliction");
        request.StartingManaPercent = 10m;
        request.AssumeExternalHealingForLifeTap = false;
        var result = new WarlockSimRunner().Run(request);
        Assert.True(result.LifeTapCasts > 0);
        Assert.True(result.EndingHealth < request.WarlockMaximumHealth);
    }
}
