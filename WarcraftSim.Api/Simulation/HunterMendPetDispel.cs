using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever Improved Mend Pet: cleanse one eligible hostile aura on each
/// effective healing tick. The existing aura manager owns removal and events.
/// Roll is supplied by the combat context so deterministic simulations and
/// exact boundary fixtures use one identical decision path.
/// </summary>
public static class HunterMendPetDispel
{
    private static readonly HashSet<string> EligibleTypes = new(
        ["curse", "disease", "magic", "poison"],StringComparer.OrdinalIgnoreCase);

    public static decimal Chance(int rank) => rank switch {
        0 => 0m, 1 => .15m, 2 => .50m,
        _ => throw new ArgumentOutOfRangeException(nameof(rank))
    };

    public static bool HasEligibleDebuff(SimulationContext context,SimulationActorState pet)
        => pet.ActiveAuras.Any(a=>IsEligible(context,pet,a));

    public static bool TryCleanse(SimulationContext context,SimulationActorState pet,
        AuraManager auras,int rank,decimal roll)
    {
        var chance=Chance(rank);
        if(chance==0m || !pet.IsAlive)return false;
        if(roll<0m || roll>=1m)
            throw new ArgumentOutOfRangeException(nameof(roll));
        var eligible=pet.ActiveAuras
            .Where(a=>IsEligible(context,pet,a))
            .OrderBy(a=>a.AppliedAtSeconds)
            .ThenBy(a=>a.Definition.Key,StringComparer.Ordinal)
            .FirstOrDefault();
        if(eligible is null || roll>=chance)return false;
        return auras.RemoveAuras(context,pet,eligible.Definition.Key,
            eligible.SourceActorKey,"improved-mend-pet")>0;
    }

    private static bool IsEligible(SimulationContext context,SimulationActorState pet,
        AuraInstance aura)
    {
        var source=context.GetActor(aura.SourceActorKey);
        return source is not null && source.TeamKey!=pet.TeamKey &&
            aura.IsActiveAt(context.CurrentTimeSeconds) &&
            aura.Definition.Tags.Any(tag=>
                tag.StartsWith("dispel:",StringComparison.OrdinalIgnoreCase) &&
                EligibleTypes.Contains(tag["dispel:".Length..]));
    }
}
