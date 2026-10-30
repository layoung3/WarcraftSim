using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Maps CP170-derived temporary raw-stat deltas into the existing Hunter
/// runtime stat aliases. The shared effect processor never implements
/// specialization-specific proc branches. Pet AP/armor uses the preexisting
/// owner-inheritance coefficients; crit/hit are read live by pet roll rules.
/// </summary>
public static class ForeverEquipmentHunterDynamicStats
{
    public static void Apply(SimulationContext context,SimulationActorState hunter,
        SimulationActorState? pet,ForeverBeastMasteryHunterTuning tuning,
        IReadOnlyDictionary<string,decimal> delta,bool rangedPrimary)
    {
        var ap=delta.GetValueOrDefault("attack-power");
        var ranged=delta.GetValueOrDefault("ranged-attack-power");
        if(rangedPrimary)
        {
            hunter.Stats.Add("attack-power",ranged);
            hunter.Stats.Add("hunter-melee-attack-power",ap);
        }
        else
        {
            hunter.Stats.Add("hunter-ranged-attack-power",ap+ranged);
        }
        hunter.Stats.Add("companion-attack-power",ap+ranged);
        var crit=delta.GetValueOrDefault("attack-critical-chance-percent");
        hunter.Stats.Add("ranged-critical-chance-percent",crit);
        hunter.Stats.Add("melee-critical-chance-percent",crit);
        hunter.Stats.Add("ranged-hit-bonus-percent",
            delta.GetValueOrDefault("hit-chance-percent"));
        ForeverEquipmentDynamicResourceCaps.Apply(context,hunter,delta);
        // Do not infer a pet haste/inheritance multiplier from owner haste.
        // HunterCompanionComposition already owns live AP/armor inheritance.
        if(pet is not null)
            HunterCompanionComposition.RefreshOwnerStats(context,pet,tuning);
    }
}
