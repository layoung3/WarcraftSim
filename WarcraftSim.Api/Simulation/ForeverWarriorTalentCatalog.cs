namespace WarcraftSim.Api.Simulation;

// Tree metadata is versioned independently of the sim engine. Some talents are
// selectable for planning but do not modify combat until their runtime effect
// is implemented; these are disclosed in the response and calculator.
public sealed record WarriorTalentDefinition(string Id, string Name, string Tree, int MaxRank,
    int Row, int Column, string? PrerequisiteId, string SimulationStatus, string Description);

public static class ForeverWarriorTalentCatalog
{
    public const string Version = "forever-beta-1.60.1.70338-preview";
    public static IReadOnlyList<WarriorTalentDefinition> Talents { get; } =
    [
        new("improved-heroic-strike", "Improved Heroic Strike", "arms", 3, 1, 1, null, "modeled", "Reduces Heroic Strike Rage cost by 1 per rank."),
        new("deflection", "Deflection", "arms", 5, 1, 2, null, "modeled", "Grants 1% parry chance per rank against incoming melee."),
        new("improved-rend", "Improved Rend", "arms", 3, 1, 3, null, "modeled", "Increases Rend bleed damage by 12/23/35%."),
        new("improved-charge", "Improved Charge", "arms", 2, 2, 1, null, "not-modeled", "Increases Charge Rage."),
        new("improved-tactical-mastery", "Improved Tactical Mastery", "arms", 5, 2, 2, null, "modeled", "Extra 3 Rage retained per rank when automated stance dancing is enabled; baseline retained Rage 10."),
        new("improved-overpower", "Improved Overpower", "arms", 2, 2, 3, null, "modeled", "Increases Overpower critical chance by 25% per rank."),
        new("anger-management", "Anger Management", "arms", 1, 3, 1, "improved-tactical-mastery", "partial", "Gain 1 Rage every 3 seconds in combat (out-of-combat retention not modeled)."),
        new("deep-wounds", "Deep Wounds", "arms", 3, 3, 3, "improved-rend", "modeled", "Critical strikes apply a rolling bleed."),
        new("spearing-strike", "Spearing Strike", "arms", 1, 4, 1, null, "modeled", "Battle Stance weapon strike; additional damage to specific creature types."),
        new("two-handed-weapon-specialization", "Two-Handed Weapon Specialization", "arms", 3, 4, 2, null, "partial", "Preview: 1% physical damage per rank when two-handing; weapon-specific delivery still pending."),
        new("impale", "Impale", "arms", 2, 4, 3, "deep-wounds", "modeled", "Improved critical damage of abilities; requires 3/3 Deep Wounds."),
        new("bloodthrill", "Bloodthrill", "arms", 5, 5, 1, null, "modeled", "Your Rend-afflicted targets can enable Overpower on main-hand melee hits."),
        new("sweeping-strikes", "Sweeping Strikes", "arms", 1, 5, 2, null, "partial", "Your next five melee strikes also hit another target."),
        new("weaponmaster", "Weaponmaster", "arms", 5, 5, 3, null, "partial", "Different effects for swords, axes, polearms, maces, and staves."),
        new("improved-slam", "Improved Slam", "arms", 2, 6, 1, null, "modeled", "Reduces Slam cast/GCD by 0.25s per point and cooldown by 3s; no longer suspends weapon swings."),
        new("improved-hamstring", "Improved Hamstring", "arms", 3, 6, 3, null, "not-modeled", "Hamstring can immobilize the target."),
        new("mortal-strike", "Mortal Strike", "arms", 1, 7, 2, "sweeping-strikes", "modeled", "Unlocks Mortal Strike."),
        new("booming-voice", "Booming Voice", "fury", 5, 1, 1, null, "partial", "Shouts cover a wider area and cost less Rage."),
        new("cruelty", "Cruelty", "fury", 5, 1, 3, null, "modeled", "+1% melee critical chance per rank."),
        new("lingering-rage", "Lingering Rage", "fury", 5, 2, 1, null, "not-modeled", "Delays Rage decay after leaving combat."),
        new("unbridled-wrath", "Unbridled Wrath", "fury", 5, 2, 3, null, "partial", "Chance for 1 Rage on weapon-damage hits; other combat modes pending."),
        new("furious-precision", "Furious Precision", "fury", 3, 3, 1, null, "modeled", "4/7/10% additional off-hand hit chance."),
        new("piercing-howl", "Piercing Howl", "fury", 1, 3, 2, null, "not-modeled", "AoE slow."),
        new("blood-craze", "Blood Craze", "fury", 3, 3, 3, null, "partial", "Healing after taking certain damage."),
        new("dual-wield-specialization", "Dual Wield Specialization", "fury", 5, 4, 1, null, "partial", "+5% off-hand damage and +10% white-hit Rage per rank (queued replacement interactions pending)."),
        new("raging-blows", "Raging Blows", "fury", 1, 4, 2, null, "modeled", "Reduces Cleave/Whirlwind cost by 3 Rage."),
        new("enrage", "Enrage", "fury", 5, 4, 3, null, "partial", "Physical damage bonus after receiving damage."),
        new("improved-execute", "Improved Execute", "fury", 2, 4, 4, null, "modeled", "Reduces Execute Rage cost."),
        new("improved-berserker-rage", "Improved Berserker Rage", "fury", 2, 5, 1, null, "not-modeled", "Berserker Rage grants Rage."),
        new("death-wish", "Death Wish", "fury", 1, 5, 2, null, "partial", "30s physical damage buff; 3 min cooldown."),
        new("improved-intercept", "Improved Intercept", "fury", 2, 5, 4, null, "not-modeled", "Reduces Intercept cooldown."),
        new("flurry", "Flurry", "fury", 5, 6, 2, "death-wish", "modeled", "Attack speed increased after melee crit; requires Death Wish."),
        new("gore-drinker", "Gore Drinker", "fury", 2, 6, 3, "enrage", "partial", "Healing from following melee attacks; requires Enrage."),
        new("bloodthirst", "Bloodthirst", "fury", 1, 7, 2, null, "modeled", "Unlocks Bloodthirst."),
        new("improved-bloodrage", "Improved Bloodrage", "protection", 2, 1, 1, null, "modeled", "Increases Rage from Bloodrage."),
        new("shield-specialization", "Shield Specialization", "protection", 5, 1, 2, null, "modeled", "Block chance and Rage when blocking."),
        new("iron-will", "Iron Will", "protection", 5, 1, 3, null, "not-modeled", "Reduces stun and fear duration."),
        new("anticipation", "Anticipation", "protection", 5, 2, 1, null, "modeled", "Increases Defense skill."),
        new("improved-revenge", "Improved Revenge", "protection", 3, 2, 2, null, "modeled", "Increases Revenge damage."),
        new("improved-thunder-clap", "Improved Thunder Clap", "protection", 3, 2, 4, null, "partial", "Reduces Thunder Clap cost."),
        new("last-stand", "Last Stand", "protection", 1, 3, 1, null, "modeled", "Temporary maximum health increase."),
        new("master-of-defense", "Master of Defense", "protection", 2, 3, 2, "shield-specialization", "modeled", "Generate Rage when avoiding attacks with a shield."),
        new("improved-disarm", "Improved Disarm", "protection", 3, 3, 3, null, "not-modeled", "Reduces Disarm cooldown."),
        new("defiance", "Defiance", "protection", 3, 3, 4, null, "modeled", "Defensive Stance shield threat modifier."),
        new("improved-sunder-armor", "Improved Sunder Armor", "protection", 3, 4, 1, null, "modeled", "Reduces Sunder Armor cost."),
        new("vanguard", "Vanguard", "protection", 1, 4, 2, null, "not-modeled", "Charge becomes available in Defensive Stance."),
        new("improved-shield-bash", "Improved Shield Bash", "protection", 2, 4, 4, null, "not-modeled", "Improves Shield Bash silence chance."),
        new("improved-shield-wall", "Improved Shield Wall", "protection", 2, 5, 1, null, "modeled", "Reduces Shield Wall cooldown."),
        new("concussion-blow", "Concussion Blow", "protection", 1, 5, 2, null, "partial", "Stuns the target."),
        new("focused-rage", "Focused Rage", "protection", 3, 5, 3, null, "partial", "Reduces modeled Protection offensive Rage costs; other Warrior action costs remain under review."),
        new("bastion", "Bastion", "protection", 5, 6, 2, null, "partial", "Modeled as physical damage +2% per rank with a shield; nonphysical damage and exact delivery are not verified."),
        new("shield-slam", "Shield Slam", "protection", 1, 7, 2, "concussion-blow", "partial", "Unlocks Shield Slam: 421–439 damage plus Block Value. Additional threat and dispel remain unverified."),
    ];

