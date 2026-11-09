using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Maps already-applied CP170-derived deltas into Hunter runtime aliases.
/// The equipment/racial processor owns ordinary stat application and expiry;
/// this callback must not apply primary-stat melee AP to ranged AP again.
/// </summary>
public static class ForeverEquipmentHunterDynamicStats
{
    private static readonly ForeverPrimaryPhysicalClassStatProfile Profile =
        ForeverPrimaryPhysicalStatProfiles.GetRequired("hunter");

    public static decimal RangedAttackPowerDelta(IReadOnlyDictionary<string,decimal> delta) =>
        delta.GetValueOrDefault("attack-power")+
        delta.GetValueOrDefault("ranged-attack-power")-
        delta.GetValueOrDefault("agility")*Profile.AttackPowerPerAgility-
        delta.GetValueOrDefault("strength")*Profile.AttackPowerPerStrength;

    public static void RefreshCompanionAttackPower(SimulationActorState hunter,bool rangedPrimary)
    {
        // Inherit the strongest eligible owner AP channel, not owner-only
        // Hawk/Beast AP that happens to be present when another buff refreshes.
        var ranged=hunter.Stats.Get(rangedPrimary?"attack-power":"hunter-ranged-attack-power")-
            hunter.Stats.Get(HunterAspectProcessor.OwnerOnlyRangedAttackPower);
        var melee=hunter.Stats.Get(rangedPrimary?"hunter-melee-attack-power":"attack-power")-
            hunter.Stats.Get(HunterAspectProcessor.OwnerOnlyMeleeAttackPower);
        hunter.Stats.Set("companion-attack-power",Math.Max(ranged,melee));
    }

    public static void Apply(SimulationContext context,SimulationActorState hunter,
        SimulationActorState? pet,ForeverBeastMasteryHunterTuning tuning,
        IReadOnlyDictionary<string,decimal> delta,bool rangedPrimary)
    {
        var ap=delta.GetValueOrDefault("attack-power");
        if(rangedPrimary)
        {
            // The processor already added ap to the primary runtime key.
            hunter.Stats.Add("attack-power",RangedAttackPowerDelta(delta)-ap);
            hunter.Stats.Add("hunter-melee-attack-power",
                ap+delta.GetValueOrDefault("melee-attack-power"));
        }
        else
        {
            hunter.Stats.Add("attack-power",delta.GetValueOrDefault("melee-attack-power"));
            hunter.Stats.Add("hunter-ranged-attack-power",RangedAttackPowerDelta(delta));
        }
        RefreshCompanionAttackPower(hunter,rangedPrimary);
        var crit=delta.GetValueOrDefault("attack-critical-chance-percent");
        hunter.Stats.Add("ranged-critical-chance-percent",crit);
        hunter.Stats.Add("melee-critical-chance-percent",crit);
        hunter.Stats.Add("ranged-hit-bonus-percent",
            delta.GetValueOrDefault("hit-chance-percent"));
        ForeverEquipmentDynamicResourceCaps.Apply(context,hunter,delta);
        if(pet is not null)
            HunterCompanionComposition.RefreshOwnerStats(context,pet,tuning);
    }
}
