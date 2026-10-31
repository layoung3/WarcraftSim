using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Api.Simulation;

/// <summary>Opt-in identifiers only. An empty selection changes no legacy simulation.</summary>
public sealed class ForeverExternalModifierSelection
{
    public List<string> Buffs { get; set; }=[];
    public List<string> Consumables { get; set; }=[];
    public List<string> TargetDebuffs { get; set; }=[];
    /// <summary>initial-only preserves each spell's natural lifetime; maintained
    /// reapplies after expiry at no simulated player action or free damage.</summary>
    public string TargetDebuffMode { get; set; } = "initial-only";
}

public sealed record ForeverExternalModifierDefinition(
    string Key,string Name,string Category,int SpellId,int? ItemId,
    int MinimumLevel,decimal DurationSeconds,string ExclusiveGroup,
    string SourceBuild,string SourceUrl,string VerificationStatus,
    Dictionary<string,decimal> Stats)
{
    public bool Modeled { get; init; }=true;
}

/// <summary>
/// A small, fully source-labelled working subset of the Forever client modifier
/// vocabulary. Unknown effects are not promoted. Source build is the published
/// tooltip snapshot (70532), not an assertion of independent 70707 hotfix values.
/// </summary>
public static class ForeverExternalModifierCatalog
{
    public const string SourceBuild="1.60.1.70532";
    private static Dictionary<string,decimal> Stats(params (string Key,decimal Value)[] values)=>
        values.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
    public static IReadOnlyList<ForeverExternalModifierDefinition> Entries { get; }=[
        new("arcane-intellect","Arcane Intellect (Rank 5)","external-buff",10157,null,56,
            3600m,"intellect-external",SourceBuild,
            "https://wow-forever.gg/db/spells/10157-arcane-intellect/",
            "published-forever-client-tooltip-provisional",
            Stats(("intellect",31m))),
        new("power-word-fortitude","Power Word: Fortitude (Rank 6)","external-buff",10938,null,60,
            3600m,"stamina-external",SourceBuild,
            "https://wow-forever.gg/db/spells/10938-power-word-fortitude/",
            "published-forever-client-tooltip-provisional",
            Stats(("stamina",70m))),
        new("battle-shout","Battle Shout (Rank 7)","external-buff",25289,null,60,
            180m,"external-melee-ap",SourceBuild,
            "https://wow-forever.gg/db/spells/25289-battle-shout/",
            "published-forever-client-tooltip-provisional",
            Stats(("melee-attack-power",139m))),
        new("blessing-of-might","Blessing of Might (Rank 7)","external-buff",25291,null,60,
            3600m,"external-melee-ap",SourceBuild,
            "https://wow-forever.gg/db/spells/25291-blessing-of-might/",
            "published-forever-client-tooltip-provisional",
            Stats(("melee-attack-power",133m))),
        new("greater-arcane-elixir","Greater Arcane Elixir","consumable",16889,13454,45,
            1800m,"elixir-spell-power",SourceBuild,
            "https://wow-forever.gg/db/spells/16889-greater-arcane-elixir/",
            "published-forever-client-tooltip-provisional",
            Stats(("spell-power",35m))),
        new("elixir-of-the-mongoose","Elixir of the Mongoose","consumable",17538,13452,46,
            1800m,"elixir-agility",SourceBuild,
            "https://www.wowhead.com/forever/spell=17538/elixir-of-the-mongoose",
            "forever-tooltip-conflicting-stat-effect-fields-provisional",
            Stats(("agility",25m),("attack-critical-chance-percent",2m))),
        new("flask-of-supreme-power","Flask of Supreme Power","consumable",17628,13512,50,
            7200m,"flask",SourceBuild,
            "https://foreverdb.net/item/13512","published-forever-client-item-tooltip-provisional",
            Stats(("spell-power",150m))),
        new("flask-of-the-titans","Flask of the Titans","consumable",17626,13510,50,
            7200m,"flask",SourceBuild,
            "https://foreverdb.net/item/13510","published-forever-client-item-tooltip-provisional",
            Stats(("health",1200m))),
        new("flask-of-distilled-wisdom","Flask of Distilled Wisdom","consumable",17627,13511,50,
            7200m,"flask",SourceBuild,
            "https://foreverdb.net/item/13511","published-forever-client-item-tooltip-provisional",
            Stats(("mana",2000m))),
        new("elixir-of-the-sages","Elixir of the Sages","consumable",17535,13447,38,
            3600m,"elixir-intellect-spirit",SourceBuild,
            "https://foreverdb.net/item/13447","published-forever-client-item-tooltip-provisional",
            Stats(("intellect",18m),("spirit",18m))),
        new("elixir-of-greater-agility","Elixir of Greater Agility","consumable",11334,9187,38,
            3600m,"elixir-agility",SourceBuild,
            "https://foreverdb.net/item/9187","published-forever-client-item-tooltip-provisional",
            Stats(("agility",25m))),
        new("elixir-of-greater-strength","Elixir of Greater Strength","consumable",11405,9206,48,
            3600m,"elixir-strength",SourceBuild,
            "https://foreverdb.net/item/9206","published-forever-client-item-tooltip-provisional",
            Stats(("strength",25m))),
        new("elixir-of-lesser-fortitude","Elixir of Lesser Fortitude","consumable",3593,3825,25,
            3600m,"elixir-health",SourceBuild,
            "https://foreverdb.net/item/3825","published-forever-client-item-tooltip-provisional",
            Stats(("health",120m))),
        new("grilled-squid","Grilled Squid (Well Fed)","consumable",0,13928,35,
            900m,"well-fed-food",SourceBuild,
            "https://foreverdb.net/item/13928","published-item-effect-spell-id-unresolved-provisional",
            Stats(("attack-critical-chance-percent",1m))),
        new("nightfin-soup","Nightfin Soup (Well Fed)","consumable",0,13931,35,
            900m,"well-fed-food",SourceBuild,
            "https://foreverdb.net/item/13931","published-item-effect-spell-id-unresolved-provisional",
            Stats(("spell-power",22m))),
        new("runn-tum-tuber-surprise","Runn Tum Tuber Surprise (Well Fed)","consumable",0,18254,35,
            900m,"well-fed-food",SourceBuild,
            "https://foreverdb.net/item/18254","published-item-effect-spell-id-unresolved-provisional",
            Stats(("intellect",15m))),
        new("smoked-desert-dumplings","Smoked Desert Dumplings (Well Fed)","consumable",0,20452,45,
            900m,"well-fed-food",SourceBuild,
            "https://foreverdb.net/item/20452","published-item-effect-spell-id-unresolved-provisional",
            Stats(("strength",20m))),
        new("dirges-chimaerok-chops","Dirge's Kickin' Chimaerok Chops (Well Fed)","consumable",0,21023,45,
            900m,"well-fed-food",SourceBuild,
            "https://foreverdb.net/item/21023","published-item-effect-spell-id-unresolved-provisional",
            Stats(("stamina",25m))),
        new("faerie-fire-rank4","Faerie Fire (Rank 4)","target-debuff",9907,null,54,
            40m,"external-armor-weakening",SourceBuild,
            "https://wow-forever.gg/db/spells/9907-faerie-fire/",
            "published-forever-client-tooltip-provisional",
            Stats(("armor",-505m))),
        new("curse-of-elements","Curse of the Elements (Rank 4)","target-debuff",1311680,null,50,
            300m,"external-curse",SourceBuild,
            "https://wow-forever.gg/db/spells/1311680-curse-of-the-elements/",
            "published-forever-client-tooltip-provisional",
            Stats(("external-magic-vulnerability-percent",10m),
                ("arcane-resistance",-75m),("fire-resistance",-75m),
                ("frost-resistance",-75m),("nature-resistance",-75m),
                ("shadow-resistance",-75m)))
    ];
    public static ForeverExternalModifierDefinition Find(string key)=>
        Entries.FirstOrDefault(x=>x.Key.Equals(key?.Trim(),StringComparison.OrdinalIgnoreCase))
        ??throw new ArgumentException($"Unknown external modifier '{key}'.");
    public static IReadOnlyList<ForeverExternalModifierDefinition> Resolve(
        ForeverExternalModifierSelection selection,int level,
        decimal? encounterDurationSeconds=null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if(selection.TargetDebuffMode is not ("initial-only" or "maintained"))
            throw new ArgumentException("Target debuff mode must be initial-only or maintained.");
        if(level is <1 or >60)throw new ArgumentException("Forever level must be between 1 and 60.");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved=new List<ForeverExternalModifierDefinition>();
        void Add(IReadOnlyList<string>? keys,string category)
        {
            if(keys is null)throw new ArgumentException("Modifier selections cannot be null.");
            foreach(var key in keys)
            {
                var effect=Find(key);
                if(effect.Category!=category)
                    throw new ArgumentException($"Modifier '{key}' is not a {category}.");
                if(level<effect.MinimumLevel)
                    throw new ArgumentException($"Modifier '{key}' requires level {effect.MinimumLevel}.");
                // A static pre-pull contributor cannot silently outlive a
                // finite consumable or buff. The separately executed target
                // aura handles its own expiration.
                if(category!="target-debuff" && encounterDurationSeconds.HasValue &&
                   effect.DurationSeconds<encounterDurationSeconds.Value)
                    throw new ArgumentException(
                        $"Modifier '{key}' lasts only {effect.DurationSeconds}s; an external refresh policy is required for a {encounterDurationSeconds.Value}s simulation.");
                if(!seen.Add(effect.Key))
                    throw new ArgumentException($"Duplicate modifier '{effect.Key}'.");
                // Forever 70707 has no TBC Battle/Guardian elixir slots.
                // ExclusiveGroup identifies real effect families (e.g. two
                // agility elixirs), one flask, food, or duplicate class auras;
                // it is NOT an item-use cooldown category.
                // Source: bindonforever.com/articles/can-you-stack-elixirs-wow-forever
                if(!string.IsNullOrEmpty(effect.ExclusiveGroup) &&
                   !groups.Add(effect.ExclusiveGroup))
                    throw new ArgumentException($"Competing modifier group '{effect.ExclusiveGroup}'.");
                resolved.Add(effect);
            }
        }
        Add(selection.Buffs,"external-buff");
        Add(selection.Consumables,"consumable");
        Add(selection.TargetDebuffs,"target-debuff");
        return resolved;
    }