    private static readonly Dictionary<string, WarriorTalentDefinition> ById =
        Talents.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

    public static int Rank(WarriorSimRequest request, string id) =>
        request.TalentMode == "tree" && request.TalentRanks.TryGetValue(id, out var value) ? value : 0;

    public static void Validate(WarriorSimRequest request)
    {
        if (request.TalentMode == "legacy") return;
        if (request.TalentMode != "tree" || request.TalentRanks is null)
            throw new ArgumentException("Talent mode must be legacy or tree, and ranks must be provided.");
        var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        { ["arms"] = 0, ["fury"] = 0, ["protection"] = 0 };
        foreach (var (id, rank) in request.TalentRanks)
        {
            if (!ById.TryGetValue(id, out var talent) || rank < 0 || rank > talent.MaxRank)
                throw new ArgumentException($"Unknown or invalid Warrior talent: {id} ({rank}).");
            totals[talent.Tree] += rank;
        }
        if (totals.Values.Sum() > Math.Max(0, request.Level - 9))
            throw new ArgumentException("Too many talent points for your level.");
        foreach (var talent in Talents)
        {
            var rank = Rank(request, talent.Id);
            if (rank == 0) continue;
            var earlier = Talents.Where(x => x.Tree == talent.Tree && x.Row < talent.Row)
                .Sum(x => Rank(request, x.Id));
            if (earlier < (talent.Row - 1) * 5)
                throw new ArgumentException($"Talent {talent.Name} requires {(talent.Row - 1) * 5} points in earlier {talent.Tree} tiers.");
            if (talent.PrerequisiteId is not null &&
                Rank(request, talent.PrerequisiteId) != ById[talent.PrerequisiteId].MaxRank)
                throw new ArgumentException($"Talent {talent.Name} requires max rank of {ById[talent.PrerequisiteId].Name}.");
        }
    }

    public static List<string> UnmodeledSelections(WarriorSimRequest request) =>
        request.TalentMode != "tree" ? [] : Talents
            .Where(x => Rank(request, x.Id) > 0 && x.SimulationStatus != "modeled")
            .Select(x => x.SimulationStatus == "partial"
                ? $"TALENT PARTIALLY SIMULATED: {x.Name} ({Rank(request, x.Id)}/{x.MaxRank})"
                : $"TALENT NOT YET SIMULATED: {x.Name} ({Rank(request, x.Id)}/{x.MaxRank})")
            .ToList();
}
