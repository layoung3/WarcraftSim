using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters.Equipment;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175EquipmentPipelineIntegrity
{
    private static CharacterEquipmentLoadout Gear(params (string Slot, int Id, int? Enchant)[] entries) =>
        new() { Items = entries.Select(x => new CharacterEquipmentItem {
            SlotKey = x.Slot, ItemId = x.Id, EnchantSpellId = x.Enchant
        }).ToList() };

    private static WarriorSimRequest Prepare(CharacterEquipmentLoadout equipment,
        WarriorSimRequest? input = null)
    {
        var request = input ?? NewRequest();
        var result = Assert.IsType<OkObjectResult>(new ForeverWarriorBuilderController()
            .Prepare(new() { Warrior = request, Equipment = equipment }));
        return JsonSerializer.SerializeToElement(result.Value).GetProperty("warrior")
            .Deserialize<WarriorSimRequest>()!;
    }

    private static WarriorSimRequest NewRequest() => new() {
        Preset = "fury", TalentMode = "tree", Race = "orc", Level = 60,
        DurationSeconds = 12m, Iterations = 1, StartingRage = 100m,
        CaptureRepresentativeTimeline = true
    };

    [Fact]
    public void ArmorSetHitSurvivesSharedPreparationWithoutCatalogMainHand()
    {
        // Source: registered Devilsaur Armor two-piece bonus is +2% hit.
        var equipment = Gear(("hands", 15063, null), ("legs", 15062, null));
        var prepared = Prepare(equipment);
        var selected = ForeverSharedEquipmentResolver.Resolve(equipment, "warrior", 60, "orc");
        var baseline = Prepare(new());
        var before = WarriorGearStatCalculator.Calculate(baseline).EffectiveStats;
        var after = WarriorGearStatCalculator.Calculate(prepared).EffectiveStats;
        Assert.Equal(2m, selected.RawStats.Get("hit-chance-percent"));
        Assert.Equal(2m, after.Get("hit-chance-percent") - before.Get("hit-chance-percent"));
        Assert.Single(prepared.Gear, x => x.SetBonusStats.GetValueOrDefault("hit-chance-percent") == 2m);
        var combat = new ForeverWarriorSimRunner().Run(prepared);
        Assert.Equal(after.Get("hit-chance-percent"), combat.EffectiveCharacterStats["hit-chance-percent"]);
        Assert.Empty(equipment.Items[0].Modifiers);
        Assert.Empty(equipment.Items[1].Modifiers);
    }

    [Fact]
    public void RemovingAllEquipmentClearsPreviouslyPreparedStatsAndEffectIdentities()
    {
        var selected = Gear(("hands", 15063, null), ("legs", 15062, null),
            ("trinket1", 23041, null));
        var equipped = Prepare(selected);
        var removed = Prepare(new(), equipped);
        var empty = Prepare(new());
        Assert.Empty(removed.Gear);
        Assert.Equal(WarriorGearStatCalculator.Calculate(empty).EffectiveStats.Values,
            WarriorGearStatCalculator.Calculate(removed).EffectiveStats.Values);
        Assert.Equal(3, equipped.Gear.Count);
    }

    [Fact]
    public void SharedPreparationPreservesSelectedProcEnchantWithoutCatalogMainHand()
    {
        var equipment = Gear(("off-hand", 279259, 13898));
        var input = NewRequest();
        input.IsTwoHanded = false;
        var prepared = Prepare(equipment, input);
        var item = Assert.Single(prepared.Gear);
        Assert.Equal(279259, item.ItemId);
        Assert.Equal(13898, item.EnchantSpellId);
        Assert.Empty(item.EnchantStats); // Fiery Weapon is a proc, not invented passive damage.
        Assert.Equal(13898, equipment.Items[0].EnchantSpellId);
    }

    [Fact]
    public void CatalogOffHandUsesExactWeaponDataAlongsideExplicitManualMainHand()
    {
        var input = NewRequest();
        input.IsTwoHanded = false;
        input.MainHandMinimumDamage = 260m;
        input.MainHandMaximumDamage = 300m;
        input.MainHandSpeedSeconds = 3.1m;
        var prepared = Prepare(Gear(("off-hand", 279259, null)), input);
        Assert.True(prepared.DualWield);
        Assert.False(prepared.ShieldEquipped);
        Assert.Equal("sword", prepared.OffHandWeaponType);
        Assert.Equal(79m, prepared.OffHandMinimumDamage);
        Assert.Equal(148m, prepared.OffHandMaximumDamage);
        Assert.Equal(2.4m, prepared.OffHandSpeedSeconds);
        Assert.Equal(260m, prepared.MainHandMinimumDamage);
        Assert.Equal(300m, prepared.MainHandMaximumDamage);
        Assert.Equal(3.1m, prepared.MainHandSpeedSeconds);
        var combat = new ForeverWarriorSimRunner().Run(prepared);
        Assert.Contains(combat.Batch.RepresentativeRun.Timeline, e =>
            e.Type == WarcraftSim.Core.Simulation.Engine.CombatEventType.Damage &&
            e.AbilityKey == "off-hand-auto");
        Assert.False(input.DualWield);
    }

    [Fact]
    public void CatalogOffHandRejectsContradictoryManualTwoHandedMainHand()
    {
        var action = new ForeverWarriorBuilderController().Prepare(new() {
            Warrior = NewRequest(), Equipment = Gear(("off-hand", 279259, null))
        });
        var rejected = Assert.IsType<BadRequestObjectResult>(action);
        Assert.Contains("manual two-handed", JsonSerializer.Serialize(rejected.Value));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    public void SharedPreparationRejectsContradictoryManualHands(bool twoHanded,
        bool dualWield, bool shield)
    {
        var input = NewRequest();
        input.IsTwoHanded = twoHanded;
        input.DualWield = dualWield;
        input.ShieldEquipped = shield;
        var action = new ForeverWarriorBuilderController().Prepare(new() {
            Warrior = input, Equipment = new()
        });
        Assert.IsType<BadRequestObjectResult>(action);
    }

    [Theory]
    [InlineData("trinket1", 19339)] // Mage-only Mind Quickening Gem.
    [InlineData("chest", 22504)] // Warlock-only Plagueheart Robe.
    [InlineData("ranged", 11287)] // Caster-only wand.
    public void NativeWarriorCatalogRespectsSourceClassRestrictions(string slot, int itemId)
    {
        var request = new WarriorSimRequest {
            EquipmentMode = "catalog", Race = "orc", Level = 60,
            Gear = [new() { Slot = "main-hand", ItemId = 279259 },
                new() { Slot = slot, ItemId = itemId }]
        };
        var error = Assert.Throws<ArgumentException>(() =>
            ForeverEquipmentCatalog.ResolveWarriorLoadout(request));
        Assert.Contains("warrior", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NativeWarriorPermanentEnchantSlotRejectsTemporaryOil()
    {
        var request = new WarriorSimRequest {
            EquipmentMode = "catalog", Race = "orc", Level = 60,
            Gear = [new() { Slot = "main-hand", ItemId = 279259, EnchantSpellId = 25122 }]
        };
        Assert.Throws<ArgumentException>(() => ForeverEquipmentCatalog.ResolveWarriorLoadout(request));
    }
}
