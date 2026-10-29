namespace WarcraftSim.Data.Forever.Mage;

/// <summary>Subset of Forever Mage talents that CP132 actually models. Unmodeled
/// filler may be reflected by points spent, without granting phantom benefits.</summary>
public sealed class ForeverMageTalentBuild
{
    public int ArcanePointsSpent { get; set; }
    public int FirePointsSpent { get; set; }
    public int FrostPointsSpent { get; set; }
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, (string Tree, int Max)> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ["arcane-focus"] = ("arcane",5), ["arcane-impact"] = ("arcane",3),
        ["arcane-concentration"] = ("arcane",5), ["arcane-meditation"] = ("arcane",3),
        ["magic-absorption"] = ("arcane",2),
        ["improved-channeling"] = ("arcane",5),
        ["arcane-shielding"] = ("arcane",2),
        ["arcane-mind"] = ("arcane",5), ["arcane-instability"] = ("arcane",3),
        ["arcane-blast"] = ("arcane",1), ["missile-barrage"] = ("arcane",2), ["arcane-wrath"] = ("arcane",1),
        ["presence-of-mind"] = ("arcane",1), ["arcane-power"] = ("arcane",1),
        ["improved-fireball"] = ("fire",5), ["ignite"] = ("fire",5),
        ["incineration"] = ("fire",3), ["pyroblast"] = ("fire",1),
        ["heating-up"] = ("fire",1), ["fire-power"] = ("fire",3),
        ["critical-mass"] = ("fire",5), ["combustion"] = ("fire",1),
        ["improved-flamestrike"] = ("fire",3),
        ["master-of-elements"] = ("fire",3),
        ["improved-scorch"] = ("fire",3), ["blast-wave"] = ("fire",1),
        ["burning-soul"] = ("fire",3), ["improved-fire-blast"] = ("fire",2),
        ["elemental-precision"] = ("frost",3), ["ice-shards"] = ("frost",5),
        ["improved-frostbolt"] = ("frost",5),
        ["frost-channeling"] = ("frost",3),
        ["shatter"] = ("frost",3), ["ice-lance"] = ("frost",1),
        ["fingers-of-frost"] = ("frost",2), ["winters-chill"] = ("frost",5),
        ["improved-blizzard"] = ("frost",3), ["permafrost"] = ("frost",3),
        ["improved-cone-of-cold"] = ("frost",3),
        ["cold-snap"] = ("frost",1), ["ice-barrier"] = ("frost",1),
        ["ice-block"] = ("frost",1)
    };
    public int Rank(string key) => Ranks is not null && Ranks.TryGetValue(key, out var rank) ? rank : 0;
    public void Validate(string spec)
    {
        if (spec is not ("arcane" or "fire" or "frost") || Ranks is null ||
            ArcanePointsSpent < 0 || FirePointsSpent < 0 || FrostPointsSpent < 0 ||
            ArcanePointsSpent + FirePointsSpent + FrostPointsSpent > 51)
            throw new ArgumentException("Invalid level-60 Mage talent allocation (maximum 51 points).");
        var treeRanks = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, rank) in Ranks)
        {
            if (!Supported.TryGetValue(key, out var info) || rank < 0 || rank > info.Max)
                throw new ArgumentException($"Unsupported/invalid Mage talent: {key}={rank}.");
            treeRanks[info.Tree] = treeRanks.GetValueOrDefault(info.Tree) + rank;
        }
        if (treeRanks.GetValueOrDefault("arcane") > ArcanePointsSpent ||
            treeRanks.GetValueOrDefault("fire") > FirePointsSpent ||
            treeRanks.GetValueOrDefault("frost") > FrostPointsSpent)
            throw new ArgumentException("Mage modeled ranks exceed points spent in a tree.");
        if (Rank("arcane-shielding") > 0 && ArcanePointsSpent < 16)
            throw new ArgumentException("Arcane Shielding requires fifteen earlier Arcane points.");
        if (Rank("magic-absorption") > 0 && ArcanePointsSpent < 6)
            throw new ArgumentException("Magic Absorption requires five earlier Arcane points.");
        if (Rank("arcane-meditation") > 0 && Rank("arcane-concentration") == 0)
            throw new ArgumentException("Arcane Meditation requires Arcane Concentration.");
        if (Rank("presence-of-mind") > 0 && ArcanePointsSpent < 21)
            throw new ArgumentException("Presence of Mind requires 20 earlier Arcane points.");
        if (Rank("arcane-power") > 0 && (ArcanePointsSpent < 31 || Rank("presence-of-mind") == 0))
            throw new ArgumentException("Arcane Power requires Presence of Mind and 30 earlier Arcane points.");
        if (Rank("arcane-wrath") > 0 && (ArcanePointsSpent < 16 || Rank("arcane-blast") == 0))
            throw new ArgumentException("Arcane Wrath requires Arcane Blast and at least 16 Arcane points.");
        if (Rank("burning-soul") > 0 && FirePointsSpent < 11)
            throw new ArgumentException("Burning Soul requires ten earlier Fire points.");
        if (Rank("blast-wave") > 0 && FirePointsSpent < 21)
            throw new ArgumentException("Blast Wave requires 20 earlier Fire points.");
        if (Rank("improved-scorch") > 0 && FirePointsSpent < 16)
            throw new ArgumentException("Improved Scorch requires 15 earlier Fire points.");
        if (Rank("master-of-elements") > 0 && FirePointsSpent < 16)
            throw new ArgumentException("Master of Elements requires 15 earlier Fire points.");
        if (Rank("heating-up") > 0 && (FirePointsSpent < 21 || Rank("pyroblast") == 0))
            throw new ArgumentException("Heating Up requires Pyroblast and at least 21 Fire points.");
        if (Rank("combustion") > 0 && (FirePointsSpent < 31 || Rank("critical-mass") < 5))
            throw new ArgumentException("Combustion requires 5/5 Critical Mass and 31 Fire points.");
        if (Rank("improved-cone-of-cold") > 0 && FrostPointsSpent < 21)
            throw new ArgumentException("Improved Cone of Cold requires 20 earlier Frost points.");
        if (Rank("improved-blizzard") > 0 && FrostPointsSpent < 11)
            throw new ArgumentException("Improved Blizzard requires ten earlier Frost points.");
        if (Rank("permafrost") > 0 && FrostPointsSpent < 11)
            throw new ArgumentException("Permafrost requires ten earlier Frost points.");
        if (Rank("shatter") > 0 && FrostPointsSpent < 16)
            throw new ArgumentException("Shatter requires fifteen earlier Frost points.");
        if (Rank("ice-block") > 0 && FrostPointsSpent < 16)
            throw new ArgumentException("Ice Block requires 15 earlier Frost points.");
        if (Rank("ice-barrier") > 0 && (FrostPointsSpent < 31 || Rank("cold-snap") == 0))
            throw new ArgumentException("Ice Barrier requires Cold Snap and 30 earlier Frost points.");
        if (Rank("cold-snap") > 0 && FrostPointsSpent < 21)
            throw new ArgumentException("Cold Snap requires 20 other Frost points.");
        if (Rank("fingers-of-frost") > 0 && (FrostPointsSpent < 21 || Rank("ice-lance") == 0))
            throw new ArgumentException("Fingers of Frost requires Ice Lance and at least 21 Frost points.");
    }
    public static ForeverMageTalentBuild Default(string spec) => spec switch
    {
        "arcane" => new() { ArcanePointsSpent = 41, FirePointsSpent = 10,
            Ranks = new(StringComparer.OrdinalIgnoreCase) { ["arcane-focus"] = 5,
                ["arcane-impact"] = 3, ["arcane-concentration"] = 5,
                ["arcane-meditation"] = 3, ["arcane-mind"] = 5,
                ["arcane-instability"] = 3, ["arcane-blast"] = 1,
                ["missile-barrage"] = 2, ["arcane-wrath"] = 1,
                ["presence-of-mind"] = 1, ["arcane-power"] = 1, ["improved-fireball"] = 5 } },
        "fire" => new() { ArcanePointsSpent = 10, FirePointsSpent = 41,
            Ranks = new(StringComparer.OrdinalIgnoreCase) { ["improved-fireball"] = 5,
                ["ignite"] = 5, ["incineration"] = 2, ["pyroblast"] = 1,
                ["heating-up"] = 1, ["fire-power"] = 3, ["critical-mass"] = 5,
                ["combustion"] = 1 } },
        "frost" => new() { ArcanePointsSpent = 10, FrostPointsSpent = 41,
            Ranks = new(StringComparer.OrdinalIgnoreCase) { ["elemental-precision"] = 3,
                ["improved-frostbolt"] = 5,
                ["ice-shards"] = 5, ["frost-channeling"] = 3,
                ["shatter"] = 3, ["ice-lance"] = 1,
                ["fingers-of-frost"] = 2, ["winters-chill"] = 5,
                ["cold-snap"] = 1 } },
        _ => throw new ArgumentException("Unknown Mage specialization",nameof(spec))
    };
}
