namespace WarcraftSim.Api.Simulation;

// Forever beta has not established exact level-60 base stats.
// Classic Era warrior values are offered as a clearly labeled preview.
public sealed record WarriorRaceBaseStats(string Id, string Name, decimal Strength, decimal Agility,
    decimal Stamina, decimal Intellect, decimal Spirit, bool IsVerifiedForever);

public static class ForeverWarriorRaceProfiles
{
    public const string SourceUrl = "https://wow-guides.com/knowledge/attributes";
    public const string Version = "classic-era-level60-proxy-not-forever-verified";
    public static IReadOnlyList<WarriorRaceBaseStats> Races { get; } =
    [
        new("human", "Human", 120m, 80m, 110m, 30m, 45m, false),
        new("dwarf", "Dwarf", 122m, 76m, 113m, 29m, 44m, false),
        new("night-elf", "Night Elf", 117m, 85m, 109m, 30m, 45m, false),
        new("gnome", "Gnome", 115m, 83m, 109m, 35m, 45m, false),
        new("orc", "Orc", 123m, 77m, 112m, 27m, 48m, false),
        new("undead", "Undead", 119m, 78m, 111m, 28m, 50m, false),
        new("tauren", "Tauren", 125m, 75m, 112m, 25m, 47m, false),
        new("troll", "Troll", 121m, 82m, 111m, 26m, 46m, false),
        new("skyborne", "Skyborne (unverified projection)", 119m, 81m, 109m, 31m, 45m, false),
    ];
    public static WarriorRaceBaseStats Get(string id) =>
        Races.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown Warrior race: {id}");
    public static void Validate(WarriorSimRequest request)
    {
        if (request.StatSourceMode == "manual") return;
        if (request.StatSourceMode != "race-base")
            throw new ArgumentException("StatSourceMode must be manual or race-base.");
        if (request.Level != 60) throw new ArgumentException("Race-based preview is level 60 only.");
        _ = Get(request.Race);
    }
}
