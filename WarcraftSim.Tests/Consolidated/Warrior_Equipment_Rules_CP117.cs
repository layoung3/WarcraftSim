using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

/// <summary>CP117 class-neutral item catalog constraints and weapon/set examples.
/// Keep this in the feature-grouped suite (do not create one file per checkpoint test).</summary>
public sealed class WarriorEquipmentRulesCP117Tests
{
    private static WarriorGearEntry Gear(string slot, int id) => new() { Slot = slot, ItemId = id };

    private static WarriorSimRequest Request(string race = "orc", params WarriorGearEntry[] gear) => new()
    {
        Preset = "fury", Race = race, EquipmentMode = "catalog", Level = 60,
        Gear = [Gear("main-hand", 279259), .. gear], Iterations = 1,
        DurationSeconds = 8m, ExecutePhaseDurationSeconds = 0m,
        TargetLevel = 60, DeepWoundsRank = 0, ImpaleRank = 0, UseBloodrage = false
    };

    [Fact]
    public void Catalog_HasSampleItemsForEverySupportedGearSlot()
    {
        var items = ForeverEquipmentCatalog.GetCatalog().Items;
        foreach (var slot in new[] { "head", "neck", "shoulder", "back", "chest", "wrist",
            "hands", "waist", "legs", "feet", "finger1", "finger2", "trinket1",
            "trinket2", "ranged", "main-hand", "off-hand" })
            Assert.Contains(items, item => item.AllowedSlots.Contains(slot));
        Assert.True(items.Count >= 22);
    }

    [Fact]
    public void BlackhandsBreadth_ProvidesPassiveCriticalChanceFromEitherTrinketSlot()
    {
        foreach (var slot in new[] { "trinket1", "trinket2" })
        {
            var baseRequest = Request();
            var withItem = Request("orc", Gear(slot, 13965));
            var baseline = WarriorGearStatCalculator.Calculate(baseRequest).EffectiveStats;
            var effective = WarriorGearStatCalculator.Calculate(withItem).EffectiveStats;
            Assert.Equal(2m, effective.Get(ForeverCombatStatKeys.AttackCriticalChancePercent) -
                baseline.Get(ForeverCombatStatKeys.AttackCriticalChancePercent));
        }
    }

    [Theory]
    [InlineData("orc")]
    [InlineData("undead")]
    [InlineData("tauren")]
    [InlineData("troll")]
    public void RuneOfGuardCaptain_IsAvailableToHordeRaces(string race)
    {
        var request = Request(race, Gear("trinket1", 19120));
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(request);
        Assert.Contains(resolved.Gear, g => g.ItemId == 19120);
        Assert.Equal("horde", ForeverRaceFactions.GetFaction(race));
    }

