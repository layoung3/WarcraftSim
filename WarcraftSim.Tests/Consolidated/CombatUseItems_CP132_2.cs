using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Items;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class CombatUseItemsCp1322Tests
{
    [Fact]
    public void ItemCatalogHasSeparatePotionGemAndRuneCooldowns()
    {
        var abilities = ForeverCombatItemCatalog.CreateAll().ToDictionary(x => x.Key);
        Assert.Equal("combat-potion", abilities[ForeverCombatItemCatalog.MightyRagePotion].SharedCooldownGroupKey);
        Assert.Equal("mage-mana-gem", abilities[ForeverCombatItemCatalog.ManaRuby].SharedCooldownGroupKey);
        Assert.Equal("mana-rune", abilities[ForeverCombatItemCatalog.DarkRune].SharedCooldownGroupKey);
        Assert.Equal(abilities[ForeverCombatItemCatalog.DarkRune].SharedCooldownGroupKey,
            abilities[ForeverCombatItemCatalog.DemonicRune].SharedCooldownGroupKey);
        Assert.Equal(120m, abilities[ForeverCombatItemCatalog.ManaRuby].CooldownSeconds);
    }

    [Fact]
    public void MageCanConsumeOneManaRubyAndRegainMana()
    {
        var r = MageSimRequest.CreatePreset("frost");
        r.StartingManaPercent = 10m;
        r.UseEvocation = false;
        r.UseMajorManaPotion = false;
        r.DurationSeconds = 20m;
        r.CombatItems = new() { ManaRubyCharges = 1, ManaRubyBelowManaPercent = 80m };
        var result = new MageSimRunner().Run(r);
        Assert.Equal(1, result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.ManaRuby));
        Assert.Single(result.Run.Timeline, e => e.AbilityKey == ForeverCombatItemCatalog.ManaRuby &&
            e.Type == CombatEventType.ResourceChanged && e.Amount > 0m);
    }

    [Fact]
    public void NoGemInventoryMeansNoGemUsage()
    {
        var r = MageSimRequest.CreatePreset("arcane");
        r.StartingManaPercent = 10m;
        r.DurationSeconds = 20m;
        r.UseEvocation = false;
        r.UseMajorManaPotion = false;
        r.CombatItems = new();
        var result = new MageSimRunner().Run(r);
        Assert.Empty(result.CombatItemUses);
    }

    [Fact]
    public void DarkRuneRestoresManaButCostsRealHealth()
    {
        var r = MageSimRequest.CreatePreset("fire");
        r.StartingManaPercent = 10m;
        r.DurationSeconds = 15m;
        r.UseEvocation = false;
        r.UseMajorManaPotion = false;
        r.CombatItems = new() { DarkRuneCharges = 1, RuneBelowManaPercent = 80m };
        var result = new MageSimRunner().Run(r);
        Assert.Equal(1, result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.DarkRune));
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == ForeverCombatItemCatalog.DarkRune &&
            e.Type == CombatEventType.HealthChanged && e.Amount is <= -600m and >= -1000m);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == ForeverCombatItemCatalog.DarkRune &&
            e.Type == CombatEventType.ResourceChanged && e.Amount is >= 900m and <= 1500m);
    }

    [Fact]
    public void BothTypesOfRuneShareTheirTwoMinuteCooldown()
    {
        var r = MageSimRequest.CreatePreset("frost");
        r.StartingManaPercent = 10m;
        r.DurationSeconds = 70m;
        r.UseEvocation = false;
        r.UseMajorManaPotion = false;
        r.CombatItems = new() { DarkRuneCharges = 1, DemonicRuneCharges = 1, RuneBelowManaPercent = 95m };
        var result = new MageSimRunner().Run(r);
        Assert.Equal(1, result.CombatItemUses.Values.Sum());
        Assert.Equal(1, result.Run.Timeline.Count(e =>
            (e.AbilityKey is ForeverCombatItemCatalog.DarkRune or ForeverCombatItemCatalog.DemonicRune) &&
            e.Type == CombatEventType.AbilityCastCompleted));
    }

    [Fact]
    public void EngineeringDamageActuallyAppearsInDamageSummary()
    {
        var r = MageSimRequest.CreatePreset("fire");
        r.DurationSeconds = 12m;
        r.CombatItems = new() { EngineeringSkill = 250, DenseDynamiteCharges = 1 };
        var result = new MageSimRunner().Run(r);
        Assert.Equal(1, result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.DenseDynamite));
        Assert.InRange(result.DamageByAbility[ForeverCombatItemCatalog.DenseDynamite], 340m, 460m);
        Assert.InRange(Math.Abs(result.DamageDone - result.DamageByAbility.Values.Sum()), 0m, .000001m);
    }

    [Fact]
    public void SapperDamagesTargetAndCaster()
    {
        var r = MageSimRequest.CreatePreset("frost");
        r.DurationSeconds = 12m;
        r.CombatItems = new() { EngineeringSkill = 205, GoblinSapperCharges = 1 };
        var result = new MageSimRunner().Run(r);
        Assert.Equal(1, result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.GoblinSapperCharge));
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == ForeverCombatItemCatalog.GoblinSapperCharge &&
            e.Type == CombatEventType.HealthChanged && e.Amount < 0m);
        Assert.True(result.DamageByAbility.GetValueOrDefault(ForeverCombatItemCatalog.GoblinSapperCharge) >= 450m);
    }

    [Fact]
    public void WarriorCanUseMightyRagePotionAndGetsActualRage()
    {
        var r = new WarriorSimRequest
        {
            Preset = "fury", Level = 60, DurationSeconds = 12m,
            Iterations = 1, Seed = 132020, StartingRage = 0m,
            CaptureRepresentativeTimeline = true,
            CombatItems = new() { MightyRagePotionCharges = 1 }
        };
        var result = new ForeverWarriorSimRunner().Run(r);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline, e =>
            e.AbilityKey == ForeverCombatItemCatalog.MightyRagePotion &&
            e.Type == CombatEventType.ResourceChanged && e.Amount is >= 45m and <= 75m);
        Assert.Contains(result.Batch.RepresentativeRun.Timeline, e =>
            e.AbilityKey == ForeverCombatItemCatalog.MightyRagePotion &&
            e.Type == CombatEventType.AuraApplied);
    }

    [Fact]
    public void ProfessionAndClassRestrictionsAreValidated()
    {
        var mage = MageSimRequest.CreatePreset("fire");
        mage.CombatItems = new() { GoblinSapperCharges = 1, EngineeringSkill = 200 };
        Assert.Throws<ArgumentException>(() => new MageSimRunner().Run(mage));
        mage.CombatItems = new() { MightyRagePotionCharges = 1 };
        Assert.Throws<ArgumentException>(() => new MageSimRunner().Run(mage));
        var warlock = WarlockSimRequest.CreatePreset("affliction");
        warlock.CombatItems = new() { ManaRubyCharges = 1 };
        Assert.Throws<ArgumentException>(() => new WarlockSimRunner().Run(warlock));
    }

    [Fact]
    public void WarlockCanUseRuneWithoutReplacingLifeTap()
    {
        var r = WarlockSimRequest.CreatePreset("destruction");
        r.StartingManaPercent = 10m;
        r.DurationSeconds = 25m;
        r.CombatItems = new() { DarkRuneCharges = 1, RuneBelowManaPercent = 90m };
        var result = new WarlockSimRunner().Run(r);
        Assert.Equal(1, result.CombatItemUses.GetValueOrDefault(ForeverCombatItemCatalog.DarkRune));
        Assert.True(result.LifeTapCasts > 0);
    }
}
