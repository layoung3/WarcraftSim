using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class ForeverEquipmentCatalogTests
{
    private static WarriorSimRequest Request(params WarriorGearEntry[] items) => new()
    {
        Preset = "fury",
        EquipmentMode = "catalog",
        Gear = [.. items],
        Level = 60,
        Iterations = 1,
        DurationSeconds = 8m,
        ExecutePhaseDurationSeconds = 0m,
        TargetLevel = 60,
        DeepWoundsRank = 0,
        ImpaleRank = 0,
        UseBloodrage = false
    };

    private static WarriorGearEntry Gear(string slot, int id, int? enchant = null) =>
        new() { Slot = slot, ItemId = id, EnchantSpellId = enchant };

    [Fact]
    public void Catalog_HasVersionedServerOwnedItemsAndProvenance()
    {
        var document = ForeverEquipmentCatalog.GetCatalog();
        Assert.NotEmpty(document.CatalogVersion);
        Assert.True(document.Items.Count >= 10);
        Assert.All(document.Items, item => Assert.StartsWith("https://www.wowhead.com/forever/", item.SourceUrl));
        Assert.Equal("Lionheart Helm", ForeverEquipmentCatalog.FindItem(12640).Name);
    }

    [Fact]
    public void CatalogWeapon_ReplacesManualDamageAndSpeed_WithoutMutatingIncomingSetup()
    {
        var request = Request(Gear("main-hand", 279259));
        request.MainHandMinimumDamage = 900m;
        request.MainHandMaximumDamage = 1000m;
        request.MainHandSpeedSeconds = 5m;
        request.IsTwoHanded = true;
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(request);
        Assert.Equal(79m, resolved.MainHandMinimumDamage);
        Assert.Equal(148m, resolved.MainHandMaximumDamage);
        Assert.Equal(2.4m, resolved.MainHandSpeedSeconds);
        Assert.False(resolved.IsTwoHanded);
        Assert.False(resolved.DualWield);
        Assert.Equal(900m, request.MainHandMinimumDamage);
        Assert.Equal("catalog", request.EquipmentMode);
        Assert.Equal("gear-bonuses", resolved.EquipmentMode);
    }

    [Fact]
    public void TwoHandedWeapon_TakesRealDamageAndCannotEquipAnOffHand()
    {
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(Request(Gear("main-hand", 279260)));
        Assert.True(resolved.IsTwoHanded);
        Assert.Equal(162m, resolved.MainHandMinimumDamage);
        Assert.Equal(244m, resolved.MainHandMaximumDamage);
        Assert.Equal(3.3m, resolved.MainHandSpeedSeconds);
        Assert.Throws<ArgumentException>(() => ForeverEquipmentCatalog.ResolveWarriorLoadout(
            Request(Gear("main-hand", 279260), Gear("off-hand", 19168))));
    }

    [Fact]
    public void DifferentRealOneHanders_EnableDualWieldAndUseOwnWeaponDamage()
    {
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(
            Request(Gear("main-hand", 279259), Gear("off-hand", 19168)));
        Assert.True(resolved.DualWield);
        Assert.False(resolved.IsTwoHanded);
        Assert.Equal(65m, resolved.OffHandMinimumDamage);
        Assert.Equal(121m, resolved.OffHandMaximumDamage);
        Assert.Equal(1.8m, resolved.OffHandSpeedSeconds);
    }

    [Fact]
    public void SuperiorStriking_AddsFiveToDamageRangeWithoutAddingAttackPower()
    {
        var request = Request(Gear("main-hand", 279259, 20031));
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(request);
        Assert.Equal(84m, resolved.MainHandMinimumDamage);
        Assert.Equal(153m, resolved.MainHandMaximumDamage);
        Assert.Equal(2.4m, resolved.MainHandSpeedSeconds);
        Assert.Equal(request.AttackPower + 16m,
            WarriorGearStatCalculator.Calculate(resolved).EffectiveStats.Get(ForeverCombatStatKeys.AttackPower));
    }

    [Fact]
    public void LionheartAndSuperiorAgilityEnchant_FlowThroughSharedStatPipeline()
    {
        var request = Request(Gear("main-hand", 279259), Gear("head", 12640), Gear("hands", 15063, 25080));
        var result = new ForeverWarriorSimRunner().Run(request);
        Assert.Equal(1200m + 16m + 36m + 28m, result.EffectiveCharacterStats[ForeverCombatStatKeys.AttackPower]);
        Assert.Equal(4m + 1m + 2m, result.EffectiveCharacterStats[ForeverCombatStatKeys.HitChancePercent]);
        Assert.Contains(result.StatContributions, x => x.SourceName == "Enchant Gloves - Superior Agility");
        Assert.Contains(result.StatContributions, x => x.SourceName == "Lionheart Helm");
    }

    [Fact]
    public void DevilsaurTwoPieceBonus_AppliesExactlyOnceAndDoesNotApplyWithOnePiece()
    {
        var alone = Request(Gear("main-hand", 19168), Gear("legs", 15062));
        var together = Request(Gear("main-hand", 19168), Gear("legs", 15062), Gear("hands", 15063));
        var a = WarriorGearStatCalculator.Calculate(alone);
        var b = WarriorGearStatCalculator.Calculate(together);
        Assert.Equal(2m, b.EffectiveStats.Get(ForeverCombatStatKeys.HitChancePercent) -
            a.EffectiveStats.Get(ForeverCombatStatKeys.HitChancePercent));
        Assert.Contains(b.StatContributions, x => x.SourceName == "Devilsaur Armor (2)");
        Assert.DoesNotContain(a.StatContributions, x => x.SourceName == "Devilsaur Armor (2)");
    }

    [Fact]
    public void Catalog_RejectsForgedClientStatsAndUnknownGameIds()
    {
        var forged = Request(Gear("main-hand", 279259));
        forged.Gear[0].Stats["strength"] = 9000m;
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(forged));
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 999999999))));
    }

    [Fact]
    public void Catalog_RejectsWrongSlotsAndDuplicateSelections()
    {
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 279259), Gear("head", 279260))));
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 279259), Gear("main-hand", 19168))));
    }

    [Fact]
    public void Catalog_RejectsRepeatedUniqueEquippedItem()
    {
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 279259), Gear("off-hand", 279259))));
    }

    [Fact]
    public void Catalog_RejectsEnchantForWrongSlotAndUnmodeledProc()
    {
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 279259, 25080))));
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("main-hand", 279259, 20034))));
    }

    [Fact]
    public void Catalog_RequiresRealMainHandWeapon()
    {
        Assert.Throws<ArgumentException>(() => ForeverWarriorSimRunner.Validate(
            Request(Gear("head", 12640))));
    }
}