    /// <summary>
    /// Caster self buffs use the existing CharacterBuffStatContributor and the
    /// existing physical/spell derived conversions, not a duplicate formula.
    /// Source attributes are retained as buff:cp173-[key] contributions.
    /// </summary>
    public static CharacterProfile ApplyToProfile(CharacterProfile source,
        ForeverExternalModifierSelection selection,
        decimal? encounterDurationSeconds=null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var chosen=Resolve(selection,source.Level,encounterDurationSeconds);
        var self=chosen.Where(x=>x.Category!="target-debuff").ToArray();
        if(self.Length==0)return source;
        var copy=System.Text.Json.JsonSerializer.Deserialize<CharacterProfile>(
            System.Text.Json.JsonSerializer.Serialize(source))
            ?? throw new InvalidOperationException("Character profile source copy failed.");
        foreach(var effect in self)
            copy.Buffs.Selections.Add(new CharacterBuffSelection {
                BuffKey="cp173-"+effect.Key,Name=effect.Name,IsEnabled=true,
                Stats=new StatCollection {
                    Values=new Dictionary<string,decimal>(effect.Stats,StringComparer.OrdinalIgnoreCase)
                }
            });
        return copy;
    }

    public static Dictionary<string,decimal> SumSelfRawStats(
        ForeverExternalModifierSelection selection,int level,
        decimal? encounterDurationSeconds=null)
    {
        var totals=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
        foreach(var source in Resolve(selection,level,encounterDurationSeconds).Where(x=>x.Category!="target-debuff"))
            foreach(var (key,value) in source.Stats)
                totals[key]=totals.GetValueOrDefault(key)+value;
        return totals;
    }
}
