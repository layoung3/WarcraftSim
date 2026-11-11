namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>Modeled CP130 Enhancement talents. Tree totals include optional
/// noncombat/unmodeled filler talents; they are not silently granted effects.</summary>
public sealed class ForeverEnhancementShamanTalents
{
    public int ElementalPointsSpent { get; set; } = 10;
    public int EnhancementPointsSpent { get; set; } = 41;
    public int RestorationPointsSpent { get; set; }
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["thundering-strikes"] = 5, ["mental-dexterity"] = 3,
        ["elemental-weapons"] = 3, ["shamanistic-focus"] = 1,
        ["flurry"] = 5, ["stormstrike"] = 1,
        ["mental-quickness"] = 2, ["improved-stormstrike"] = 2,
        ["maelstrom-weapon"] = 5, ["rage-of-the-farseer"] = 1
    };
    public int Rank(string key) => Ranks.TryGetValue(key, out var value) ? value : 0;
    // Historical tree source, not current-client certification:
    // gunba/wow-forever-sim @ 1cc5ae1e9abdbf941b7326d2d90421822dce31ba,
    // ui/core/talents/trees/shaman.json. The two opt-in early talents are
    // Ancestral Knowledge (2/4/6/8/10% Intellect) and Improved Lightning
    // Shield (5/10/15% orb damage). Legacy defaults above remain unchanged.
    private static readonly Dictionary<string, int> Max = new(StringComparer.OrdinalIgnoreCase)
    {
        ["thundering-strikes"] = 5, ["mental-dexterity"] = 3,
        ["elemental-weapons"] = 3, ["shamanistic-focus"] = 1,
        ["flurry"] = 5, ["stormstrike"] = 1,
        ["mental-quickness"] = 2, ["improved-stormstrike"] = 2,
        ["maelstrom-weapon"] = 5, ["rage-of-the-farseer"] = 1,
        ["ancestral-knowledge"] = 5, ["improved-lightning-shield"] = 3,
        ["convection"] = 5, ["concussion"] = 5, ["reverberation"] = 5
    };
    public static IReadOnlyList<(string Key, string Tree, int MaxRank)> SupportedTalentMetadata =>
        Max.Select(x => (x.Key, Tree(x.Key), x.Value)).ToArray();

    private static string Tree(string key) =>
        key.Equals("convection", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("concussion", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("reverberation", StringComparison.OrdinalIgnoreCase) ? "elemental" : "enhancement";

    public void Validate(int level)
    {
        var budget = Math.Max(0, level - 9);
        if (ElementalPointsSpent < 0 || EnhancementPointsSpent < 0 || RestorationPointsSpent < 0 ||
            ElementalPointsSpent + EnhancementPointsSpent + RestorationPointsSpent > budget)
            throw new ArgumentException("Enhancement talent tree points exceed the level budget.");
        foreach (var (key, rank) in Ranks)
            if (!Max.TryGetValue(key, out var max) || rank < 0 || rank > max)
                throw new ArgumentException($"Invalid or unmodeled Enhancement talent '{key}' rank {rank}.");
        // Typed legacy requests use aggregate capacity, not strict itemized equality.
        // Historical row gates and exact named totals remain in the strict validator.
        if (Ranks.Where(x => Tree(x.Key) == "enhancement").Sum(x => x.Value) > EnhancementPointsSpent ||
            Ranks.Where(x => Tree(x.Key) == "elemental").Sum(x => x.Value) > ElementalPointsSpent)
            throw new ArgumentException("Modeled Enhancement/Elemental ranks exceed their assigned tree points.");
        if (Rank("improved-stormstrike") > 0 && Rank("stormstrike") == 0 ||
            Rank("rage-of-the-farseer") > 0 && Rank("mental-quickness") < 2)
            throw new ArgumentException("Improved Stormstrike requires Stormstrike and Rage of the Farseer requires Mental Quickness 2/2.");
        if (Rank("stormstrike") > 0 && EnhancementPointsSpent < 16 ||
            Rank("maelstrom-weapon") > 0 && EnhancementPointsSpent < 26 ||
            Rank("rage-of-the-farseer") > 0 && EnhancementPointsSpent < 31)
            throw new ArgumentException("A selected Enhancement talent lacks its tree-tier point budget.");
    }
}
