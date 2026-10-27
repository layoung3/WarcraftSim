namespace WarcraftSim.Data.Forever.Priest;

/// <summary>
/// CP129 explicit Shadow talent subset. Totals include filler talents we have
/// not modeled mechanically; every mechanically modeled rank is validated.
/// </summary>
public sealed class ForeverPriestShadowTalentBuild
{
    public int DisciplinePointsSpent { get; set; } = 10;
    public int HolyPointsSpent { get; set; }
    public int ShadowPointsSpent { get; set; } = 41;
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shadow-focus"] = 5,
        ["spirit-tap"] = 5,
        ["improved-shadow-word-pain"] = 2,
        ["shadow-reach"] = 2,
        ["mind-flay"] = 1,
        ["improved-mind-flay"] = 2,
        ["improved-mind-blast"] = 5,
        ["shadow-weaving"] = 3,
        ["vampiric-embrace"] = 1,
        ["devouring-contagion"] = 2,
        ["early-demise"] = 2,
        ["darkness"] = 5,
        ["shadowform"] = 1
    };

    private static readonly Dictionary<string, int> MaxRanks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["shadow-focus"] = 5, ["spirit-tap"] = 5,
        ["improved-shadow-word-pain"] = 2, ["shadow-reach"] = 2,
        ["mind-flay"] = 1,
        ["improved-mind-flay"] = 2, ["improved-mind-blast"] = 5,
        ["shadow-weaving"] = 3, ["vampiric-embrace"] = 1,
        ["devouring-contagion"] = 2, ["early-demise"] = 2,
        ["darkness"] = 5, ["shadowform"] = 1
    };

    public int Rank(string key) => Ranks.TryGetValue(key, out var value) ? value : 0;

    public void Validate(int level)
    {
        if (level is < 10 or > 60) throw new ArgumentException("Shadow talent preview requires level 10–60.");
        if (DisciplinePointsSpent < 0 || HolyPointsSpent < 0 || ShadowPointsSpent < 0 ||
            DisciplinePointsSpent + HolyPointsSpent + ShadowPointsSpent > level - 9)
            throw new ArgumentException("Shadow talent totals exceed the character level budget.");
        foreach (var (key, ranks) in Ranks)
            if (!MaxRanks.TryGetValue(key, out var cap) || ranks < 0 || ranks > cap)
                throw new ArgumentException($"Invalid or unmodeled Shadow talent rank: {key}={ranks}.");
        if (Ranks.Values.Sum() > ShadowPointsSpent)
            throw new ArgumentException("Allocated Shadow ranks exceed points spent in Shadow.");
        if (Rank("mind-flay") > 0 && ShadowPointsSpent < 11 ||
            Rank("improved-mind-flay") > 0 && Rank("mind-flay") == 0 ||
            Rank("vampiric-embrace") > 0 && ShadowPointsSpent < 16 ||
            Rank("shadowform") > 0 && ShadowPointsSpent < 31 ||
            Rank("early-demise") > 0 && ShadowPointsSpent < 26)
            throw new ArgumentException("Shadow talent prerequisite or tree-point milestone is not met.");
    }
}
