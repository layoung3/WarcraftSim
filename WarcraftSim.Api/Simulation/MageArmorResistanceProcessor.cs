using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Current level-60 Mage Armor: +15 all magic school resistance, enhanced
/// by Arcane Shielding ranks 1/2 by 25/50%. The resistance stat is live,
/// but its conversion to a server-style resist distribution is provisional.
/// </summary>
public sealed class MageArmorResistanceProcessor(
    SimulationActorState mage,int arcaneShieldingRanks):ICombatEventProcessor
{
    private static readonly string[] Schools={"arcane","fire","frost","holy","nature","shadow"};
    private readonly Dictionary<string,decimal> _baseline=Schools.ToDictionary(
        school=>school,school=>mage.Stats.Get(school+"-resistance"),
        StringComparer.OrdinalIgnoreCase);
    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.TargetActorKey!=mage.Key || ev.AbilityKey!="mage-armor" ||
            ev.Type is not (CombatEventType.AuraApplied or CombatEventType.AuraRemoved))
            return;
        var present=mage.ActiveAuras.Any(a=>a.Definition.Key=="mage-armor" &&
            a.IsActiveAt(context.CurrentTimeSeconds));
        var res=present?15m*(1m+arcaneShieldingRanks*.25m):0m;
        foreach(var school in Schools)
            mage.Stats.Set(school+"-resistance",_baseline[school]+res);
    }
}
