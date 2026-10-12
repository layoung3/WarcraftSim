using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>Character-stat preview for the Warrior alpha. This intentionally
/// reuses the class-neutral equipment contribution pipeline and Forever
/// primary/rating conversions. Catalog IDs are resolved separately from the
/// versioned Forever database and never trust client-supplied item bonuses.</summary>
public static class WarriorGearStatCalculator
{
    private static readonly HashSet<string> Slots = new(StringComparer.OrdinalIgnoreCase)
    {
        "head", "neck", "shoulder", "back", "chest", "wrist", "hands", "waist",
        "legs", "feet", "finger1", "finger2", "trinket1", "trinket2",
        "ranged", "main-hand", "off-hand"
    };

    private static readonly HashSet<string> StatKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ForeverCombatStatKeys.Strength, ForeverCombatStatKeys.Agility,
        ForeverCombatStatKeys.AttackPower, ForeverCombatStatKeys.HitRating,
        ForeverCombatStatKeys.CriticalStrikeRating,
        ForeverCombatStatKeys.HitChancePercent,
        ForeverCombatStatKeys.AttackCriticalChancePercent,
        ForeverCombatStatKeys.HastePercent,
        ForeverCombatStatKeys.Armor
    };

    public static void Validate(WarriorSimRequest request)
    {
        if (request.EquipmentMode == "catalog")
        {
            Validate(ForeverEquipmentCatalog.ResolveWarriorLoadout(request));
            return;
        }
        if (request.EquipmentMode is not ("manual" or "gear-bonuses"))
            throw new ArgumentException("EquipmentMode must be 'manual', 'gear-bonuses' or 'catalog'.");
        if (request.Gear is null || request.Gear.Count > Slots.Count)
            throw new ArgumentException("Gear must contain no more than 17 slots.");
        var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in request.Gear)
        {
            if (item is null || !Slots.Contains(item.Slot) ||
                !occupied.Add(item.Slot) || string.IsNullOrWhiteSpace(item.ItemKey))
                throw new ArgumentException("Gear requires a supported unique slot and an item key.");
            ValidateBonuses(item.Stats, "item");
            ValidateBonuses(item.EnchantStats, "enchant");
            ValidateBonuses(item.SetBonusStats, "set bonus");
            if (item.SetBonusStats.Count > 0 && string.IsNullOrWhiteSpace(item.SetBonusName))
                throw new ArgumentException("A set bonus with stats needs a set bonus name.");
            if (item.EnchantStats.Count > 0 && string.IsNullOrWhiteSpace(item.EnchantName))
                throw new ArgumentException("An enchant with bonuses requires an enchant name.");
        }
    }

    private static void ValidateBonuses(Dictionary<string, decimal>? bonuses, string label)
    {
        if (bonuses is null) throw new ArgumentException($"{label} stats cannot be null.");
        foreach (var (key, value) in bonuses)
            if (!StatKeys.Contains(key) || value is < -100000m or > 100000m)
                throw new ArgumentException($"Unsupported {label} stat or out-of-range amount: {key}.");
    }

    public static CharacterSimulationRuntimeCalculationResult Calculate(WarriorSimRequest request)
    {
        if (request.EquipmentMode == "catalog")
            return Calculate(ForeverEquipmentCatalog.ResolveWarriorLoadout(request));
        Validate(request);
        var profile = new CharacterProfile
        {
            Name = "Warrior DPS alpha",
            ClassKey = ForeverCharacterClassKeys.Warrior,
            RulesetKey = "wow-forever",
            Level = request.Level
        };
        profile.BaseStats.Set(ForeverCombatStatKeys.AttackPower, request.AttackPower);
        profile.BaseStats.Set(ForeverCombatStatKeys.AttackCriticalChancePercent, request.MeleeCritPercent);
        profile.BaseStats.Set(ForeverCombatStatKeys.HitChancePercent, request.MeleeHitPercent);
        profile.BaseStats.Set(ForeverCombatStatKeys.HastePercent, request.MeleeHastePercent);
        if (request.EquipmentMode == "gear-bonuses")
        {
            foreach (var input in request.Gear)
            {
                var item = new CharacterEquipmentItem
                {
                    SlotKey = input.Slot,
                    ItemKey = input.ItemKey,
                    Name = string.IsNullOrWhiteSpace(input.Name) ? input.ItemKey : input.Name,
                    Stats = ToStats(input.Stats)
                };
                if (input.EnchantStats.Count > 0)
                    item.Modifiers.Add(new CharacterEquipmentModifier
                    {
                        PlacementKey = "enchant",
                        ModifierKey = "manual-enchant",
                        ModifierType = CharacterEquipmentModifierTypes.Enchant,
                        Name = input.EnchantName ?? "Enchant",
                        Stats = ToStats(input.EnchantStats)
                    });
                if (input.SetBonusStats.Count > 0)
                    item.Modifiers.Add(new CharacterEquipmentModifier
                    {
                        PlacementKey = "set-bonus",
                        ModifierKey = "set-bonus:" + input.SetBonusName,
                        ModifierType = "set-bonus",
                        Name = input.SetBonusName!,
                        Stats = ToStats(input.SetBonusStats)
                    });
                profile.Equipment.Items.Add(item);
            }
        }

        return CharacterSimulationRuntimeCalculatorFactory.CreateStandard(
            _ => request.MaximumHealth,
            additionalStatContributors:
            [
                new ForeverPrimaryPhysicalStatContributor(),
                new ForeverPhysicalStatConversionContributor()
            ]).Calculate(profile);
    }

    private static StatCollection ToStats(Dictionary<string, decimal> bonuses)
    {
        var stats = new StatCollection();
        foreach (var (key, value) in bonuses)
            stats.Add(key, value);
        return stats;
    }
}
