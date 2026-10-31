namespace WarcraftSim.Data.Forever.Warlock;

/// <summary>Modeled Warlock DPS subset. Points spent in unmodeled talents may be
/// included in tree totals; no unsupported effect is silently granted.</summary>
public sealed class ForeverWarlockTalentBuild
{
    public int AfflictionPointsSpent { get; set; }
    public int DemonologyPointsSpent { get; set; }
    public int DestructionPointsSpent { get; set; }
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, (string Tree, int Cap)> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ["improved-corruption"] = ("affliction", 5), ["grim-reach"] = ("affliction", 2),
        ["suppression"] = ("affliction", 5), ["improved-life-tap"] = ("affliction", 2),
        ["improved-drains"] = ("affliction", 3), ["pandemic"] = ("affliction", 3),
        ["malevolence"] = ("affliction", 5), ["shadow-mastery"] = ("affliction", 5),
        ["wrack"] = ("affliction", 1), ["siphon-life"] = ("affliction",1),
        ["improved-bane-of-agony"] = ("affliction",2), ["nightfall"] = ("affliction",2),
        ["soul-siphon"] = ("affliction",3), ["fel-concentration"] = ("affliction",3),
        ["soul-harvest"] = ("affliction",2),
        ["amplify-curse"] = ("affliction",1),
        ["demonic-embrace"] = ("demonology",5),
        ["demonic-aegis"] = ("demonology",2),
        ["unholy-power"] = ("demonology", 5), ["demonic-sacrifice"] = ("demonology", 1), ["soul-link"] = ("demonology", 1), ["fel-vitality"] = ("demonology", 3),
        ["demonic-knowledge"] = ("demonology", 3), ["demonic-pact"] = ("demonology", 1),
        ["master-demonologist"] = ("demonology", 5),
        ["demonic-brand"] = ("demonology", 3), ["decimation"] = ("demonology", 2),
        ["improved-felhunter"] = ("demonology", 3),
        ["improved-imp"] = ("demonology", 3), ["improved-sayaad"] = ("demonology", 3),
        ["improved-voidwalker"] = ("demonology", 3),
        ["improved-health-funnel"] = ("demonology", 2),
        ["demonic-energies"] = ("demonology",2),
        ["master-summoner"] = ("demonology", 2),
        ["fel-domination"] = ("demonology", 1),
        ["destructive-reach"] = ("destruction", 2),
        ["pyroclasm"] = ("destruction", 2),
        ["bane"] = ("destruction", 5), ["shadowburn"] = ("destruction",1),
        ["conflagrate"] = ("destruction",1), ["cataclysm"] = ("destruction", 3),
        ["ruin"] = ("destruction", 5), ["improved-shadow-bolt"] = ("destruction", 5),
        ["incinerate"] = ("destruction", 1), ["bane-of-havoc"] = ("destruction", 1),
        ["agonizing-flames"] = ("destruction", 3), ["shadow-and-flame"] = ("destruction", 5),
        ["fire-and-brimstone"] = ("destruction",3), ["molten-skin"] = ("destruction",5),
        ["intensity"] = ("destruction",3), ["aftermath"] = ("destruction",5)
    };
    private static readonly IReadOnlyDictionary<string,int> EarlierPoints = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase) {
        ["demonic-energies"]=5, ["demonic-aegis"]=5, ["demonic-knowledge"]=20,
        ["master-demonologist"]=25, ["master-summoner"]=10, ["fel-domination"]=15,
        ["soul-harvest"]=5, ["fel-concentration"]=10, ["soul-siphon"]=20,
        ["amplify-curse"]=10, ["improved-voidwalker"]=5, ["aftermath"]=5,
        ["molten-skin"]=5, ["intensity"]=15, ["ruin"]=10,
        ["agonizing-flames"]=15, ["pyroclasm"]=20, ["shadow-and-flame"]=25,
        ["fire-and-brimstone"]=20, ["nightfall"]=15, ["conflagrate"]=15,
        ["improved-bane-of-agony"]=10, ["decimation"]=15, ["demonic-brand"]=15,
        ["siphon-life"]=20, ["shadowburn"]=10, ["wrack"]=30,
        ["demonic-pact"]=30, ["soul-link"]=20, ["incinerate"]=30
    };
    public static IReadOnlyList<(string Key,string Tree,int MaxRank)> SupportedTalentMetadata =>
        Supported.Select(x=>(x.Key,x.Value.Tree,x.Value.Cap)).ToArray();
    public int Rank(string key) => Ranks is not null && Ranks.TryGetValue(key, out var rank) ? rank : 0;

    public void Validate(int level, string spec)
    {
        if (level != 60 || spec is not ("affliction" or "demonology" or "destruction") || Ranks is null ||
            AfflictionPointsSpent < 0 || DemonologyPointsSpent < 0 || DestructionPointsSpent < 0 ||
            AfflictionPointsSpent + DemonologyPointsSpent + DestructionPointsSpent > level - 9)
            throw new ArgumentException("Warlock requires a level-60 valid specialization and 51-point talent budget.");
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, rank) in Ranks)
        {
            if (!Supported.TryGetValue(key, out var info) || rank < 0 || rank > info.Cap)
                throw new ArgumentException($"Unmodeled or invalid Warlock talent: {key}={rank}.");
            used[info.Tree] = used.GetValueOrDefault(info.Tree) + rank;
        }
        if (used.GetValueOrDefault("affliction") > AfflictionPointsSpent ||
            used.GetValueOrDefault("demonology") > DemonologyPointsSpent ||
            used.GetValueOrDefault("destruction") > DestructionPointsSpent)
            throw new ArgumentException("Modeled Warlock talent ranks exceed assigned tree points.");
        foreach(var (key,required) in EarlierPoints)
        {
            var rank=Rank(key);if(rank<=0)continue;
            var tree=Supported[key].Tree;
            var allocated=tree switch {
                "affliction"=>AfflictionPointsSpent,
                "demonology"=>DemonologyPointsSpent,
                "destruction"=>DestructionPointsSpent,_=>0
            };
            if(allocated-rank<required)
                throw new ArgumentException($"Warlock {key} requires {required} points before its own ranks in {tree}.");
        }
        if (Rank("demonic-energies") > 0 && DemonologyPointsSpent < 6 ||
            Rank("demonic-aegis") > 0 && DemonologyPointsSpent < 6 ||
            Rank("demonic-knowledge") > 0 && DemonologyPointsSpent < 21 ||
            Rank("master-demonologist") > 0 && DemonologyPointsSpent < 26 ||
            Rank("master-summoner") > 0 && DemonologyPointsSpent < 11 ||
            Rank("fel-domination") > 0 &&
                (DemonologyPointsSpent < 16 || Rank("master-summoner") < 2) ||
            Rank("soul-harvest") > 0 && AfflictionPointsSpent < 6 ||
            Rank("fel-concentration") > 0 && AfflictionPointsSpent < 11 ||
            Rank("soul-siphon") > 0 && AfflictionPointsSpent < 21 ||
            Rank("amplify-curse") > 0 && AfflictionPointsSpent < 11 ||
            Rank("improved-voidwalker") > 0 && DemonologyPointsSpent < 6 ||
            Rank("aftermath") > 0 && DestructionPointsSpent < 6 ||
            Rank("molten-skin") > 0 && DestructionPointsSpent < 6 ||
            Rank("intensity") > 0 && DestructionPointsSpent < 16 ||
            Rank("ruin") > 0 && DestructionPointsSpent < 11 ||
            Rank("agonizing-flames") > 0 && DestructionPointsSpent < 16 ||
            Rank("pyroclasm") > 0 && (DestructionPointsSpent < 21 || Rank("intensity") < 3) ||
            Rank("shadow-and-flame") > 0 && DestructionPointsSpent < 26 ||
            Rank("fire-and-brimstone") > 0 && (DestructionPointsSpent < 21 || Rank("conflagrate") == 0) ||
            Rank("nightfall") > 0 && AfflictionPointsSpent < 16 ||
            Rank("conflagrate") > 0 && DestructionPointsSpent < 16 ||
            Rank("improved-bane-of-agony") > 0 && AfflictionPointsSpent < 11 ||
            Rank("decimation") > 0 && DemonologyPointsSpent < 16 ||
            Rank("demonic-brand") > 0 && DemonologyPointsSpent < 16 ||
            Rank("siphon-life") > 0 && AfflictionPointsSpent < 21 ||
            Rank("shadowburn") > 0 && DestructionPointsSpent < 11 ||
            Rank("wrack") > 0 && (AfflictionPointsSpent < 31 || Rank("siphon-life") == 0) ||
            Rank("demonic-pact") > 0 && (DemonologyPointsSpent < 31 || Rank("soul-link") == 0) ||
            Rank("soul-link") > 0 && (DemonologyPointsSpent < 21 || Rank("demonic-sacrifice") == 0) ||
            Rank("incinerate") > 0 && (DestructionPointsSpent < 31 || Rank("bane-of-havoc") == 0))
            throw new ArgumentException("A Warlock capstone is missing its required tier or prerequisite.");
    }

    public static ForeverWarlockTalentBuild Default(string spec) => spec switch
    {
        "affliction" => new() { AfflictionPointsSpent = 41, DemonologyPointsSpent = 10,
            Ranks = new(StringComparer.OrdinalIgnoreCase) {
                ["improved-corruption"] = 5, ["grim-reach"] = 2, ["suppression"] = 5,
                ["improved-life-tap"] = 2, ["improved-drains"] = 3, ["pandemic"] = 3,
                ["shadow-mastery"] = 5, ["malevolence"] = 5, ["wrack"] = 1,
                ["siphon-life"] = 1 } },
        "demonology" => new() { AfflictionPointsSpent = 10, DemonologyPointsSpent = 41,
            Ranks = new(StringComparer.OrdinalIgnoreCase) {
                ["unholy-power"] = 5, ["fel-vitality"] = 3,
                ["demonic-knowledge"] = 3, ["demonic-sacrifice"] = 1, ["soul-link"] = 1, ["demonic-pact"] = 1,
                ["master-demonologist"] = 5, ["demonic-brand"] = 3, ["decimation"] = 2,
                ["improved-life-tap"] = 2 } },
        "destruction" => new() { AfflictionPointsSpent = 10, DestructionPointsSpent = 41,
            Ranks = new(StringComparer.OrdinalIgnoreCase) {
                ["bane"] = 5, ["cataclysm"] = 3, ["ruin"] = 5,
                ["improved-shadow-bolt"] = 5, ["bane-of-havoc"] = 1,
                ["incinerate"] = 1, ["agonizing-flames"] = 3, ["improved-life-tap"] = 2,
                ["shadowburn"] = 1, ["conflagrate"] = 1 } },
        _ => throw new ArgumentException("Unknown Warlock DPS specialization.", nameof(spec))
    };
}
