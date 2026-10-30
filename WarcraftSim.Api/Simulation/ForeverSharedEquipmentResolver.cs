using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Data.Forever.Simulation;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Catalog-backed, class-neutral normalization before Core's existing
/// CharacterEquipmentStatContributor pipeline. No combat formula lives here.
/// Raw external records are never considered usable equipment until curated
/// into a versioned local catalog entry with a stable game ID.
/// </summary>
public static class ForeverSharedEquipmentResolver
{
    public static readonly string[] Slots = [
        "head","neck","shoulder","back","chest","wrist","hands","waist",
        "legs","feet","finger1","finger2","trinket1","trinket2",
        "main-hand","off-hand","ranged"
    ];

    public static ForeverResolvedEquipment Resolve(
        CharacterEquipmentLoadout loadout,string classKey,int level=60,string? race=null)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(loadout.Items);
        if (string.IsNullOrWhiteSpace(classKey) || level is <1 or >60)
            throw new ArgumentException("Valid class and level 1–60 required.");
        if(loadout.Items.Count>Slots.Length)
            throw new ArgumentException("Equipment contains more than 17 slot selections.");

        var result=new ForeverResolvedEquipment();
        var slots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uniqueIds=new HashSet<int>();
        var uniqueGroups=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sets=new Dictionary<string,List<CharacterEquipmentItem>>(StringComparer.OrdinalIgnoreCase);
        ForeverEquipmentItem? main=null,off=null;

