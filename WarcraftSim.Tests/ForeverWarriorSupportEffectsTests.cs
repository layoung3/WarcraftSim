using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Combat;
using Microsoft.AspNetCore.Mvc;

namespace WarcraftSim.Tests;

public sealed class ForeverWarriorSupportEffectsTests
{
    private static WarriorSimRequest Request() => new()
    {
        Preset = "arms", Level = 60, Iterations = 1, DurationSeconds = 15m,
        TargetLevel = 60, ExecutePhaseDurationSeconds = 0m,
        TargetDodgePercent = 0m, UseBloodrage = false,
        DeepWoundsRank = 0, ImpaleRank = 0
    };

    [Fact]
    public void SupportCatalog_IsVersionedWithCategorizedEffectsAndProvenance()
    {
        var catalog = ForeverSupportEffectCatalog.GetCatalog();
        Assert.NotEmpty(catalog.CatalogVersion);
        Assert.Contains(catalog.Effects, x => x.Category == "raid-buff");
        Assert.Contains(catalog.Effects, x => x.Category == "consumable");
        Assert.Contains(catalog.Effects, x => x.Category == "boss-debuff");
        Assert.All(catalog.Effects, x => Assert.StartsWith("https://", x.SourceUrl));
        Assert.All(catalog.Effects, x => Assert.NotEmpty(x.SourceStatus));
    }

    [Fact]
    public void ZeroSelections_KeepStatsAndBossArmorUnchanged()
    {
        var request = Request();
        request.TargetArmor = 3500m;
        var output = new ForeverWarriorSimRunner().Run(request);
        Assert.Empty(output.ActiveSupportEffects);
        Assert.Equal(3500m, output.EffectiveBossArmor);
        Assert.Equal(request.AttackPower, output.EffectiveCharacterStats[ForeverCombatStatKeys.AttackPower]);
    }

    [Fact]
    public void BattleShoutAndMight_StackAsAdditiveBuffSources()
    {
        var request = Request();
        request.RaidBuffIds = ["battle-shout", "blessing-of-might"];
        var calculated = WarriorGearStatCalculator.Calculate(request);
        Assert.Equal(1200m + 139m + 133m,
            calculated.EffectiveStats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.Contains(calculated.StatContributions, x => x.SourceName == "Battle Shout (rank 7)");
        Assert.Contains(calculated.StatContributions, x => x.SourceName == "Blessing of Might (highest rank)");
    }

    [Fact]
    public void AgilityAndStrengthBuffs_PassThroughDerivedConversions()
    {
        var request = Request();
        request.RaidBuffIds = ["strength-of-earth", "grace-of-air", "leader-of-the-pack"];
        var calculated = WarriorGearStatCalculator.Calculate(request);
        Assert.Equal(1200m + 106m,
            calculated.EffectiveStats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.Equal(20m + 3m + 89m / 20m,
            calculated.EffectiveStats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
    }

    [Fact]
    public void ConsumableBonuses_AffectCalculatedStats()
    {
        var request = Request();
        request.ConsumableIds = ["elixir-of-giants", "elixir-of-the-mongoose"];
        var calculated = WarriorGearStatCalculator.Calculate(request);
        Assert.Equal(1250m, calculated.EffectiveStats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.Equal(20m + 2m + 25m / 20m,
            calculated.EffectiveStats.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
    }

    [Fact]
    public void MajorAndMinorArmorDebuffs_StackAndClampAtZero()
    {
        var request = Request();
        request.TargetArmor = 3500m;
        request.BossDebuffIds = ["sunder-armor", "faerie-fire", "curse-of-recklessness"];
        request.SunderArmorStacks = 5;
        var output = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(3500m - 2250m - 505m - 505m, output.EffectiveBossArmor);
        request.TargetArmor = 100m;
        Assert.Equal(0m, new ForeverWarriorSimRunner().Run(request).EffectiveBossArmor);
    }

    [Theory]
    [InlineData(1, 450)]
    [InlineData(3, 1350)]
    [InlineData(5, 2250)]
    public void SunderArmorStacks_AreExplicitAndBounded(int stacks, int reduction)
    {
        var request = Request();
        request.TargetArmor = 4000m;
        request.BossDebuffIds = ["sunder-armor"];
        request.SunderArmorStacks = stacks;
        Assert.Equal(4000m - reduction, new ForeverWarriorSimRunner().Run(request).EffectiveBossArmor);
    }

    [Fact]
    public void ExposeArmor_ReplacesSunderInTheMajorArmorGroup()
    {
        var request = Request();
        request.BossDebuffIds = ["sunder-armor", "expose-armor"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void ConflictingConsumables_AreRejectedRatherThanDoubleCounted()
    {
        var request = Request();
        request.ConsumableIds = ["juju-might", "winterfall-firewater"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void UnsupportedActiveProcAndPercentMultiplier_AreRejected()
    {
        var request = Request();
        request.RaidBuffIds = ["windfury-totem"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.RaidBuffIds = ["blessing-of-kings"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void UnknownDuplicateAndWrongCategoryIds_FailValidation()
    {
        var request = Request();
        request.RaidBuffIds = ["nonexistent"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.RaidBuffIds = ["battle-shout", "battle-shout"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
        request.RaidBuffIds = ["elixir-of-giants"];
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(request));
    }

    [Fact]
    public void BuffedAndDebuffedSim_ProducesMoreDamageWithSameRngSeed()
    {
        var request = Request();
        request.TargetArmor = 4000m;
        var runner = new ForeverWarriorSimRunner();
        var baseline = runner.Run(request);
        request.RaidBuffIds = ["battle-shout", "blessing-of-might"];
        request.BossDebuffIds = ["sunder-armor"];
        var supported = runner.Run(request);
        Assert.True(supported.MeanDps > baseline.MeanDps);
        Assert.Equal(1750m, supported.EffectiveBossArmor);
    }

    [Fact]
    public void ApplyingSupportDoesNotMutateSavedRequest()
    {
        var request = Request();
        request.RaidBuffIds = ["battle-shout"];
        request.BossDebuffIds = ["sunder-armor"];
        request.TargetArmor = 4000m;
        _ = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(4000m, request.TargetArmor);
        Assert.Equal(1200m, request.AttackPower);
        Assert.Single(request.RaidBuffIds, x => x == "battle-shout");
    }

    [Fact]
    public void CatalogEquipmentAndSupportBuffs_CombineWithoutChangingItemData()
    {
        var request = Request();
        request.EquipmentMode = "catalog";
        request.Gear = [new WarriorGearEntry { Slot = "main-hand", ItemId = 279259 }];
        request.RaidBuffIds = ["battle-shout"];
        var result = new ForeverWarriorSimRunner().Run(request);
        // Ironwood Blade contributes 8 strength => 16 AP; shout adds another 139.
        Assert.Equal(1200m + 16m + 139m, result.EffectiveCharacterStats[ForeverCombatStatKeys.AttackPower]);
        Assert.Contains(result.ActiveSupportEffects, x => x.Contains("Battle Shout"));
        Assert.Equal("catalog", request.EquipmentMode);
        Assert.Equal(1200m, request.AttackPower);
    }

    [Fact]
    public void SupportCatalogEndpoint_ReturnsDataWithoutRunningSim()
    {
        var controller = new WarriorSimulationController();
        var response = controller.SupportCatalog();
        var result = Assert.IsType<OkObjectResult>(response.Result);
        var catalog = Assert.IsType<ForeverSupportCatalogDocument>(result.Value);
        Assert.NotEmpty(catalog.Effects);
    }
}
