using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Shares CP170's existing Forever derived-stat conversion rules for selected
/// precombat consumables/external buffs; only the current Hunter runner tuning
/// adapters differ. Empty selections preserve legacy numeric-only mode.
/// </summary>
public static class ForeverExternalHunterStatAdapter
{
    private static T Copy<T>(T original) where T:class =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(original)) ??
        throw new InvalidOperationException("Hunter external-modifier request clone failed.");

    private static IReadOnlyDictionary<string,decimal> Resolve(
        ForeverExternalModifierSelection selection)
    {
        var raw=ForeverExternalModifierCatalog.SumSelfRawStats(selection,60);
        return ForeverEquipmentDynamicStatResolver.ResolveDelta("hunter",60,raw);
    }

    public static MarksmanshipHunterSimRequest Apply(MarksmanshipHunterSimRequest input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var d=Resolve(input.ExternalModifiers);
        if(d.Count==0)return input;
        var result=Copy(input);
        var t=result.Tuning;
        t.RangedAttackPower+=d.GetValueOrDefault("attack-power")+
            d.GetValueOrDefault("ranged-attack-power");
        t.Intellect+=d.GetValueOrDefault("intellect");
        t.CriticalChancePercent+=d.GetValueOrDefault("attack-critical-chance-percent");
        t.HitChancePercent+=d.GetValueOrDefault("hit-chance-percent");
        t.MaximumHealth+=d.GetValueOrDefault("health")+10m*d.GetValueOrDefault("stamina");
        t.MaximumMana+=d.GetValueOrDefault("mana")+15m*d.GetValueOrDefault("intellect");
        result.PetTuning.HunterArmor+=d.GetValueOrDefault("armor");
        result.PetTuning.HunterStamina+=d.GetValueOrDefault("stamina");
        result.PetTuning.HunterRangedHitBonusPercent+=d.GetValueOrDefault("hit-chance-percent");
        result.PetTuning.HunterMeleeCriticalPercent+=d.GetValueOrDefault("attack-critical-chance-percent");
        return result;
    }

    public static SurvivalHunterSimRequest Apply(SurvivalHunterSimRequest input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var d=Resolve(input.ExternalModifiers);
        if(d.Count==0)return input;
        var result=Copy(input);
        var t=result.Tuning;
        t.MeleeAttackPower+=d.GetValueOrDefault("attack-power")+
            d.GetValueOrDefault("melee-attack-power");
        t.RangedAttackPower+=d.GetValueOrDefault("attack-power")+
            d.GetValueOrDefault("ranged-attack-power");
        t.Agility+=d.GetValueOrDefault("agility");
        t.CriticalChancePercent+=d.GetValueOrDefault("attack-critical-chance-percent");
        t.HitChancePercent+=d.GetValueOrDefault("hit-chance-percent");
        t.MaximumHealth+=d.GetValueOrDefault("health")+10m*d.GetValueOrDefault("stamina");
        t.MaximumMana+=d.GetValueOrDefault("mana")+15m*d.GetValueOrDefault("intellect");
        result.PetTuning.HunterArmor+=d.GetValueOrDefault("armor");
        result.PetTuning.HunterStamina+=d.GetValueOrDefault("stamina");
        result.PetTuning.HunterRangedHitBonusPercent+=d.GetValueOrDefault("hit-chance-percent");
        result.PetTuning.HunterMeleeCriticalPercent+=d.GetValueOrDefault("attack-critical-chance-percent");
        return result;
    }

    public static BeastMasteryHunterSimRequest Apply(BeastMasteryHunterSimRequest input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var d=Resolve(input.ExternalModifiers);
        if(d.Count==0)return input;
        var result=Copy(input);
        var t=result.Tuning;
        t.MeleeAttackPower+=d.GetValueOrDefault("attack-power")+
            d.GetValueOrDefault("melee-attack-power");
        t.RangedAttackPower+=d.GetValueOrDefault("attack-power")+
            d.GetValueOrDefault("ranged-attack-power");
        t.HunterCriticalPercent+=d.GetValueOrDefault("attack-critical-chance-percent");
        t.HunterMeleeCriticalPercent+=d.GetValueOrDefault("attack-critical-chance-percent");
        t.HunterHitPercent+=d.GetValueOrDefault("hit-chance-percent");
        t.HunterHealth+=d.GetValueOrDefault("health")+10m*d.GetValueOrDefault("stamina");
        t.HunterMana+=d.GetValueOrDefault("mana")+15m*d.GetValueOrDefault("intellect");
        t.HunterArmor+=d.GetValueOrDefault("armor");
        t.HunterStamina+=d.GetValueOrDefault("stamina");
        t.HunterRangedHitBonusPercent+=d.GetValueOrDefault("hit-chance-percent");
        return result;
    }
}
