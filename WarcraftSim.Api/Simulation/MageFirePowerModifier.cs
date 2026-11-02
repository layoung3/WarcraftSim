using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Client 70707 source: Ignite, Improved Scorch, Fireball, Pyroblast
/// and Impact are the only Fire Power effects that count on a target.
/// Frostfire Bolt benefits but cannot contribute; Ignite's already boosted
/// critical pool is never multiplied again on reactive ticks.
/// https://wowfdb.com/talents/mage/
/// </summary>
public static class MageFirePowerModifier
{
    private static readonly string[] Eligible=[
        "mage-ignite","mage-improved-scorch-vulnerability",
        "fireball-dot","pyroblast-dot","mage-impact-stun"
    ];
    public static decimal Multiplier(SimulationActorState target,string owner,
        string abilityKey,int ranks,decimal at)
    {
        if(ranks is <0 or >3)throw new ArgumentOutOfRangeException(nameof(ranks));
        if(ranks==0 || abilityKey=="mage-ignite")return 1m;
        var effects=Eligible.Count(key=>target.ActiveAuras.Any(a=>
            a.Definition.Key.Equals(key,StringComparison.OrdinalIgnoreCase) &&
            a.SourceActorKey.Equals(owner,StringComparison.OrdinalIgnoreCase) &&
            a.IsActiveAt(at)));
        return 1m+Math.Min(4,effects)*ranks*.01m;
    }
}
