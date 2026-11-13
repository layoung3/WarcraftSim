namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>
/// Class-wide allocation identities. Historical source rows/caps are overlaid only
/// where dated official Forever development/hotfix notes explicitly supersede them.
/// Legality is independent of scenario coverage and preset identity.
/// </summary>
public static class ForeverShamanClassTalents
{
    public const string HistoricalCommit = "1cc5ae1e9abdbf941b7326d2d90421822dce31ba";
    public const string ReviewedDate = "2026-11-12";
    public const string DevelopmentNotesUrl = "https://us.forums.blizzard.com/en/wow/t/wow-forever-beta-development-notes-%E2%80%93-updated-november-2/2360696";
    public const string HotfixNotesUrl = "https://worldofwarcraft.blizzard.com/en-us/news/24315281/wow-forever-hotfixes-november-6-2026";
    public const string HistoricalTreeUrl = "https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/shaman.json";
    public const string ClientTreeUrl = "https://theforeverera.com/en/talents/shaman/";
    public static IReadOnlyList<ShamanClassTalentDefinition> Entries { get; } =
    [
        new("convection","elemental",5,1,2,null,0),
        new("concussion","elemental",5,1,3,null,0),
        new("elemental-devastation","elemental",3,2,1,null,0),
        new("reverberation","elemental",5,2,2,null,0),
        new("elemental-alacrity","elemental",3,2,3,null,0),
        new("call-of-flame","elemental",3,2,4,null,0),
        new("elemental-focus","elemental",1,3,1,null,0),
        new("elemental-warding","elemental",3,3,2,null,0),
        new("trial-by-fire","elemental",2,4,1,null,0),
        new("eye-of-the-storm","elemental",3,4,2,null,0),
        new("earthbound","elemental",1,4,4,null,0),
        new("elemental-reach","elemental",2,5,1,null,0),
        new("call-of-thunder","elemental",1,5,2,null,0),
        new("lightning-overload","elemental",3,5,3,"elemental-alacrity",3),
        new("elemental-fury","elemental",5,6,3,"lightning-overload",3),
        new("lightning-mastery","elemental",1,7,2,"call-of-thunder",1),
        new("earths-grasp","enhancement",2,1,1,null,0),
        new("thundering-strikes","enhancement",5,1,2,null,0),
        new("ancestral-knowledge","enhancement",5,1,3,null,0),
        new("guardian-totems","enhancement",2,2,1,null,0),
        new("mental-dexterity","enhancement",3,2,2,null,0),
        new("improved-ghost-wolf","enhancement",2,4,4,null,0),
        new("improved-lightning-shield","enhancement",3,2,3,null,0),
        new("elemental-weapons","enhancement",3,3,1,null,0),
        new("shamanistic-focus","enhancement",1,3,3,null,0),
        new("anticipation","enhancement",3,3,4,null,0),
        new("toughness","enhancement",5,4,1,null,0),
        new("flurry","enhancement",5,4,2,"mental-dexterity",3),
        new("stormstrike","enhancement",1,4,3,null,0),
        new("spirit-weapons","enhancement",1,5,1,null,0),
        new("mental-quickness","enhancement",2,5,2,null,0),
        new("improved-stormstrike","enhancement",2,5,3,"stormstrike",1),
        new("maelstrom-weapon","enhancement",5,6,3,null,0),
        new("rage-of-the-farseer","enhancement",1,7,2,"mental-quickness",2),
        new("improved-healing-wave","restoration",5,1,2,null,0),
        new("tidal-focus","restoration",5,1,3,null,0),
        new("mindfulness","restoration",3,2,1,null,0),
        new("natural-grace","restoration",3,2,2,null,0),
        new("totemic-focus","restoration",5,2,3,null,0),
        new("improved-reincarnation","restoration",2,2,4,null,0),
        new("ancestral-healing","restoration",3,3,1,null,0),
        new("healing-focus","restoration",3,3,2,null,0),
        new("water-shield","restoration",1,3,3,null,0),
        new("tidal-mastery","restoration",5,4,1,null,0),
        new("restorative-totems","restoration",5,4,2,null,0),
        new("mana-tide-totem","restoration",1,4,3,null,0),
        new("healing-way","restoration",3,5,2,null,0),
        new("nature-swiftness","restoration",1,5,3,null,0),
        new("purification","restoration",5,6,3,null,0),
        new("riptide","restoration",1,7,2,"healing-way",3),
    ];
    public static IReadOnlyList<(string Key,string Tree,int MaxRank)> SupportedTalentMetadata =>
        Entries.Select(x => (x.Key,x.Tree,x.MaxRank)).ToArray();
    public static ShamanClassTalentDefinition? Find(string key) =>
        Entries.FirstOrDefault(x => x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
    public static void Validate(IReadOnlyDictionary<string,int> ranks, int elemental,
        int enhancement,int restoration,int level = 60)
    {
        ArgumentNullException.ThrowIfNull(ranks);
        // Canonical identities are case-insensitive; duplicates differing only
        // by case are rejected rather than bypassing row/parent checks.
        ranks=ranks.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
        if(level is <10 or >60 || elemental<0 || enhancement<0 || restoration<0 ||
            elemental+enhancement+restoration>level-9)
            throw new ArgumentException("Class-wide Shaman talent points exceed the level budget.");
        var totals = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(var (key,rank) in ranks)
        {
            var node=Find(key);
            if(node is null || rank<0 || rank>node.MaxRank)
                throw new ArgumentException($"Unknown or invalid class-wide Shaman talent: {key}={rank}.");
            totals[node.Tree]=totals.GetValueOrDefault(node.Tree)+rank;
        }
        if(totals.GetValueOrDefault("elemental")!=elemental ||
            totals.GetValueOrDefault("enhancement")!=enhancement ||
            totals.GetValueOrDefault("restoration")!=restoration)
            throw new ArgumentException("Class-wide Shaman tree totals must equal their named ranks.");
        foreach(var node in Entries.Where(x=>ranks.GetValueOrDefault(x.Key)>0))
        {
            var earlier=Entries.Where(x=>x.Tree==node.Tree && x.Row<node.Row)
                .Sum(x=>ranks.GetValueOrDefault(x.Key));
            if(earlier<(node.Row-1)*5)
                throw new ArgumentException($"{node.Key} needs {(node.Row-1)*5} points in earlier {node.Tree} rows, found {earlier}.");
            if(node.ParentKey is not null && ranks.GetValueOrDefault(node.ParentKey)<node.ParentRank)
                throw new ArgumentException($"{node.Key} requires {node.ParentKey} rank {node.ParentRank}.");
        }
    }
    public static decimal MindfulnessFraction(int rank) => rank switch {1=>.17m,2=>.33m,3=>.50m,_=>0m};
    public static decimal MentalDexterityFraction(int rank) => rank switch {1=>.33m,2=>.66m,3=>1m,_=>0m};
    public static decimal AncestralHealingArmorFraction(int rank) => rank switch {1=>.08m,2=>.17m,3=>.25m,_=>0m};
    public static decimal PushbackResistance(int rank) => rank switch {1=>23m,2=>47m,3=>70m,_=>0m};
    public static decimal WardingFraction(int rank) => rank switch {1=>.03m,2=>.07m,3=>.10m,_=>0m};
}

public sealed record ShamanClassTalentDefinition(string Key,string Tree,int MaxRank,
    int Row,int Column,string? ParentKey,int ParentRank)
{
    public ShamanTalentCoverage Coverage => ForeverShamanClassCoverage.Entries[Key];
    public string SourceConfidence => "reviewed-client-70888-mirror-with-official-hotfix-overlay-provisional";
    public string SourceUrl => Key is "call-of-thunder" or "lightning-mastery" or
        "improved-ghost-wolf" or "flurry" or "riptide" or "lightning-overload"
        ? ForeverShamanClassTalents.DevelopmentNotesUrl : Key is "mental-dexterity" or "maelstrom-weapon" or "stormstrike"
        ? ForeverShamanClassTalents.HotfixNotesUrl : ForeverShamanClassTalents.ClientTreeUrl;
}
