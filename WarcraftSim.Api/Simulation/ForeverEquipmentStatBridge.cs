using System.Text.Json;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Equipment;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Adapter only: Core owns stat contribution ordering; this service resolves
/// server-owned catalog selections, then applies additive equipment deltas to
/// existing per-class tuning. Original request objects are never mutated.
/// </summary>
public static class ForeverEquipmentStatBridge
{
    public static CharacterProfile ResolveProfile(CharacterProfile source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.Equipment is null)
            throw new ArgumentException("Character equipment cannot be null.");
        if(source.Equipment.Items.Count==0)return source;
        var normalized=ForeverSharedEquipmentResolver.Resolve(
            source.Equipment,source.ClassKey,source.Level);
        var profile=JsonSerializer.Deserialize<CharacterProfile>(
            JsonSerializer.Serialize(source)) ??
            throw new InvalidOperationException("Character profile copy failed.");
        profile.Equipment=normalized.Loadout;
        return profile;
    }

    public static MarksmanshipHunterSimRequest Resolve(MarksmanshipHunterSimRequest original)
    {
        if(original.Equipment?.Items.Count is not >0)return original;
        var result=ForeverSharedEquipmentResolver.Resolve(original.Equipment,"hunter");
        var request=Clone(original);
        var t=request.Tuning;
        var stats=result.Stats;
        t.RangedAttackPower+=stats.Get("attack-power")+stats.Get("ranged-attack-power");
        t.Intellect+=stats.Get("intellect");
        t.CriticalChancePercent+=stats.Get("attack-critical-chance-percent");
        t.HitChancePercent+=stats.Get("hit-chance-percent");
        t.MaximumMana+=stats.Get("mana")+stats.Get("intellect")*15m;
        t.MaximumHealth+=stats.Get("health")+stats.Get("stamina")*10m;
        if(result.Weapons.TryGetValue("ranged",out var weapon))
        {
            t.WeaponMinDamage=weapon.MinimumDamage;
            t.WeaponMaxDamage=weapon.MaximumDamage;
            t.WeaponSpeedSeconds=weapon.SpeedSeconds;
        }
        ApplyPetStats(request.PetTuning,stats);
        request.Equipment=result.Loadout;
        return request;
    }

    public static SurvivalHunterSimRequest Resolve(SurvivalHunterSimRequest original)
    {
        if(original.Equipment?.Items.Count is not >0)return original;
        var result=ForeverSharedEquipmentResolver.Resolve(original.Equipment,"hunter");
        var request=Clone(original);
        var t=request.Tuning;
        var stats=result.Stats;
        var totalAttack=stats.Get("attack-power");
        t.MeleeAttackPower+=totalAttack+stats.Get("melee-attack-power");
        t.RangedAttackPower+=totalAttack+stats.Get("ranged-attack-power");
        t.Agility+=stats.Get("agility");
        t.CriticalChancePercent+=stats.Get("attack-critical-chance-percent");
        t.HitChancePercent+=stats.Get("hit-chance-percent");
        t.MaximumMana+=stats.Get("mana")+stats.Get("intellect")*15m;
        t.MaximumHealth+=stats.Get("health")+stats.Get("stamina")*10m;
        if(result.Weapons.TryGetValue("main-hand",out var main))
        {
            t.MeleeWeaponMinDamage=main.MinimumDamage;
            t.MeleeWeaponMaxDamage=main.MaximumDamage;
            t.MeleeWeaponSpeedSeconds=main.SpeedSeconds;
        }
        if(result.Weapons.TryGetValue("off-hand",out var off))
        {
            t.OffhandWeaponMinDamage=off.MinimumDamage;
            t.OffhandWeaponMaxDamage=off.MaximumDamage;
            t.OffhandWeaponSpeedSeconds=off.SpeedSeconds;
        }
        if(result.Weapons.TryGetValue("ranged",out var ranged))
        {
            t.RangedWeaponMinDamage=ranged.MinimumDamage;
            t.RangedWeaponMaxDamage=ranged.MaximumDamage;
            t.RangedWeaponSpeedSeconds=ranged.SpeedSeconds;
        }
        ApplyPetStats(request.PetTuning,stats);
        request.Equipment=result.Loadout;
        return request;
    }

    public static BeastMasteryHunterSimRequest Resolve(BeastMasteryHunterSimRequest original)
    {
        if(original.Equipment?.Items.Count is not >0)return original;
        var result=ForeverSharedEquipmentResolver.Resolve(original.Equipment,"hunter");
        var request=Clone(original);
        var t=request.Tuning;
        var stats=result.Stats;
        var attack=stats.Get("attack-power");
        t.RangedAttackPower+=attack+stats.Get("ranged-attack-power");
        t.MeleeAttackPower+=attack+stats.Get("melee-attack-power");
        t.HunterStamina+=stats.Get("stamina");
        t.HunterArmor+=stats.Get("armor");
        t.HunterCriticalPercent+=stats.Get("attack-critical-chance-percent");
        t.HunterMeleeCriticalPercent+=stats.Get("attack-critical-chance-percent");
        t.HunterHitPercent+=stats.Get("hit-chance-percent");
        t.HunterMana+=stats.Get("mana")+stats.Get("intellect")*15m;
        t.HunterHealth+=stats.Get("health")+stats.Get("stamina")*10m;
        if(result.Weapons.TryGetValue("ranged",out var weapon))
        {
            t.HunterWeaponMin=weapon.MinimumDamage;
            t.HunterWeaponMax=weapon.MaximumDamage;
            t.HunterWeaponSpeed=weapon.SpeedSeconds;
        }
        ApplyPetStats(t,stats);
        request.Equipment=result.Loadout;
        return request;
    }

    private static void ApplyPetStats(
        WarcraftSim.Data.Forever.Hunter.ForeverBeastMasteryHunterTuning t,
        WarcraftSim.Core.Stats.StatCollection stats)
    {
        // Pet inherits BONUS ranged hit, not the final owner roll chance.
        t.HunterRangedHitBonusPercent+=stats.Get("hit-chance-percent");
        t.HunterExpertisePercent+=stats.Get("expertise-percent");
        t.HunterMeleeCriticalPercent+=stats.Get("attack-critical-chance-percent");
        t.HunterArmor+=stats.Get("armor");
        t.HunterStamina+=stats.Get("stamina");
    }

    private static T Clone<T>(T request) where T:class =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(request)) ??
        throw new InvalidOperationException("Equipment request clone failed.");
}
