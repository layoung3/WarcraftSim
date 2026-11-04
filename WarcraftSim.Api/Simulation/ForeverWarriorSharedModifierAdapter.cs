using WarcraftSim.Core.Characters;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Compatibility boundary for the legacy Warrior support identifiers. The
/// source-labelled CP173 catalog remains authoritative for new shared effects.
/// This does not replicate modifier values or duration tables.
/// </summary>
public static class ForeverWarriorSharedModifierAdapter
{
    // Same effect or item, represented under historical Warrior identifiers.
    // Groups are validated using the shared catalog, rather than duplicated.
    private static readonly Dictionary<string,string> LegacyToShared =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["battle-shout"]="battle-shout",
            ["blessing-of-might"]="blessing-of-might",
            ["elixir-of-the-mongoose"]="elixir-of-the-mongoose",
            ["elixir-of-giants"]="elixir-of-greater-strength",
            ["faerie-fire"]="faerie-fire-rank4"
        };

    public static IReadOnlyList<ForeverExternalModifierDefinition> Resolve(WarriorSimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(request.ExternalModifiers is null)
            throw new ArgumentException("Shared external modifiers cannot be null.");
        var selected=ForeverExternalModifierCatalog.Resolve(
            request.ExternalModifiers,request.Level,request.DurationSeconds);
        var keys=selected.Select(x=>x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groups=selected.Where(x=>!string.IsNullOrWhiteSpace(x.ExclusiveGroup))
            .ToDictionary(x=>x.ExclusiveGroup,x=>x.Name,StringComparer.OrdinalIgnoreCase);
        foreach(var id in request.RaidBuffIds.Concat(request.ConsumableIds)
                     .Concat(request.BossDebuffIds))
        {
            if(!LegacyToShared.TryGetValue(id,out var common))continue;
            var legacyEquivalent=ForeverExternalModifierCatalog.Find(common);
            if(keys.Contains(common))
                throw new ArgumentException(
                    $"Shared modifier '{common}' duplicates prepared legacy Warrior effect '{id}'.");
            if(!string.IsNullOrWhiteSpace(legacyEquivalent.ExclusiveGroup) &&
               groups.TryGetValue(legacyEquivalent.ExclusiveGroup,out var conflicting))
                throw new ArgumentException(
                    $"Legacy Warrior modifier '{id}' conflicts with shared '{conflicting}' ({legacyEquivalent.ExclusiveGroup}).");
        }
        if(keys.Contains("battle-shout") && request.MaintenancePolicies.Any(x=>
            string.Equals(x.AbilityKey,"battle-shout",StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("External Battle Shout conflicts with the Warrior's assigned Battle Shout upkeep.");
        return selected;
    }

    public static CharacterProfile ApplyToProfile(CharacterProfile profile,WarriorSimRequest request)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var chosen=Resolve(request);
        if(chosen.Count==0)return profile;
        var result=ForeverExternalModifierCatalog.ApplyToProfile(
            profile,request.ExternalModifiers,request.DurationSeconds);
        // This is a class-key adapter, not a second buff formula: Warrior's
        // existing physical stats use attack-power while other adapters
        // distinguish melee-attack-power from ranged AP.
        foreach(var buff in result.Buffs.Selections.Where(x=>x.BuffKey.StartsWith(
            "cp173-",StringComparison.OrdinalIgnoreCase)))
        {
            if(buff.Stats.Values.Remove("melee-attack-power",out var meleeAp))
                buff.Stats.Add("attack-power",meleeAp);
        }
        return result;
    }
}
