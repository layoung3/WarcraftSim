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
        ["unholy-power"] = ("demonology", 5), ["demonic-sacrifice"] = ("demonology", 1), ["soul-link"] = ("demonology", 1), ["fel-vitality"] = ("demonology", 3),
        ["demonic-knowledge"] = ("demonology", 3), ["demonic-pact"] = ("demonology", 1),
        ["master-demonologist"] = ("demonology", 5),
        ["demonic-brand"] = ("demonology", 3), ["decimation"] = ("demonology", 2),
        ["improved-felhunter"] = ("demonology", 3),
        ["improved-imp"] = ("demonology", 3), ["improved-sayaad"] = ("demonology", 3),
        ["bane"] = ("destruction", 5), ["shadowburn"] = ("destruction",1),
        ["conflagrate"] = ("destruction",1), ["cataclysm"] = ("destruction", 3),
        ["ruin"] = ("destruction", 5), ["improved-shadow-bolt"] = ("destruction", 5),
        ["incinerate"] = ("destruction", 1), ["bane-of-havoc"] = ("destruction", 1),
        ["agonizing-flames"] = ("destruction", 3), ["shadow-and-flame"] = ("destruction", 5)
    };
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
        if (Rank("nightfall") > 0 && AfflictionPointsSpent < 16 ||
            Rank("conflagrate") > 0 && DestructionPointsSpent < 16 ||
            Rank("improved-bane-of-agony") > 0 && AfflictionPointsSpent < 11 ||
            Rank("decimation") > 0 && DemonologyPointsSpent < 16 ||
            Rank("demonic-brand") > 0 && DemonologyPointsSpent < 16 ||
            Rank("siphon-life") > 0 && AfflictionPointsSpent < 21 ||
            Rank("shadowburn") > 0 && DestructionPointsSpent < 11 ||
            Rank("wrack") > 0 && AfflictionPointsSpent < 31 ||
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