    [Theory]
    [InlineData("human")]
    [InlineData("dwarf")]
    [InlineData("night-elf")]
    [InlineData("gnome")]
    [InlineData("skyborne")]
    public void RuneOfGuardCaptain_RejectsNonHordeOrUnverifiedRaces(string race)
    {
        var error = Assert.Throws<ArgumentException>(() => ForeverEquipmentCatalog.ResolveWarriorLoadout(
            Request(race, Gear("trinket1", 19120))));
        Assert.Contains("horde", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuneOfGuardCaptain_UsesOnlyUnconditionalForeverBonus_NoAssumedForestBonus()
    {
        var baseStats = WarriorGearStatCalculator.Calculate(Request()).EffectiveStats;
        var equipped = WarriorGearStatCalculator.Calculate(
            Request("orc", Gear("trinket2", 19120))).EffectiveStats;
        Assert.Equal(14m, equipped.Get(ForeverCombatStatKeys.AttackPower) -
            baseStats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.Equal(0.7m, equipped.Get(ForeverCombatStatKeys.HitChancePercent) -
            baseStats.Get(ForeverCombatStatKeys.HitChancePercent));
    }

    [Fact]
    public void BlackstoneRing_IsUniqueEquipped_ButTarnishedElvenRingsAreNot()
    {
        Assert.Throws<ArgumentException>(() => ForeverEquipmentCatalog.ResolveWarriorLoadout(
            Request("orc", Gear("finger1", 17713), Gear("finger2", 17713))));
        var pair = ForeverEquipmentCatalog.ResolveWarriorLoadout(
            Request("orc", Gear("finger1", 18500), Gear("finger2", 18500)));
        Assert.Equal(2, pair.Gear.Count(g => g.ItemId == 18500));
        var one = WarriorGearStatCalculator.Calculate(
            Request("orc", Gear("finger1", 18500))).EffectiveStats;
        var two = WarriorGearStatCalculator.Calculate(
            Request("orc", Gear("finger1", 18500), Gear("finger2", 18500))).EffectiveStats;
        Assert.Equal(1m, two.Get(ForeverCombatStatKeys.HitChancePercent) -
            one.Get(ForeverCombatStatKeys.HitChancePercent));
    }

    [Fact]
    public void DalRendWeapons_AreRestrictedToTheirActualHands()
    {
        Assert.Throws<ArgumentException>(() => ForeverEquipmentCatalog.ResolveWarriorLoadout(
            Request("orc", Gear("off-hand", 12940))));
        Assert.Throws<ArgumentException>(() => ForeverEquipmentCatalog.ResolveWarriorLoadout(new WarriorSimRequest
        {
            EquipmentMode = "catalog", Race = "orc", Gear = [Gear("main-hand", 12939)]
        }));
    }

    [Fact]
    public void DalRendPair_GrantsTwoPieceAttackPowerOnlyWhenBothWeaponsEquipped()
    {
        var mainOnly = new WarriorSimRequest
        {
            EquipmentMode = "catalog", Race = "orc", Gear = [Gear("main-hand", 12940)]
        };
        var paired = new WarriorSimRequest
        {
            EquipmentMode = "catalog", Race = "orc",
            Gear = [Gear("main-hand", 12940), Gear("off-hand", 12939)]
        };
        var a = WarriorGearStatCalculator.Calculate(mainOnly);
        var b = WarriorGearStatCalculator.Calculate(paired);
        Assert.Equal(50m, b.EffectiveStats.Get(ForeverCombatStatKeys.AttackPower) -
            a.EffectiveStats.Get(ForeverCombatStatKeys.AttackPower));
        Assert.DoesNotContain(a.StatContributions, x => x.SourceName == "Dal'Rend's Arms (2)");
        Assert.Contains(b.StatContributions, x => x.SourceName == "Dal'Rend's Arms (2)");
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(paired);
        Assert.True(resolved.DualWield);
        Assert.Equal(81m, resolved.MainHandMinimumDamage);
        Assert.Equal(151m, resolved.MainHandMaximumDamage);
        Assert.Equal(2.8m, resolved.MainHandSpeedSeconds);
        Assert.Equal(21m, resolved.OffHandMinimumDamage);
        Assert.Equal(63m, resolved.OffHandMaximumDamage);
        Assert.Equal(1.8m, resolved.OffHandSpeedSeconds);
    }

    [Fact]
    public void OffHandDalRend_CannotBeCombinedWithTwoHandedMainWeapon()
    {
        Assert.Throws<ArgumentException>(() => ForeverEquipmentCatalog.ResolveWarriorLoadout(
            new WarriorSimRequest
            {
                EquipmentMode = "catalog", Race = "orc",
                Gear = [Gear("main-hand", 279260), Gear("off-hand", 12939)]
            }));
    }

    [Fact]
    public void RangedBow_IsAStatStick_NotASecondMeleeAutoAttack()
    {
        var request = Request("orc", Gear("ranged", 18729));
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(request);
        Assert.Equal(2.4m, resolved.MainHandSpeedSeconds);
        Assert.False(resolved.DualWield);
        Assert.Equal("", resolved.OffHandWeaponType);
        Assert.Contains(resolved.Gear, g => g.ItemId == 18729);
        var baseStats = WarriorGearStatCalculator.Calculate(Request()).EffectiveStats;
        var withBow = WarriorGearStatCalculator.Calculate(request).EffectiveStats;
        Assert.Equal(3m, withBow.Get("stamina") - baseStats.Get("stamina"));
    }

    [Fact]
    public void Boots_ContributeCorrectStaticStats()
    {
        var a = WarriorGearStatCalculator.Calculate(Request()).EffectiveStats;
        var b = WarriorGearStatCalculator.Calculate(
            Request("orc", Gear("feet", 18521))).EffectiveStats;
        Assert.Equal(17m, b.Get(ForeverCombatStatKeys.Strength) - a.Get(ForeverCombatStatKeys.Strength));
        Assert.Equal(552m, b.Get(ForeverCombatStatKeys.Armor) - a.Get(ForeverCombatStatKeys.Armor));
    }

    [Fact]
    public void EntireSeventeenSlotLoadout_ResolvesWithoutLostSetOrTrinketStats()
    {
        var request = new WarriorSimRequest
        {
            EquipmentMode = "catalog", Race = "orc", Level = 60,
            Gear =
            [
                Gear("head", 12640), Gear("neck", 18404), Gear("shoulder", 13358),
                Gear("back", 13340), Gear("chest", 11726), Gear("wrist", 12936),
                Gear("hands", 15063), Gear("waist", 13142), Gear("legs", 15062),
                Gear("feet", 18521), Gear("finger1", 17713), Gear("finger2", 18500),
                Gear("trinket1", 13965), Gear("trinket2", 19120), Gear("ranged", 18729),
                Gear("main-hand", 12940), Gear("off-hand", 12939)
            ]
        };
        var resolved = ForeverEquipmentCatalog.ResolveWarriorLoadout(request);
        Assert.Equal(17, resolved.Gear.Count);
        Assert.True(resolved.DualWield);
        var result = WarriorGearStatCalculator.Calculate(request);
        Assert.Contains(result.StatContributions, x => x.SourceName == "Devilsaur Armor (2)");
        Assert.Contains(result.StatContributions, x => x.SourceName == "Dal'Rend's Arms (2)");
        Assert.Contains(result.StatContributions, x => x.SourceName == "Blackhand's Breadth");
        Assert.Contains(result.StatContributions, x => x.SourceName == "Rune of the Guard Captain");
    }

    [Fact]
    public void FactionLockedAndUniqueTrinket_CannotAppearTwice()
    {
        Assert.Throws<ArgumentException>(() => ForeverEquipmentCatalog.ResolveWarriorLoadout(
            Request("orc", Gear("trinket1", 19120), Gear("trinket2", 19120))));
    }
}
