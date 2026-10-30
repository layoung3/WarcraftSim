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
            if (item.AllowedFactions.Count > 0)
            {
                var faction = ForeverRaceFactions.GetFaction(request.Race);
                if (faction is null || !item.AllowedFactions.Contains(faction, StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException($"{item.Name} is available only to {string.Join(" / ", item.AllowedFactions)} characters; race '{request.Race}' does not qualify.");
            }
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
        if (off is not null && (mainWeapon.Hand == "two-hand" ||
            (off.Weapon?.Hand != "one-hand" && off.ShieldBlockValue <= 0m)))
            throw new ArgumentException("Two-handed weapons cannot be paired with an off-hand; only one-handed weapons or shields are valid off-hands.");

        foreach (var set in GetCatalog().Sets)
        {
            if (set.Modeled && equippedSets.TryGetValue(set.Key, out var members) && members.Count >= set.RequiredPieces)
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
        resolved.ShieldEquipped = off?.ShieldBlockValue > 0m;
        if (resolved.ShieldEquipped)
        {
            resolved.ShieldIntrinsicBlockValue = off!.ShieldBlockValue;
            resolved.PlayerBlockValue = request.TankStatMode == "derived"
                ? off!.ShieldBlockValue : request.PlayerBlockValue + off!.ShieldBlockValue;
        }
        resolved.DualWield = off?.Weapon is not null;
        if (off?.Weapon is { } offWeapon)
        {
            resolved.OffHandMinimumDamage = offWeapon.MinimumDamage + offDamageEnchant;
            resolved.OffHandMaximumDamage = offWeapon.MaximumDamage + offDamageEnchant;
            resolved.OffHandSpeedSeconds = offWeapon.SpeedSeconds;
        }
        return resolved;
    }

    /// <summary>
    /// Opt-in, fail-closed reviewed catalog overlay; caller must pin exact bytes.
    /// File is read only at startup; shared stat resolver stays authoritative.
    /// </summary>
    public static ForeverEquipmentCatalogDocument LoadApprovedLocalManifest(
        ForeverEquipmentCatalogDocument baseline,string path,string expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if(string.IsNullOrWhiteSpace(path) ||
           string.IsNullOrWhiteSpace(expectedSha256) ||
           expectedSha256.Length!=64 ||
           !expectedSha256.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Approved equipment manifest requires an absolute path and 64-digit SHA-256.");
        if(!Path.IsPathFullyQualified(path) || !File.Exists(path))
            throw new InvalidOperationException("Approved equipment manifest must exist at a fully qualified local file path.");
        var data=File.ReadAllBytes(path);
        var actual=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data));
        if(!actual.Equals(expectedSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Approved equipment manifest SHA-256 does not match the pinned bytes.");
        var result=ForeverEquipmentSnapshotPipeline.Import(
            System.Text.Encoding.UTF8.GetString(data),baseline);
        return result.Catalog;
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
        var reviewName = assembly.GetManifestResourceNames().SingleOrDefault(x =>
            x.EndsWith("forever-equipment-cp171-reviewed.json", StringComparison.OrdinalIgnoreCase));
        if (reviewName is not null)
        {
            using var reviewStream = assembly.GetManifestResourceStream(reviewName) ??
                throw new InvalidOperationException("Embedded approved CP171 equipment snapshot missing.");
            using var reader = new StreamReader(reviewStream);
            document = ForeverEquipmentSnapshotPipeline.Import(reader.ReadToEnd(), document).Catalog;
        }
        // The CP172 source document extends (rather than replaces) CP171.
        // All IDs remain server-owned and all imported client fields remain
        // checked by the existing resolver.
        var cp172=ForeverEquipmentEffectsCatalog.GetCatalog();
        if(cp172.Items.Any(x=>document.Items.Any(y=>y.Id==x.Id)) ||
           cp172.Enchants.Any(x=>document.Enchants.Any(y=>y.SpellId==x.SpellId)))
            throw new InvalidOperationException("CP172 source duplicates a registered equipment ID.");
        foreach(var sourceEffect in cp172.Effects.Where(x=>x.ItemId is not null))
        {
            var sourceItem=cp172.Items.FirstOrDefault(x=>x.Id==sourceEffect.ItemId) ??
                document.Items.FirstOrDefault(x=>x.Id==sourceEffect.ItemId);
            if(sourceItem is null)
                throw new InvalidOperationException($"Equipment effect {sourceEffect.Key} references unknown item {sourceEffect.ItemId}.");
            foreach(var linked in sourceItem.Effects.Where(x=>x.SpellId==sourceEffect.SpellId &&
                sourceEffect.SpellId.HasValue))
                linked.ExecutionStatus=sourceEffect.Modeled?"modeled":"not-modeled";
        }
        document.Items.AddRange(cp172.Items);
        document.Enchants.AddRange(cp172.Enchants);
        document.CatalogVersion=cp172.CatalogVersion;
        // Optional LOCAL approved manifest for later client builds. Exact input
        // hash is required; no network access or per-class gear databases.
        var approvedPath = Environment.GetEnvironmentVariable("WARCRAFTSIM_FOREVER_APPROVED_MANIFEST");
        if (!string.IsNullOrWhiteSpace(approvedPath))
        {
            var approvedDigest = Environment.GetEnvironmentVariable("WARCRAFTSIM_FOREVER_APPROVED_SHA256");
            document = LoadApprovedLocalManifest(document, approvedPath, approvedDigest ?? "");
        }
        if (document.Items.Select(x => x.Id).Distinct().Count() != document.Items.Count ||
            document.Enchants.Select(x => x.SpellId).Distinct().Count() != document.Enchants.Count)
            throw new InvalidOperationException("The equipment catalog contains duplicate item or enchant IDs.");
        if (document.Items.Any(x => x.AllowedFactions.Any(y => y is not ("horde" or "alliance"))) ||
            document.Items.Any(x => !string.IsNullOrEmpty(x.SetKey) &&
                document.Sets.All(set => !set.Key.Equals(x.SetKey, StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("Equipment catalog contains an unknown faction or set definition.");
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
    public string UniqueEquippedGroup { get; set; } = "";
    public List<string> AllowedClasses { get; set; } = [];
    public string Quality { get; set; } = "";
    public string ItemClass { get; set; } = "";
    public string ItemSubclass { get; set; } = "";
    public string BindType { get; set; } = "";
    public string SourceBuild { get; set; } = "";
    public string VerificationStatus { get; set; } = "";
    public string SnapshotDate { get; set; } = "";
    // Empty = any faction. A restriction is checked against the selected race,
    // including when race-base stat mode is not enabled.
    public List<string> AllowedFactions { get; set; } = [];
    public string? SetKey { get; set; }
    public Dictionary<string, decimal> Stats { get; set; } = new();
    public ForeverEquipmentWeapon? Weapon { get; set; }
    public decimal ShieldBlockValue { get; set; }

    public string SourceUrl { get; set; } = "";
    public string SourceStatus { get; set; } = "";
    public List<string> Notes { get; set; } = [];
    // Linked spell/equip/proc metadata, not automatically executable combat bonuses.
    public List<ForeverEquipmentEffectReference> Effects { get; set; } = [];
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
    public bool IsTemporary { get; set; }
    public decimal DurationSeconds { get; set; }
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
    public bool Modeled { get; set; } = true;
    public string Notes { get; set; } = "";
    public Dictionary<string, decimal> Stats { get; set; } = new();
    public string SourceUrl { get; set; } = "";
}
