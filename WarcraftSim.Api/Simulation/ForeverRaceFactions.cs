namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Shared race-to-faction mapping for server-side equipment restrictions.
/// An unknown/unverified race has no inferred faction and cannot bypass a
/// faction-locked item. Expand this mapping with verified Forever races.
/// </summary>
public static class ForeverRaceFactions
{
    public static string? GetFaction(string? race) => race?.Trim().ToLowerInvariant() switch
    {
        "human" or "dwarf" or "night-elf" or "gnome" or "high-order-skyborne" => "alliance",
        "orc" or "undead" or "tauren" or "troll" or "windshaper-skyborne" => "horde",
        _ => null
    };
}
