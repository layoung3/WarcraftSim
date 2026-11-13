using System.Text.Json;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Api.Simulation;

/// <summary>Isolate Enhancement inputs without changing dictionary lookup semantics.</summary>
public static class EnhancementShamanSimulationRequestSnapshot
{
    public static EnhancementShamanSimRequest Create(EnhancementShamanSimRequest source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateGraph(source);
        var copy=JsonSerializer.Deserialize<EnhancementShamanSimRequest>(JsonSerializer.Serialize(source))!;
        copy.Talents.Ranks=new(source.Talents.Ranks,source.Talents.Ranks.Comparer);
        CasterSimulationRequestSnapshot.PreserveStatComparers(source.Character,copy.Character);
        return copy;
    }

    // Reject malformed nested JSON before cloning or entering the shared contributors.
    private static void ValidateGraph(EnhancementShamanSimRequest request)
    {
        var p=request.Character;
        if(p is null || request.Talents?.Ranks is null || request.Tuning is null ||
           request.CombatItems is null || request.CallOfElementsTotems is null ||
           request.CallOfAncestorsTotems is null || request.CallOfSpiritsTotems is null ||
           p.Equipment?.Items is null ||
           p.Talents?.Selections is null || p.Buffs?.Selections is null)
            throw new ArgumentException("Enhancement requires complete character, talents, tuning, combat items and totem lists.");
        static void Stats(StatCollection? stats)
        {
            if(stats?.Values is null)throw new ArgumentException("Character stat maps cannot be null.");
        }
        Stats(p.BaseStats);
        foreach(var item in p.Equipment.Items)
        {
            if(item is null || item.Modifiers is null)
                throw new ArgumentException("Equipment entries and modifier lists cannot be null.");
            Stats(item.Stats);
            foreach(var modifier in item.Modifiers)
            {
                if(modifier is null)throw new ArgumentException("Equipment modifiers cannot be null.");
                Stats(modifier.Stats);
            }
        }
        foreach(var talent in p.Talents.Selections)
        {
            if(talent is null)throw new ArgumentException("Character talent selections cannot be null.");
            Stats(talent.Stats);
        }
        foreach(var buff in p.Buffs.Selections)
        {
            if(buff is null)throw new ArgumentException("Character buff selections cannot be null.");
            Stats(buff.Stats);
        }
    }
}
