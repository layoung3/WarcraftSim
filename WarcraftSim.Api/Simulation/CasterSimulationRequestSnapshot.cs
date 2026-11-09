using System.Text.Json;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Api.Simulation;

/// <summary>Per-run caster inputs with isolated mutable values and unchanged key lookup semantics.</summary>
public static class CasterSimulationRequestSnapshot
{
    public static MageSimRequest Create(MageSimRequest source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy=JsonSerializer.Deserialize<MageSimRequest>(JsonSerializer.Serialize(source))!;
        if(source.Talents?.Ranks is { } ranks)
            copy.Talents.Ranks=new(ranks,ranks.Comparer);
        PreserveStatComparers(source.Character,copy.Character);
        return copy;
    }

    public static WarlockSimRequest Create(WarlockSimRequest source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy=JsonSerializer.Deserialize<WarlockSimRequest>(JsonSerializer.Serialize(source))!;
        if(source.Talents?.Ranks is { } ranks)
            copy.Talents.Ranks=new(ranks,ranks.Comparer);
        if(source.AfflictionAdditionalTargetPriority is { } affliction)
            copy.AfflictionAdditionalTargetPriority=new(copy.AfflictionAdditionalTargetPriority,affliction.Comparer);
        if(source.DestructionAdditionalTargetPriority is { } destruction)
            copy.DestructionAdditionalTargetPriority=new(copy.DestructionAdditionalTargetPriority,destruction.Comparer);
        PreserveStatComparers(source.Character,copy.Character);
        return copy;
    }

    public static BalanceDruidSimRequest Create(BalanceDruidSimRequest source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy=JsonSerializer.Deserialize<BalanceDruidSimRequest>(JsonSerializer.Serialize(source))!;
        if(source.Talents?.Ranks is { } ranks)
            copy.Talents.Ranks=new(ranks,ranks.Comparer);
        PreserveStatComparers(source.Character,copy.Character);
        return copy;
    }

    public static ElementalShamanSimRequest Create(ElementalShamanSimRequest source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy=JsonSerializer.Deserialize<ElementalShamanSimRequest>(JsonSerializer.Serialize(source))!;
        if(source.Talents?.Ranks is { } ranks)
            copy.Talents.Ranks=new(ranks,ranks.Comparer);
        PreserveStatComparers(source.Character,copy.Character);
        return copy;
    }

    // Generate intentionally replaces base attributes, but its JSON clone must
    // retain lookup semantics for all unchanged equipment, talent and buff maps.
    internal static CharacterProfile GenerateBaseline(CharacterProfile source,
        ForeverLevel60BaselineObservation baseline)
    {
        var copy=ForeverLevel60Baseline.Generate(source,baseline);
        PreserveStatComparers(source,copy,includeBaseStats:false);
        return copy;
    }

    // JSON isolates the whole mutable request graph, including target-priority
    // lists, but cannot round-trip Dictionary.Comparer. Restore each input map's
    // original comparer without normalizing keys, replacing nulls, or sharing
    // mutable data with the caller. Validation remains the runner's responsibility.
    internal static void PreserveStatComparers(CharacterProfile? source,CharacterProfile? copy,
        bool includeBaseStats=true,bool includeEquipment=true)
    {
        if(source is null || copy is null)return;
        if(includeBaseStats)Preserve(source.BaseStats,copy.BaseStats);
        if(includeEquipment && source.Equipment?.Items is { } items)
            for(var i=0;i<items.Count;i++)
            {
                var item=items[i];
                var cloned=copy.Equipment.Items[i];
                if(item is null || cloned is null)continue;
                Preserve(item.Stats,cloned.Stats);
                if(item.Modifiers is { } modifiers)
                    for(var j=0;j<modifiers.Count;j++)
                        Preserve(modifiers[j]?.Stats,cloned.Modifiers[j]?.Stats);
            }
        if(source.Talents?.Selections is { } talents)
            for(var i=0;i<talents.Count;i++)
                Preserve(talents[i]?.Stats,copy.Talents.Selections[i]?.Stats);
        if(source.Buffs?.Selections is { } buffs)
            for(var i=0;i<buffs.Count;i++)
                Preserve(buffs[i]?.Stats,copy.Buffs.Selections[i]?.Stats);
    }

    private static void Preserve(StatCollection? source,StatCollection? copy)
    {
        if(source?.Values is { } values && copy is not null)
            copy.Values=new(values,values.Comparer);
    }
}
