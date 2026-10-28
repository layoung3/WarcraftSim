using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167ADirectSpells
{
    [Fact]
    public void ScorchHasVerifiedRankAndRetainsFireTalentCritPath()
    {
        var talents = ForeverMageTalentBuild.Default("fire");
        var book = ForeverMageDpsSpellbook.Create(talents, new ForeverMageTuning(), 200m);
        var scorch = Assert.Single(book, a => a.Key == "scorch");
        Assert.Equal(150m, scorch.ResourceCosts.Single().Amount);
        Assert.Equal(1.5m, scorch.CastTimeSeconds);
        Assert.Equal(181m + 200m * .429m, scorch.Effects.Single().MinimumValue);
        Assert.Equal("fire", scorch.Effects.Single().SchoolKey);
    }

    [Fact]
    public void ScorchOptInCastsDealsDamageAndReconcilesWithOwner()
    {
        var request = MageSimRequest.CreatePreset("fire");
        request.DurationSeconds = 18m;
        request.BaseSpellHitPercent = 100m;
        request.SpellCritPercent = 0m;
        request.CaptureTimeline = true;
        request.UseEvocation = false;
        request.UseMajorManaPotion = false;
        request.CombatItems.ManaRubyCharges = 0;
        request.CustomPriority = ["scorch"];
        var result = new MageSimRunner().Run(request);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "scorch" && e.Amount > 0m);
        Assert.True(result.DamageByAbility.GetValueOrDefault("scorch") > 0m);
        Assert.InRange(Math.Abs(result.DamageDone - result.DamageByAbility.Values.Sum()),
            0m, .000001m);
    }

    [Fact]
    public void ElementalEarthShockHasRankSevenAndSharedShockCooldown()
    {
        var talents = new ForeverElementalShamanTalents();
        var book = ForeverElementalShamanSpellbook.Create(talents,
            new ForeverElementalShamanTuning(), 0m);
        var earth = Assert.Single(book, a => a.Key == "earth-shock");
        var flame = Assert.Single(book, a => a.Key == "flame-shock");
        Assert.Equal(450m * (1m - .02m * talents.Rank("convection")),
            earth.ResourceCosts.Single().Amount);
        Assert.Equal(301m * (1m + .01m * talents.Rank("concussion")),
            earth.Effects.Single().MinimumValue);
        Assert.Equal(0m, earth.CastTimeSeconds);
        Assert.Equal("shaman-shocks", earth.SharedCooldownGroupKey);
        Assert.Equal(flame.SharedCooldownGroupKey, earth.SharedCooldownGroupKey);
        Assert.Equal(earth.CooldownSeconds, earth.SharedCooldownSeconds);
        Assert.Equal(flame.CooldownSeconds, flame.SharedCooldownSeconds);
    }

    [Fact]
    public void EarthShockOptInDealsDamageAndPreservesAccounting()
    {
        var req = ElementalPreRaidProfileCatalog.Setup("throughput", "raid-sustain");
        req.DurationSeconds = 15m;
        req.BaseSpellHitPercent = 100m;
        req.UseLightningOverload = false;
        req.CaptureTimeline = true;
        req.CustomPriority = ["earth-shock", "lightning-bolt"];
        var result = new ElementalShamanSimRunner().Run(req);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "earth-shock" && e.Amount > 0m);
        Assert.True(result.DamageByAbility.GetValueOrDefault("earth-shock") > 0m);
        Assert.InRange(Math.Abs(result.DamageDone - result.DamageByAbility.Values.Sum()),
            0m, .000001m);
    }
}
