using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Forever 70425 Warlock damage interactions, evaluated when damage lands.</summary>
public static class Warlock70425DamageModifiers
{
    public static decimal DemonicKnowledgeSpellPower(int rank, int level, bool petSummoned)
    {
        if (!petSummoned) return 0m;
        return Math.Max(0, level) * (Math.Clamp(rank, 0, 3) switch
        { 1 => .33m, 2 => .67m, 3 => 1m, _ => 0m });
    }

    public static decimal Multiplier(string sourceKey, string abilityKey, string? school,
        CombatEffectDeliveryType delivery, bool hasOwnImmolate, bool hasOwnWrack,
        string petType, int masterRanks, bool hasSoulLink, int agonyRanks)
    {
        bool pet = sourceKey is "warlock-pet" or "warlock-replacement";
        decimal result = hasSoulLink ? 1.03m : 1m;
        if (!pet)
        {
            if (abilityKey == "incinerate" && hasOwnImmolate) result *= 1.25m;
            if (hasOwnWrack && school == "shadow" &&
                delivery == CombatEffectDeliveryType.Periodic &&
                abilityKey is ("corruption" or "bane-of-agony" or
                    "bane-of-doom" or "siphon-life"))
                result *= 1.10m;
            if (abilityKey is "shadow-bolt" or "immolate" or "incinerate" or
                "conflagrate" or "soul-fire" or "searing-pain" or
                "shadowburn" or "rain-of-fire" or "hellfire")
                result *= 1m + (Math.Clamp(agonyRanks,0,3) switch
                    { 1 => .03m, 2 => .07m, 3 => .10m, _ => 0m });
        }
        if (masterRanks > 0 &&
            ((petType == "imp" && school == "fire") ||
             ((petType == "succubus" || petType == "incubus") && school == "shadow")))
            result *= 1m + Math.Clamp(masterRanks,0,5) * .02m;
        return result;
    }
}
