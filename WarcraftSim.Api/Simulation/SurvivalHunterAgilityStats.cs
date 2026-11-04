using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Survival-only Lightning Reflexes transformation. Approved Forever 70707
/// level-60 hunter physical profile: 1 melee AP, 2 ranged AP per Agility,
/// 52.9 Agility per 1% critical chance, 2 Armor per Agility.
/// Dodge is server-derived and has no approved client conversion; a nullable
/// per-encounter 1%-dodge coefficient is deliberately opt-in, never inferred.
/// Baseline tuning already includes untalented Agility-derived statistics:
/// only the TALENT's incremental Agility is added here.
/// </summary>
public static class SurvivalHunterAgilityStats
{
    private static readonly ForeverPrimaryPhysicalClassStatProfile Profile =
        ForeverPrimaryPhysicalStatProfiles.GetRequired("hunter");
    private static readonly decimal AgilityPerCrit =
        ForeverPrimaryPhysicalStatProfiles.GetAgilityPerCriticalPercent("hunter",60);

    public static decimal TalentBonus(decimal agility,int ranks)
    {
        if(agility<0m || ranks is <0 or >5)
            throw new ArgumentOutOfRangeException(nameof(ranks));
        return agility*.03m*ranks;
    }

    public static decimal CritBonus(decimal agility,int ranks) =>
        TalentBonus(agility,ranks)/AgilityPerCrit;

    public static decimal? DodgeBonus(decimal agility,int ranks,decimal? perPercent) =>
        perPercent is >0m ? TalentBonus(agility,ranks)/perPercent : null;

    public static void Initialize(StatCollection stats,ForeverSurvivalHunterTuning tune,
        int ranks)
    {
        var extra=TalentBonus(tune.Agility,ranks);
        stats.Set("agility",tune.Agility+extra);
        stats.Set("hunter-effective-agility",tune.Agility+extra);
        stats.Set(ForeverCombatStatKeys.AttackPower,
            tune.MeleeAttackPower+extra*Profile.AttackPowerPerAgility);
        stats.Set("hunter-ranged-attack-power",
            tune.RangedAttackPower+extra*Profile.RangedAttackPowerPerAgility);
        stats.Set("companion-attack-power",
            Math.Max(tune.MeleeAttackPower+extra*Profile.AttackPowerPerAgility,
                tune.RangedAttackPower+extra*Profile.RangedAttackPowerPerAgility));
        stats.Set("melee-critical-chance-percent",
            tune.CriticalChancePercent+extra/AgilityPerCrit);
        stats.Set("ranged-critical-chance-percent",
            tune.CriticalChancePercent+extra/AgilityPerCrit);
        if(tune.AgilityPerDodgePercentAt60 is { } dodge)
            stats.Set("hunter-dodge-chance-percent",extra/dodge);
    }

    public static void ApplyRuntimeDelta(SimulationContext context,
        SimulationActorState hunter,SimulationActorState? pet,
        ForeverBeastMasteryHunterTuning petTuning,
        ForeverSurvivalHunterTuning survivalTuning,int ranks,
        IReadOnlyDictionary<string,decimal> delta)
    {
        // The shared Hunter derivation already applies RAW Agility's ordinary
        // AP/crit/armor contributions. Only the EXTRA 3% per rank is new.
        ForeverEquipmentHunterDynamicStats.Apply(context,hunter,pet,
            petTuning,delta,rangedPrimary:false);
        var raw=delta.GetValueOrDefault("agility");
        // The shared rangedPrimary:false adapter does not currently update
        // melee AP. It also aggregates derived +1 melee AP/Agility into
        // ranged AP even though that same Agility contributes a separate
        // +2 ranged AP. Correct just this Survival callback to prevent
        // double counting and retain generic AP alongside equipment Agility.
        hunter.Stats.Add(ForeverCombatStatKeys.AttackPower,
            delta.GetValueOrDefault("attack-power")+
            delta.GetValueOrDefault("melee-attack-power"));
        var ordinaryMeleeFromAgility=raw*Profile.AttackPowerPerAgility;
        hunter.Stats.Add("hunter-ranged-attack-power",-ordinaryMeleeFromAgility);
        hunter.Stats.Add("companion-attack-power",-ordinaryMeleeFromAgility);
        var extra=raw*.03m*ranks;
        hunter.Stats.Add("agility",raw+extra);
        hunter.Stats.Add("hunter-effective-agility",raw+extra);
        hunter.Stats.Add(ForeverCombatStatKeys.AttackPower,
            extra*Profile.AttackPowerPerAgility);
        hunter.Stats.Add("hunter-ranged-attack-power",
            extra*Profile.RangedAttackPowerPerAgility);
        hunter.Stats.Add("companion-attack-power",
            extra*Profile.RangedAttackPowerPerAgility);
        hunter.Stats.Add("ranged-critical-chance-percent",extra/AgilityPerCrit);
        hunter.Stats.Add("melee-critical-chance-percent",extra/AgilityPerCrit);
        hunter.Stats.Add("armor",delta.GetValueOrDefault("armor")+
            extra*ForeverPrimaryPhysicalStatProfiles.ArmorPerAgility);
        if(survivalTuning.AgilityPerDodgePercentAt60 is { } dodge)
            hunter.Stats.Add("hunter-dodge-chance-percent",extra/dodge);
        if(pet is not null)
            HunterCompanionComposition.RefreshOwnerStats(context,pet,petTuning);
    }
}
