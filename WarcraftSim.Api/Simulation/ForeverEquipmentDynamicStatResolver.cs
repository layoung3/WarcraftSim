using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Simulation;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Reuses CP170's exact equipment + derived-stat contributor ordering for
/// temporary raw stats. This is a DELTA-only, empty-equipment calculation:
/// permanent equipment has already been applied once by the normal resolver.
/// </summary>
public static class ForeverEquipmentDynamicStatResolver
{
    public static IReadOnlyDictionary<string,decimal> ResolveDelta(
        string classKey,int level,IReadOnlyDictionary<string,decimal> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if(raw.Count==0)return new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
        if(string.IsNullOrWhiteSpace(classKey) || level is <1 or >60)
            throw new ArgumentException("Temporary stat derivation needs a supported class and level.");
        if(raw.Any(x=>string.IsNullOrWhiteSpace(x.Key)))
            throw new ArgumentException("Temporary modifier keys must not be empty.");

        var profile=new CharacterProfile {
            ClassKey=classKey,Level=level,
            BaseStats=new StatCollection{Values=new Dictionary<string,decimal>(
                raw,StringComparer.OrdinalIgnoreCase)}
        };
        // Compute a zero-source baseline too. This protects against class
        // contributors adding constant stats not attributable to the effect.
        var calculator=ForeverCharacterSimulationRuntimeCalculatorFactory
            .CreateStandard(_=>1m);
        var empty=calculator.Calculate(new CharacterProfile{
            ClassKey=classKey,Level=level
        }).EffectiveStats;
        var filled=calculator.Calculate(profile).EffectiveStats;
        return filled.Values.Keys.Concat(empty.Values.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key=>(key,amount:filled.Get(key)-empty.Get(key)))
            .Where(x=>x.amount!=0m)
            .ToDictionary(x=>x.key,x=>x.amount,StringComparer.OrdinalIgnoreCase);
    }
}
