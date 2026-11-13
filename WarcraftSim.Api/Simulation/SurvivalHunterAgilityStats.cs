using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Shared Lightning Reflexes transformation, independent of starting preset. Approved Forever 70707
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

    /// <summary>Display-only increment on the shared character calculator's
    /// ordinary stat keys. The ready profile remains untalented for combat.</summary>
    public static decimal ApplyPreview(StatCollection stats,int ranks)
    {
        var extra=TalentBonus(stats.Get("agility"),ranks);
        stats.Add("agility",extra);
        stats.Add("attack-power",extra*Profile.AttackPowerPerAgility);
        stats.Add("ranged-attack-power",extra*Profile.RangedAttackPowerPerAgility);
        stats.Add("attack-critical-chance-percent",extra/AgilityPerCrit);
        stats.Add("armor",extra*ForeverPrimaryPhysicalStatProfiles.ArmorPerAgility);
        return extra;
    }

    /// <summary>Add only the learned Agility increment to a ranged-primary
    /// owner's initialized untalented stats. No base Agility is inferred.</summary>
    public static void InitializeRanged(StatCollection stats,decimal agility,int ranks)
    {
        var extra=TalentBonus(agility,ranks);
        stats.Set("agility",agility+extra);
        stats.Set("hunter-effective-agility",agility+extra);
        stats.Add("attack-power",extra*Profile.RangedAttackPowerPerAgility);
        stats.Add("hunter-melee-attack-power",extra*Profile.AttackPowerPerAgility);
        stats.Set("companion-attack-power",Math.Max(stats.Get("attack-power"),
            stats.Get("hunter-melee-attack-power")));
        stats.Add("ranged-critical-chance-percent",extra/AgilityPerCrit);
        stats.Add("melee-critical-chance-percent",extra/AgilityPerCrit);
        stats.Add("armor",extra*ForeverPrimaryPhysicalStatProfiles.ArmorPerAgility);
    }

    public static void ApplyRuntimeDelta(SimulationContext context,
        SimulationActorState hunter,SimulationActorState? pet,
        ForeverBeastMasteryHunterTuning petTuning,
        ForeverSurvivalHunterTuning survivalTuning,int ranks,
        IReadOnlyDictionary<string,decimal> delta) =>
        ApplyRuntimeDelta(context,hunter,pet,petTuning,ranks,delta,
            rangedPrimary:false,survivalTuning.AgilityPerDodgePercentAt60);

    /// <summary>The equipment/racial processor has already applied ordinary
    /// derived deltas. Add only Lightning Reflexes and update live aliases.</summary>
    public static void ApplyRuntimeDelta(SimulationContext context,
        SimulationActorState hunter,SimulationActorState? pet,
        ForeverBeastMasteryHunterTuning petTuning,int ranks,
        IReadOnlyDictionary<string,decimal> delta,bool rangedPrimary,
        decimal? agilityPerDodgePercent=null)
    {
        ForeverEquipmentHunterDynamicStats.Apply(context,hunter,null,
            petTuning,delta,rangedPrimary);
        var raw=delta.GetValueOrDefault("agility");
        var extra=raw*.03m*ranks;
        hunter.Stats.Add("agility",extra);
        hunter.Stats.Add("hunter-effective-agility",raw+extra);
        hunter.Stats.Add(rangedPrimary?"hunter-melee-attack-power":"attack-power",
            extra*Profile.AttackPowerPerAgility);
        hunter.Stats.Add(rangedPrimary?"attack-power":"hunter-ranged-attack-power",
            extra*Profile.RangedAttackPowerPerAgility);
        hunter.Stats.Add("ranged-critical-chance-percent",extra/AgilityPerCrit);
        hunter.Stats.Add("melee-critical-chance-percent",extra/AgilityPerCrit);
        hunter.Stats.Add("armor",extra*ForeverPrimaryPhysicalStatProfiles.ArmorPerAgility);
        if(agilityPerDodgePercent is { } dodge)
            hunter.Stats.Add("hunter-dodge-chance-percent",extra/dodge);
        ForeverEquipmentHunterDynamicStats.RefreshCompanionAttackPower(hunter,rangedPrimary);
        if(pet is not null)
            HunterCompanionComposition.RefreshOwnerStats(context,pet,petTuning);
    }
}
