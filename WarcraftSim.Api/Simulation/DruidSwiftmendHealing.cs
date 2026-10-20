using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever Swiftmend: full-duration healing of one of the caster's currently
/// active Rejuvenation/Regrowth HoTs; does not remove the selected HoT.
/// If both qualify, choose the larger full-duration value (configurable target
/// selection is a future encounter-UI consideration).
/// </summary>
public static class DruidSwiftmendHealing
{
    public static decimal FullDurationAmount(SimulationActorState caster,
        SimulationActorState target, decimal now)
    {
        var best = 0m;
        foreach (var aura in target.ActiveAuras)
        {
            if (!aura.IsActiveAt(now) ||
                !string.Equals(aura.SourceActorKey, caster.Key, StringComparison.OrdinalIgnoreCase) ||
                aura.Definition.Key is not ("rejuvenation" or "regrowth") ||
                !caster.Abilities.TryGetValue(aura.Definition.Key, out var spell)) continue;
            foreach (var effect in spell.Definition.Effects.Where(e =>
                e.EffectType == AbilityEffectTypes.PeriodicHealing &&
                string.Equals(e.AuraKey, aura.Definition.Key, StringComparison.OrdinalIgnoreCase) &&
                e.TickIntervalSeconds is >0m && e.DurationSeconds is >0m))
            {
                var ticks = Math.Floor(effect.DurationSeconds!.Value / effect.TickIntervalSeconds!.Value);
                best = Math.Max(best, effect.MinimumValue * ticks);
            }
        }
        return best;
    }
}
