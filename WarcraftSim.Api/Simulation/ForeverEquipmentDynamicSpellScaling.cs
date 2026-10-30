using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Converts only demonstrably linear, already snapshotted Spell Power
/// contributions into Core's EXISTING live scaling-stat mechanism. This is
/// opt-in for a timed Spell Power equipment effect; no baseline rotations or
/// numeric-only simulations change without that effect equipped.
/// </summary>
public static class ForeverEquipmentDynamicSpellScaling
{
    public static void Enable(
        IList<AbilityDefinition> equipped,
        IEnumerable<AbilityDefinition> atZeroSpellPower,
        IEnumerable<AbilityDefinition> atOneSpellPower,
        decimal baseSpellPower,
        StatCollection liveActorStats)
    {
        var zero=atZeroSpellPower.ToDictionary(x=>x.Key,StringComparer.OrdinalIgnoreCase);
        var one=atOneSpellPower.ToDictionary(x=>x.Key,StringComparer.OrdinalIgnoreCase);
        foreach(var ability in equipped)
        {
            if(!zero.TryGetValue(ability.Key,out var bare) ||
               !one.TryGetValue(ability.Key,out var unit))continue;
            foreach(var effect in ability.Effects)
            {
                // Until Forever's snapshot/dynamic tick behavior is certified,
                // never rewrite DoT/HoT tick coefficients to live scaling.
                if(effect.EffectType is not (
                    AbilityEffectTypes.DirectDamage or AbilityEffectTypes.DirectHealing))
                    continue;
                if(effect.ScalingStatKey is not null || effect.ScalingCoefficient!=0m)
                    continue;
                var e0=bare.Effects.FirstOrDefault(x=>x.Key==effect.Key);
                var e1=unit.Effects.FirstOrDefault(x=>x.Key==effect.Key);
                if(e0 is null||e1 is null)continue;
                var deltaMin=e1.MinimumValue-e0.MinimumValue;
                var deltaMax=e1.MaximumValue-e0.MaximumValue;
                // Nonlinear/custom mechanics stay untouched rather than
                // guessing how to recalculate them at impact.
                if(deltaMin<=0m || deltaMin!=deltaMax ||
                   effect.MinimumValue != e0.MinimumValue+deltaMin*baseSpellPower ||
                   effect.MaximumValue != e0.MaximumValue+deltaMax*baseSpellPower)continue;
                effect.MinimumValue=e0.MinimumValue;
                effect.MaximumValue=e0.MaximumValue;
                effect.ScalingStatKey="spell-power";
                effect.ScalingCoefficient=deltaMin;
            }
        }
        liveActorStats.Set("spell-power",baseSpellPower);
    }
}
