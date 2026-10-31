using System.Text.Json;
using WarcraftSim.Core.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Opt-in replacement of illustrative level-60 Hunter tuning with an actual
/// provenance-labelled naked Hunter observation BEFORE the existing CP170
/// equipment and CP173 external modifier bridges. Not a second gear resolver.
/// Weapon damage/timings still come from selected catalog equipment.
/// </summary>
public static class ForeverLevel60HunterObservationAdapter
{
    private static T Clone<T>(T source) where T:class =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source)) ??
        throw new InvalidOperationException("Hunter baseline request clone failed.");

    private static ForeverLevel60BaselineObservation? Verified(
        ForeverLevel60BaselineObservation? baseline,string? race,bool useReviewed,bool useProvisional)
    {
        baseline=ForeverLevel60Baseline.Resolve(baseline,useReviewed,race??"","hunter",useProvisional);
        if(baseline is null)return null;
        ForeverLevel60Baseline.Validate(baseline);
        if(baseline.ClassKey!="hunter" || !string.Equals(baseline.Race,race,
           StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Hunter baseline race/class mismatch.");
        if(baseline.ObservedUnbuffedMaximumHealth is null ||
           baseline.ObservedUnbuffedMaximumMana is null ||
           baseline.ObservedUnbuffedRangedAttackPower is null ||
           baseline.ObservedUnbuffedMeleeAttackPower is null ||
           baseline.ObservedUnbuffedArmor is null)
            throw new ArgumentException("Hunter combat baseline requires unbuffed HP, Mana, melee/ranged AP and Armor observations. No default numeric tuning is silently injected.");
        return baseline;
    }

    private static decimal BaseAgilityCrit(ForeverLevel60BaselineObservation baseline)=>
        baseline.Agility/ForeverPrimaryPhysicalStatProfiles
            .GetAgilityPerCriticalPercent("hunter",60);

    public static MarksmanshipHunterSimRequest Apply(MarksmanshipHunterSimRequest source)
    {
        var b=Verified(source.Level60Baseline,source.Race,source.UseReviewedLevel60Baseline,
            source.UseProvisionalLevel60Baseline);
        if(b is null)return source;
        var r=Clone(source);
        r.Tuning.Intellect=b.Intellect;
        r.Tuning.CriticalChancePercent=BaseAgilityCrit(b);
        r.Tuning.RangedAttackPower=b.ObservedUnbuffedRangedAttackPower!.Value;
        r.Tuning.MaximumMana=b.ObservedUnbuffedMaximumMana!.Value;
        r.Tuning.MaximumHealth=b.ObservedUnbuffedMaximumHealth!.Value;
        r.PetTuning.HunterArmor=b.ObservedUnbuffedArmor!.Value;
        r.PetTuning.HunterStamina=b.Stamina;
        return r;
    }

    public static SurvivalHunterSimRequest Apply(SurvivalHunterSimRequest source)
    {
        var b=Verified(source.Level60Baseline,source.Race,source.UseReviewedLevel60Baseline,
            source.UseProvisionalLevel60Baseline);
        if(b is null)return source;
        var r=Clone(source);
        r.Tuning.Agility=b.Agility;
        r.Tuning.CriticalChancePercent=BaseAgilityCrit(b);
        r.Tuning.MeleeAttackPower=b.ObservedUnbuffedMeleeAttackPower!.Value;
        r.Tuning.RangedAttackPower=b.ObservedUnbuffedRangedAttackPower!.Value;
        r.Tuning.MaximumMana=b.ObservedUnbuffedMaximumMana!.Value;
        r.Tuning.MaximumHealth=b.ObservedUnbuffedMaximumHealth!.Value;
        r.PetTuning.HunterArmor=b.ObservedUnbuffedArmor!.Value;
        r.PetTuning.HunterStamina=b.Stamina;
        return r;
    }

    public static BeastMasteryHunterSimRequest Apply(BeastMasteryHunterSimRequest source)
    {
        var b=Verified(source.Level60Baseline,source.Race,source.UseReviewedLevel60Baseline,
            source.UseProvisionalLevel60Baseline);
        if(b is null)return source;
        var r=Clone(source);
        r.Tuning.MeleeAttackPower=b.ObservedUnbuffedMeleeAttackPower!.Value;
        r.Tuning.RangedAttackPower=b.ObservedUnbuffedRangedAttackPower!.Value;
        r.Tuning.HunterCriticalPercent=BaseAgilityCrit(b);
        r.Tuning.HunterMeleeCriticalPercent=BaseAgilityCrit(b);
        r.Tuning.HunterStamina=b.Stamina;
        r.Tuning.HunterArmor=b.ObservedUnbuffedArmor!.Value;
        r.Tuning.HunterMana=b.ObservedUnbuffedMaximumMana!.Value;
        r.Tuning.HunterHealth=b.ObservedUnbuffedMaximumHealth!.Value;
        return r;
    }
}
