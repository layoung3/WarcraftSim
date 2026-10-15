namespace WarcraftSim.Data.Forever.Priest;

/// <summary>
/// Healing-only subset of Forever Priest talent validation. Points not in this
/// subset may be accounted for by per-tree point totals; this does not claim
/// to validate the complete 53-talent tree or every dependency.
/// </summary>
public sealed class ForeverPriestHealingTalentBuild
{
    public int DisciplinePointsSpent { get; set; }
    public int HolyPointsSpent { get; set; }
    public int ShadowPointsSpent { get; set; }
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // (tree, max ranks, points required in same tree before unlock)
    private static readonly Dictionary<string, (string Tree, int Max, int Tier)> Catalog = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["twin-disciplines"] = ("discipline", 5, 0),
        ["mental-agility"] = ("discipline", 3, 10),
        ["meditation"] = ("discipline", 3, 10),
        ["mental-strength"] = ("discipline", 5, 15),
        ["soul-warding"] = ("discipline", 1, 15),
        ["penance"] = ("discipline", 1, 20),
        ["divine-aegis"] = ("discipline", 3, 25),
        ["improved-renew"] = ("holy", 3, 0),
        ["improved-power-word-shield"] = ("discipline", 3, 5),
        ["divine-fury"] = ("holy", 5, 10),
        ["improved-healing"] = ("holy", 3, 15),
        ["binding-heal"] = ("holy", 1, 15),
        ["spiritual-guidance"] = ("holy", 5, 20),
        ["spiritual-healing"] = ("holy", 3, 25),
        ["litany-of-light"] = ("holy", 2, 20),
        ["prayer-of-mending"] = ("holy", 1, 30)
    };

    public int Rank(string key) => Ranks.TryGetValue(key, out var value) ? value : 0;

    public void Validate(int level = 60)
    {
        if (level is < 1 or > 60 || DisciplinePointsSpent < 0 || HolyPointsSpent < 0 ||
            ShadowPointsSpent < 0 ||
            DisciplinePointsSpent + HolyPointsSpent + ShadowPointsSpent > Math.Max(0, level - 9))
            throw new ArgumentException("Priest talent point totals exceed the level budget.");
        var disciplineSelected = 0;
        var holySelected = 0;
        foreach (var pair in Ranks)
        {
            if (!Catalog.TryGetValue(pair.Key, out var info) ||
                pair.Value < 0 || pair.Value > info.Max)
                throw new ArgumentException($"Unsupported Priest healing talent/rank: {pair.Key}.");
            int treeTotal = info.Tree == "discipline" ? DisciplinePointsSpent : HolyPointsSpent;
            if (pair.Value > 0 && treeTotal - pair.Value < info.Tier)
                throw new ArgumentException($"Talent {pair.Key} requires {info.Tier} earlier points in {info.Tree}.");
            if (info.Tree == "discipline") disciplineSelected += pair.Value;
            else holySelected += pair.Value;
        }
        if (disciplineSelected > DisciplinePointsSpent || holySelected > HolyPointsSpent)
            throw new ArgumentException("Selected talent ranks exceed declared tree points.");
    }
}