        foreach(var selection in loadout.Items)
        {
            if(selection is null)throw new ArgumentException("Equipment contains a null item.");
            if(string.IsNullOrWhiteSpace(selection.SlotKey))
                throw new ArgumentException("Each equipped item requires a slot.");
            var slot=selection.SlotKey.Trim().ToLowerInvariant() switch {
                "shoulders" => "shoulder",
                "wrists" => "wrist",
                "ring1" => "finger1", "ring2" => "finger2",
                var other=>other
            };
            if(!Slots.Contains(slot,StringComparer.OrdinalIgnoreCase) || !slots.Add(slot))
                throw new ArgumentException($"Invalid or duplicate equipment slot '{selection.SlotKey}'.");

            // Backwards-compatible authored stat-only gear is still supported
            // by the Core pipeline. It is not advertised as verified catalog data.
            if(selection.ItemId is null)
            {
                if(selection.EnchantSpellId is not null)
                    throw new ArgumentException("Catalog enchant requires a catalog item ID.");
                if(string.IsNullOrWhiteSpace(selection.ItemKey))
                    throw new ArgumentException($"Noncatalog equipment '{slot}' needs an item key.");
                result.Loadout.Items.Add(new CharacterEquipmentItem {
                    SlotKey=slot,ItemKey=selection.ItemKey,Name=selection.Name,
                    Stats=selection.Stats,Modifiers=selection.Modifiers
                });
                continue;
            }
            if(selection.ItemId is not >0 ||
               selection.Stats is null || selection.Modifiers is null ||
               selection.Stats.Values.Count>0 || selection.Modifiers.Count>0)
                throw new ArgumentException("Catalog equipment cannot override item stats or modifiers.");

            var item=ForeverEquipmentCatalog.FindItem(selection.ItemId.Value);
            if(!item.AllowedSlots.Contains(slot,StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"{item.Name} cannot occupy slot '{slot}'.");
            if(level<item.RequiredLevel)
                throw new ArgumentException($"{item.Name} requires level {item.RequiredLevel}.");
            if(item.AllowedClasses.Count>0 &&
               !item.AllowedClasses.Contains(classKey,StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"{item.Name} cannot be used by {classKey}.");
            if(item.UniqueEquipped && !uniqueIds.Add(item.Id))
                throw new ArgumentException($"{item.Name} is unique-equipped.");
            if(!string.IsNullOrWhiteSpace(item.UniqueEquippedGroup) &&
               !uniqueGroups.Add(item.UniqueEquippedGroup))
                throw new ArgumentException($"Unique-equipped group '{item.UniqueEquippedGroup}' already present.");
            if(item.AllowedFactions.Count>0)
            {
                var faction=ForeverRaceFactions.GetFaction(race);
                if(faction is null ||
                   !item.AllowedFactions.Contains(faction,StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException(
                        $"{item.Name} requires one of: {string.Join(", ",item.AllowedFactions)}; race '{race??"unset"}' does not qualify.");
            }

            ValidateWeapon(classKey,slot,item);

            var normalized=new CharacterEquipmentItem {
                SlotKey=slot,ItemKey=$"forever-item-{item.Id}",ItemId=item.Id,
                Name=item.Name,
                Stats=new StatCollection{Values=new Dictionary<string,decimal>(
                    item.Stats,StringComparer.OrdinalIgnoreCase)}
            };
            if(selection.EnchantSpellId is int enchantId)
            {
                var enchant=ForeverEquipmentCatalog.FindEnchant(enchantId);
                if(!enchant.Modeled ||
                   !enchant.AllowedSlots.Contains(slot,StringComparer.OrdinalIgnoreCase))
                    throw new ArgumentException($"Enchant {enchantId} is not executable on '{slot}'.");
                normalized.EnchantSpellId=enchantId;
                normalized.Modifiers.Add(new CharacterEquipmentModifier{
                    PlacementKey="permanent",ModifierKey=$"enchant-{enchantId}",
                    ModifierType=CharacterEquipmentModifierTypes.Enchant,
                    Name=enchant.Name,
                    Stats=new StatCollection{Values=new Dictionary<string,decimal>(
                        enchant.Stats,StringComparer.OrdinalIgnoreCase)}
                });
            }
            if(!string.IsNullOrWhiteSpace(item.SetKey))
            {
                if(!sets.TryGetValue(item.SetKey,out var list))
                    sets[item.SetKey]=list=[];
                list.Add(normalized);
            }
            if(slot=="main-hand")main=item;
            if(slot=="off-hand")off=item;
            result.Loadout.Items.Add(normalized);
            result.Sources.Add(new ForeverEquipmentSource {
                ItemId=item.Id,Name=item.Name,Slot=slot,
                SourceUrl=item.SourceUrl,SourceStatus=item.SourceStatus,
                SourceBuild=item.SourceBuild,CatalogVersion=ForeverEquipmentCatalog.GetCatalog().CatalogVersion
            });
            if(item.Weapon is { } weapon)
            {
                var bonus=selection.EnchantSpellId is int id ?
                    ForeverEquipmentCatalog.FindEnchant(id).WeaponDamageBonus:0m;
                result.Weapons[slot]=new ForeverResolvedWeapon {
                    Type=weapon.Type,Hand=weapon.Hand,
                    MinimumDamage=weapon.MinimumDamage+bonus,
                    MaximumDamage=weapon.MaximumDamage+bonus,
                    SpeedSeconds=weapon.SpeedSeconds
                };
            }
        }

        if(main?.Weapon?.Hand=="two-hand" && off is not null)
            throw new ArgumentException("Two-handed main hand cannot be paired with an off hand.");
        if(off?.Weapon?.Hand=="two-hand")
            throw new ArgumentException("A two-handed weapon cannot occupy an off-hand slot.");

        foreach(var bonus in ForeverEquipmentCatalog.GetCatalog().Sets)
        {
            if(!sets.TryGetValue(bonus.Key,out var pieces) ||
               pieces.Count<bonus.RequiredPieces)continue;
            // One and only one set-bonus contributor at each threshold.
            pieces[0].Modifiers.Add(new CharacterEquipmentModifier{
                PlacementKey=$"set-{bonus.RequiredPieces}",
                ModifierKey=bonus.Key,ModifierType="set-bonus",
                Name=bonus.Name,
                Stats=new StatCollection{Values=new Dictionary<string,decimal>(
                    bonus.Stats,StringComparer.OrdinalIgnoreCase)}
            });
        }
        foreach(var item in result.Loadout.Items)
        {
            foreach(var (key,value) in item.Stats.Values)result.RawStats.Add(key,value);
            foreach(var modifier in item.Modifiers)
                foreach(var (key,value) in modifier.Stats.Values)
                    result.RawStats.Add(key,value);
        }
        // Use the same contributor ordering as Mage and Warlock, including
        // equipped stat sources, rating conversion and class-aware derivation.
        // A synthetic equipment-only profile contains no racial/base stats;
        // legacy Hunter numeric inputs are added by the class adapter once.
        var equipmentProfile=new CharacterProfile {
            ClassKey=classKey,Level=level,Equipment=result.Loadout
        };
        var computed=ForeverCharacterSimulationRuntimeCalculatorFactory
            .CreateStandard(_=>1m).Calculate(equipmentProfile);
        foreach(var (key,value) in computed.EffectiveStats.Values)
            result.Stats.Set(key,value);
        return result;
    }

    private static void ValidateWeapon(string classKey,string slot,ForeverEquipmentItem item)
    {
        var weapon=item.Weapon;
        if(weapon is null)
        {
            if(slot is "main-hand" or "ranged" && item.ShieldBlockValue==0m)
                throw new ArgumentException($"{item.Name} has no modeled weapon for '{slot}'.");
            if(slot=="off-hand" && item.ShieldBlockValue>0m &&
               classKey is "mage" or "warlock" or "hunter")
                throw new ArgumentException($"{classKey} cannot equip shields.");
            return;
        }
        if(slot=="ranged" && weapon.Hand!="ranged" ||
           slot=="main-hand" && weapon.Hand=="ranged" ||
           slot=="off-hand" && weapon.Hand!="one-hand")
            throw new ArgumentException($"{item.Name} has incompatible weapon hand.");
        if(classKey is "mage" or "warlock" &&
           weapon.Type is not ("staff" or "sword" or "dagger" or "wand"))
            throw new ArgumentException($"{classKey} cannot equip {weapon.Type}.");
        if(classKey=="hunter" && weapon.Type=="shield")
            throw new ArgumentException("Hunters cannot equip shields.");
    }
}

public sealed class ForeverResolvedEquipment
{
    public CharacterEquipmentLoadout Loadout { get; }=new();
    public StatCollection Stats { get; }=new();
    // Unconverted input sources; needed where an existing class tuning already
    // applies part of the same primary-stat conversion (e.g. Survival Agility).
    public StatCollection RawStats { get; }=new();
    public Dictionary<string,ForeverResolvedWeapon> Weapons { get; }=
        new(StringComparer.OrdinalIgnoreCase);
    public List<ForeverEquipmentSource> Sources { get; }=[];
}
public sealed class ForeverResolvedWeapon
{
    public string Hand { get; set; }="";
    public string Type { get; set; }="";
    public decimal MinimumDamage { get; set; }
    public decimal MaximumDamage { get; set; }
    public decimal SpeedSeconds { get; set; }
}
public sealed class ForeverEquipmentSource
{
    public int ItemId { get; set; }
    public string Name { get; set; }="";
    public string Slot { get; set; }="";
    public string SourceUrl { get; set; }="";
    public string SourceStatus { get; set; }="";
    public string SourceBuild { get; set; }="";
    public string CatalogVersion { get; set; }="";
}
