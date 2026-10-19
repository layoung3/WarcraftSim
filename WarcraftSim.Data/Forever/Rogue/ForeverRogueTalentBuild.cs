namespace WarcraftSim.Data.Forever.Rogue;

/// <summary>
/// CP142 supported, wired Rogue talent subset. This is deliberately NOT a complete
/// Forever talent tree; unsupported talent keys fail validation instead of
/// accepting allocated points whose effects are silently ignored.
/// </summary>
public sealed class ForeverRogueTalentBuild
{
    public int AssassinationPointsSpent { get; set; }
    public int CombatPointsSpent { get; set; }
    public int SubtletyPointsSpent { get; set; }
    public Dictionary<string, int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, (string Tree, int Cap)> Supported =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["malice"] = ("assassination", 5),
            ["ruthlessness"] = ("assassination", 3),
            ["improved-slice-and-dice"] = ("assassination", 3),
            ["vile-poisons"] = ("assassination", 5),
            ["improved-poisons"] = ("assassination", 5),
            ["vigor"] = ("assassination", 2),
            ["seal-fate"] = ("assassination", 5),
            ["mutilate"] = ("assassination", 1),
            ["venom"] = ("assassination", 1),
            ["improved-eviscerate"] = ("combat", 3),
            ["precision"] = ("combat", 5),
            ["dual-wield-specialization"] = ("combat", 5),
            ["aggression"] = ("combat", 3),
            ["blade-flurry"] = ("combat", 1),
            ["adrenaline-rush"] = ("combat", 1),
            ["opportunity"] = ("subtlety", 5),
            ["hemorrhage"] = ("subtlety", 1),
            ["thousand-cuts"] = ("subtlety", 1),
            ["cold-blood"] = ("assassination", 1),
            ["flawless-execution"] = ("combat", 1),
            ["ghostly-strike"] = ("subtlety", 1),
            ["premeditation"] = ("subtlety", 1),
            ["cutthroat"] = ("subtlety", 3),
            ["improved-ambush"] = ("subtlety", 3),
            ["puncturing-wounds"] = ("combat", 3),
            ["lethality"] = ("assassination", 5),
            ["improved-garrote"] = ("assassination", 3),
            ["improved-kidney-shot"] = ("assassination", 3),
            ["improved-expose-armor"] = ("assassination", 2),
            ["quick-recovery"] = ("assassination", 2),
            ["fleet-footed"] = ("assassination", 2),
            ["deflection"] = ("combat", 5),
            ["riposte"] = ("combat", 1),
            ["hack-and-slash"] = ("combat", 5),
            ["weapon-expertise"] = ("combat", 2),
            ["endurance"] = ("combat", 2),
            ["lightning-reflexes"] = ("combat", 5),
            ["elusiveness"] = ("subtlety", 2),
            ["master-of-subtlety"] = ("subtlety", 3),
            ["dirty-deeds"] = ("subtlety", 2),
            ["dirty-tricks"] = ("subtlety", 2),
            ["preparation"] = ("subtlety", 1),
            // Deadliness and the four legacy weapon-specialization nodes were
            // explicitly removed/replaced by Forever (official Rogue deep dive).
        };

    public int Rank(string key) => Ranks is not null && Ranks.TryGetValue(key, out var rank) ? rank : 0;

    public void Validate(int level, string specialization)
    {
        if (level != 60 || specialization is not ("assassination" or "combat" or "subtlety") ||
            Ranks is null || AssassinationPointsSpent < 0 || CombatPointsSpent < 0 ||
            SubtletyPointsSpent < 0 || AssassinationPointsSpent + CombatPointsSpent +
            SubtletyPointsSpent > 51)
            throw new ArgumentException("Rogue requires a valid level-60 51-point talent build.");
        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, rank) in Ranks)
        {
            if (!Supported.TryGetValue(key, out var info) || rank < 0 || rank > info.Cap)
                throw new ArgumentException($"Unsupported or invalid Rogue talent: {key}={rank}.");
            used[info.Tree] = used.GetValueOrDefault(info.Tree) + rank;
        }
        if (used.GetValueOrDefault("assassination") > AssassinationPointsSpent ||
            used.GetValueOrDefault("combat") > CombatPointsSpent ||
            used.GetValueOrDefault("subtlety") > SubtletyPointsSpent)
            throw new ArgumentException("Modeled Rogue ranks exceed their declared tree point totals.");
        if (Rank("mutilate") > 0 && AssassinationPointsSpent < 21 ||
            Rank("venom") > 0 && AssassinationPointsSpent < 31 ||
            Rank("blade-flurry") > 0 && CombatPointsSpent < 21 ||
            Rank("adrenaline-rush") > 0 && CombatPointsSpent < 31 ||
            Rank("hemorrhage") > 0 && SubtletyPointsSpent < 21 ||
            Rank("thousand-cuts") > 0 && SubtletyPointsSpent < 31 ||
            Rank("cold-blood") > 0 && AssassinationPointsSpent < 16 ||
            Rank("flawless-execution") > 0 && CombatPointsSpent < 16 ||
            Rank("premeditation") > 0 && SubtletyPointsSpent < 16 ||
            Rank("ghostly-strike") > 0 && SubtletyPointsSpent < 11 ||
            Rank("preparation") > 0 && SubtletyPointsSpent < 21 ||
            Rank("riposte") > 0 && CombatPointsSpent < 11)
            throw new ArgumentException("Rogue milestone talent has insufficient tree points.");
        if (Rank("riposte") > 0 && Rank("deflection") < 5)
            throw new ArgumentException("Riposte requires five ranks in Deflection.");
        if (Rank("venom") > 0 && Rank("mutilate") == 0)
            throw new ArgumentException("CP142 Assassination preset requires Mutilate before Venom.");
    }

    public static ForeverRogueTalentBuild Default(string spec) => spec switch
    {
        "assassination" => new() { AssassinationPointsSpent = 41, CombatPointsSpent = 10,
            Ranks = new(StringComparer.OrdinalIgnoreCase)
            { ["malice"] = 5, ["ruthlessness"] = 3, ["vile-poisons"] = 5,
              ["improved-poisons"] = 5, ["seal-fate"] = 5, ["mutilate"] = 1,
              ["venom"] = 1, ["vigor"] = 2, ["improved-slice-and-dice"] = 3,
              ["improved-eviscerate"] = 3, ["precision"] = 5 } },
        "combat" => new() { AssassinationPointsSpent = 10, CombatPointsSpent = 41,
            Ranks = new(StringComparer.OrdinalIgnoreCase)
            { ["malice"] = 5, ["improved-eviscerate"] = 3, ["precision"] = 5,
              ["dual-wield-specialization"] = 5, ["aggression"] = 3,
              ["blade-flurry"] = 1, ["adrenaline-rush"] = 1 } },
        "subtlety" => new() { AssassinationPointsSpent = 10, CombatPointsSpent = 0, SubtletyPointsSpent = 41,
            Ranks = new(StringComparer.OrdinalIgnoreCase)
            { ["malice"] = 5, ["opportunity"] = 5,
              ["hemorrhage"] = 1, ["thousand-cuts"] = 1 } },
        _ => throw new ArgumentException("Unknown Rogue specialization.", nameof(spec))
    };
}
