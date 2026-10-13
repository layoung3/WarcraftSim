using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Versioned, class-neutral Forever item/enchantment definitions. Items are
/// resolved SERVER-SIDE by numeric game ID; clients cannot supply combat stats.
/// The curated starter catalog is embedded to keep test runs deterministic.
/// </summary>
public static class ForeverEquipmentCatalog
{
    private static readonly Lazy<ForeverEquipmentCatalogDocument> Catalog = new(Load);

    public static ForeverEquipmentCatalogDocument GetCatalog() => Catalog.Value;

    public static ForeverEquipmentItem FindItem(int id) =>
        Catalog.Value.Items.FirstOrDefault(x => x.Id == id) ??
        throw new ArgumentException($"Unknown Forever catalog item ID {id}. The curated catalog is not complete.");

    public static ForeverEquipmentEnchant FindEnchant(int spellId) =>
        Catalog.Value.Enchants.FirstOrDefault(x => x.SpellId == spellId) ??
        throw new ArgumentException($"Unknown Forever enchant spell ID {spellId}.");

    /// <summary>Resolves the curated item IDs into the already tested shared
    /// equipment-stat pipeline and sets actual weapon speed/damage. The original
    /// incoming request is never mutated, so imported setups remain repeatable.</summary>
    public static WarriorSimRequest ResolveWarriorLoadout(WarriorSimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EquipmentMode != "catalog")
            throw new ArgumentException("Catalog resolution requires EquipmentMode='catalog'.");
        if (request.Gear is null || request.Gear.Count > 17)
            throw new ArgumentException("A catalog loadout supports at most 17 equipment slots.");

        var resolved = request.ShallowCopy();
        resolved.EquipmentMode = "gear-bonuses";
        resolved.Gear = [];
        var slots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueIds = new HashSet<int>();
        var equippedSets = new Dictionary<string, List<WarriorGearEntry>>(StringComparer.OrdinalIgnoreCase);
        ForeverEquipmentItem? main = null;
        ForeverEquipmentItem? off = null;
        decimal mainDamageEnchant = 0m, offDamageEnchant = 0m;

