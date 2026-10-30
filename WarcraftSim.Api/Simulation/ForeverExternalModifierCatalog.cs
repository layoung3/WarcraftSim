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
            1800m,"battle-elixir",SourceBuild,
            "https://wow-forever.gg/db/spells/16889-greater-arcane-elixir/",
            "published-forever-client-tooltip-provisional",
            Stats(("spell-power",35m))),
        new("elixir-of-the-mongoose","Elixir of the Mongoose","consumable",17538,13452,46,
            1800m,"battle-elixir",SourceBuild,
            "https://www.wowhead.com/forever/spell=17538/elixir-of-the-mongoose",
            "forever-tooltip-conflicting-stat-effect-fields-provisional",
            Stats(("agility",25m),("attack-critical-chance-percent",2m))),
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
        ForeverExternalModifierSelection selection,int level)
    {
        ArgumentNullException.ThrowIfNull(selection);
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
                if(!seen.Add(effect.Key))
                    throw new ArgumentException($"Duplicate modifier '{effect.Key}'.");
                // Conservative exclusion within one named category; do not
                // silently stack competing elixir or alternate rank sources.
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
        ForeverExternalModifierSelection selection)
    {
        ArgumentNullException.ThrowIfNull(source);
        var chosen=Resolve(selection,source.Level);
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
        ForeverExternalModifierSelection selection,int level)
    {
        var totals=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
        foreach(var source in Resolve(selection,level).Where(x=>x.Category!="target-debuff"))
            foreach(var (key,value) in source.Stats)
                totals[key]=totals.GetValueOrDefault(key)+value;
        return totals;
    }
}
