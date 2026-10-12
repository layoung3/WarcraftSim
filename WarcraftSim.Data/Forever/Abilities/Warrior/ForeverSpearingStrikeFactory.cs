using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Level-60 Forever Spearing Strike. Client-visible base is 40% main-hand
/// weapon damage, with another 80% against Giants, Dragonkin, or mounted.
/// The instant attack uses the already tested normalized AP contribution and
/// single-roll melee profile. Encounter target category must be explicit.
/// </summary>
public static class ForeverSpearingStrikeFactory
{
    public const string AbilityKey = "spearing-strike";

    public static AbilityDefinition Create(
        decimal minimumWeaponDamage, decimal maximumWeaponDamage,
        decimal normalizedWeaponSpeed, string weaponSkillKey,
        bool creatureBonus)
    {
        if (minimumWeaponDamage < 0m || maximumWeaponDamage < minimumWeaponDamage ||
            normalizedWeaponSpeed <= 0m || string.IsNullOrWhiteSpace(weaponSkillKey))
            throw new ArgumentException("Invalid Spearing Strike weapon configuration.");
        var fraction = creatureBonus ? 1.20m : 0.40m;
        var effect = new AbilityEffectDefinition
        {
            Key = "damage", EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy,
            WeaponHandKey = WeaponHandKeys.MainHand,
            MinimumValue = fraction * minimumWeaponDamage,
            MaximumValue = fraction * maximumWeaponDamage,
            ScalingStatKey = ForeverCombatStatKeys.AttackPower,
            ScalingCoefficient = fraction * normalizedWeaponSpeed /
                ForeverAutoAttackFactory.AttackPowerPerWeaponDamagePerSecond,
            MitigationType = DamageMitigationTypes.Armor
        };
        ForeverMeleeAttackProfiles.Apply(effect,
            ForeverMeleeAttackProfiles.PlayerSpecial, weaponSkillKey);
        return new AbilityDefinition
        {
            Key = AbilityKey,
            Name = "Spearing Strike",
            ClassKey = ForeverCharacterClassKeys.Warrior,
            RequiredLevel = 25,
            CooldownSeconds = 20m,
            GlobalCooldownSeconds = 1.5m,
            RequiredSourceAuraKeys = [ForeverWarriorAuraKeys.BattleStance],
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "rage", Amount = 15m }],
            Effects = [effect]
        };
    }
}