        foreach (var selection in request.Gear)
        {
            if (selection is null || string.IsNullOrWhiteSpace(selection.Slot) ||
                !slots.Add(selection.Slot))
                throw new ArgumentException("Catalog equipment must have a unique, nonempty slot.");
            // Deliberate Custom option in the catalog picker: a non-weapon
            // item may carry locally supplied static stats. Catalog items, by
            // contrast, must always use the server-owned item data.
            if (selection.ItemId is null &&
                string.Equals(selection.ItemKey, "custom-" + selection.Slot,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (selection.Slot is "main-hand" or "off-hand" ||
                    selection.EnchantSpellId is not null ||
                    (selection.SetBonusStats?.Count ?? 0) != 0 ||
                    string.IsNullOrWhiteSpace(selection.Name))
                    throw new ArgumentException("Custom equipment requires a name, is not a weapon, and cannot fake a catalog enchant or set bonus.");
                resolved.Gear.Add(new WarriorGearEntry
                {
                    Slot = selection.Slot, ItemKey = selection.ItemKey,
                    Name = selection.Name,
                    Stats = new Dictionary<string, decimal>(selection.Stats ?? new(),
                        StringComparer.OrdinalIgnoreCase),
                    EnchantName = selection.EnchantName,
                    EnchantStats = new Dictionary<string, decimal>(
                        selection.EnchantStats ?? new(), StringComparer.OrdinalIgnoreCase)
                });
                continue;
            }
            if (selection.ItemId is not int itemId || itemId <= 0)
                throw new ArgumentException($"Catalog slot '{selection.Slot}' requires a real item ID.");
            if ((selection.Stats?.Count ?? 0) != 0 || (selection.EnchantStats?.Count ?? 0) != 0 ||
                (selection.SetBonusStats?.Count ?? 0) != 0)
                throw new ArgumentException("Catalog selections cannot override server-owned item, enchant, or set-bonus stats.");
            var item = FindItem(itemId);
            if (!item.AllowedSlots.Contains(selection.Slot, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"{item.Name} cannot be equipped in '{selection.Slot}'.");
            if (item.RequiredLevel > request.Level)
                throw new ArgumentException($"{item.Name} requires level {item.RequiredLevel}.");
            if (item.UniqueEquipped && !uniqueIds.Add(item.Id))
                throw new ArgumentException($"{item.Name} is unique-equipped and cannot be used twice.");

            var gear = new WarriorGearEntry
            {
                Slot = selection.Slot,
                ItemKey = $"forever-item-{item.Id}",
                ItemId = item.Id,
                Name = item.Name,
                Stats = new Dictionary<string, decimal>(item.Stats, StringComparer.OrdinalIgnoreCase)
            };
            if (selection.EnchantSpellId is int enchantId)
            {
                var enchant = FindEnchant(enchantId);
                if (!enchant.Modeled)
                    throw new ArgumentException($"{enchant.Name} is not simulated yet; it cannot be selected as a static stat bonus.");
                if (!enchant.AllowedSlots.Contains(selection.Slot, StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException($"{enchant.Name} cannot enchant '{selection.Slot}'.");
                gear.EnchantSpellId = enchantId;
                gear.EnchantName = enchant.Name;
                gear.EnchantStats = new Dictionary<string, decimal>(enchant.Stats, StringComparer.OrdinalIgnoreCase);
                if (selection.Slot.Equals("main-hand", StringComparison.OrdinalIgnoreCase))
                    mainDamageEnchant = enchant.WeaponDamageBonus;
                if (selection.Slot.Equals("off-hand", StringComparison.OrdinalIgnoreCase))
                    offDamageEnchant = enchant.WeaponDamageBonus;
            }
            if (!string.IsNullOrWhiteSpace(item.SetKey))
            {
                if (!equippedSets.TryGetValue(item.SetKey, out var members))
                    equippedSets.Add(item.SetKey, members = []);
                members.Add(gear);
            }
            if (selection.Slot.Equals("main-hand", StringComparison.OrdinalIgnoreCase)) main = item;
            if (selection.Slot.Equals("off-hand", StringComparison.OrdinalIgnoreCase)) off = item;
            resolved.Gear.Add(gear);
        }

        var mainWeapon = main?.Weapon ??
            throw new ArgumentException("A catalog Warrior loadout requires an equipped main-hand weapon.");
        if (off is not null && (mainWeapon.Hand == "two-hand" || off.Weapon?.Hand != "one-hand"))
            throw new ArgumentException("Two-handed weapons cannot be paired with off-hands; off-hand DPS needs a one-handed weapon.");

        foreach (var set in GetCatalog().Sets)
        {
            if (equippedSets.TryGetValue(set.Key, out var members) && members.Count >= set.RequiredPieces)
            {
                members[0].SetBonusName = $"{set.Name} ({set.RequiredPieces})";
                members[0].SetBonusStats = new Dictionary<string, decimal>(set.Stats, StringComparer.OrdinalIgnoreCase);
            }
        }

        resolved.MainHandWeaponType = mainWeapon.Type;
        resolved.OffHandWeaponType = off?.Weapon?.Type ?? "";
        resolved.MainHandMinimumDamage = mainWeapon.MinimumDamage + mainDamageEnchant;
        resolved.MainHandMaximumDamage = mainWeapon.MaximumDamage + mainDamageEnchant;
        resolved.MainHandSpeedSeconds = mainWeapon.SpeedSeconds;
        resolved.IsTwoHanded = mainWeapon.Hand == "two-hand";
        resolved.DualWield = off is not null;
        if (off?.Weapon is { } offWeapon)
        {
            resolved.OffHandMinimumDamage = offWeapon.MinimumDamage + offDamageEnchant;
            resolved.OffHandMaximumDamage = offWeapon.MaximumDamage + offDamageEnchant;
            resolved.OffHandSpeedSeconds = offWeapon.SpeedSeconds;
        }
        return resolved;
    }

    private static ForeverEquipmentCatalogDocument Load()
    {
        var assembly = typeof(ForeverEquipmentCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().Single(x =>
            x.EndsWith("forever-equipment-v1.json", StringComparison.OrdinalIgnoreCase));
        using var stream = assembly.GetManifestResourceStream(name) ??
            throw new InvalidOperationException("Embedded Forever equipment catalog not found.");
        var document = JsonSerializer.Deserialize<ForeverEquipmentCatalogDocument>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidOperationException("Forever equipment catalog is empty.");
        if (document.Items.Select(x => x.Id).Distinct().Count() != document.Items.Count ||
            document.Enchants.Select(x => x.SpellId).Distinct().Count() != document.Enchants.Count)
            throw new InvalidOperationException("The equipment catalog contains duplicate item or enchant IDs.");
        return document;
    }
}

public sealed class ForeverEquipmentCatalogDocument
{
    public string CatalogVersion { get; set; } = "";
    public string Scope { get; set; } = "";
    public List<ForeverEquipmentItem> Items { get; set; } = [];
    public List<ForeverEquipmentEnchant> Enchants { get; set; } = [];
    public List<ForeverEquipmentSetBonus> Sets { get; set; } = [];
}

public sealed class ForeverEquipmentItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int RequiredLevel { get; set; }
    public List<string> AllowedSlots { get; set; } = [];
    public bool UniqueEquipped { get; set; }
    public string? SetKey { get; set; }
    public Dictionary<string, decimal> Stats { get; set; } = new();
    public ForeverEquipmentWeapon? Weapon { get; set; }
    public string SourceUrl { get; set; } = "";
    public string SourceStatus { get; set; } = "";
    public List<string> Notes { get; set; } = [];
}

public sealed class ForeverEquipmentWeapon
{
    public string Hand { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal MinimumDamage { get; set; }
    public decimal MaximumDamage { get; set; }
    public decimal SpeedSeconds { get; set; }
}

public sealed class ForeverEquipmentEnchant
{
    public int SpellId { get; set; }
    public string Name { get; set; } = "";
    public bool Modeled { get; set; } = true;
    public List<string> AllowedSlots { get; set; } = [];
    public Dictionary<string, decimal> Stats { get; set; } = new();
    public decimal WeaponDamageBonus { get; set; }
    public string SourceUrl { get; set; } = "";
    public string SourceStatus { get; set; } = "";
    public List<string> Notes { get; set; } = [];
}

public sealed class ForeverEquipmentSetBonus
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public int RequiredPieces { get; set; }
    public Dictionary<string, decimal> Stats { get; set; } = new();
    public string SourceUrl { get; set; } = "";
}
