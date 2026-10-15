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
        ["arcane-mind"] = ("arcane",5), ["arcane-instability"] = ("arcane",3),
        ["arcane-blast"] = ("arcane",1), ["missile-barrage"] = ("arcane",1),
        ["improved-fireball"] = ("fire",5), ["ignite"] = ("fire",5),
        ["incineration"] = ("fire",2), ["pyroblast"] = ("fire",1),
        ["heating-up"] = ("fire",1), ["fire-power"] = ("fire",5),
        ["critical-mass"] = ("fire",3),
        ["elemental-precision"] = ("frost",3), ["ice-shards"] = ("frost",5),
        ["frost-channeling"] = ("frost",3), ["piercing-ice"] = ("frost",3),
        ["shatter"] = ("frost",5), ["ice-lance"] = ("frost",1),
        ["fingers-of-frost"] = ("frost",2), ["winters-chill"] = ("frost",5)
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
        if (Rank("missile-barrage") > 0 && (ArcanePointsSpent < 16 || Rank("arcane-blast") == 0))
            throw new ArgumentException("Missile Barrage requires an Arcane Blast build with at least 16 Arcane points.");
        if (Rank("heating-up") > 0 && (FirePointsSpent < 21 || Rank("pyroblast") == 0))
            throw new ArgumentException("Heating Up requires Pyroblast and at least 21 Fire points.");
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
                ["missile-barrage"] = 1, ["improved-fireball"] = 5 } },
        "fire" => new() { ArcanePointsSpent = 10, FirePointsSpent = 41,
            Ranks = new(StringComparer.OrdinalIgnoreCase) { ["improved-fireball"] = 5,
                ["ignite"] = 5, ["incineration"] = 2, ["pyroblast"] = 1,
                ["heating-up"] = 1, ["fire-power"] = 5, ["critical-mass"] = 3 } },
        "frost" => new() { ArcanePointsSpent = 10, FrostPointsSpent = 41,
            Ranks = new(StringComparer.OrdinalIgnoreCase) { ["elemental-precision"] = 3,
                ["ice-shards"] = 5, ["frost-channeling"] = 3,
                ["piercing-ice"] = 3, ["shatter"] = 5, ["ice-lance"] = 1,
                ["fingers-of-frost"] = 2, ["winters-chill"] = 5 } },
        _ => throw new ArgumentException("Unknown Mage specialization",nameof(spec))
    };
}
