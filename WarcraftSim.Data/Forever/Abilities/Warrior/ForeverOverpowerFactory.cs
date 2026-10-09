using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Overpower ranks 1-4 and Bloodthrill reactive opportunity registration.
/// Call AddReactiveOpportunityDefinition(CreateOpportunity(...)) after
/// the actor learns the ability and register ReactiveAbilityOpportunityProcessor.
/// </summary>
public static class ForeverOverpowerFactory
{
    public const string AbilityKey = "overpower";
    public const string OpportunityKey = "overpower-opportunity";
    public const string RageResourceKey = "rage";
    public const decimal DodgeWindowSeconds = 5m;
    public const decimal BloodthrillWindowSeconds = 6m;

    public static readonly (int Level, decimal BonusDamage)[] Ranks =
    [
        (12, 5m),
        (28, 15m),
        (44, 25m),
        (60, 35m)
    ];

    public static AbilityDefinition CreateForLevel(int characterLevel,
        decimal minimumWeaponDamage, decimal maximumWeaponDamage,
        decimal normalizedWeaponSpeedSeconds, string attackSkillStatKey,
        bool requireBattleStance = true)
    {
        if (characterLevel < 12)
            throw new ArgumentOutOfRangeException(nameof(characterLevel));
        if (minimumWeaponDamage < 0m || maximumWeaponDamage < minimumWeaponDamage)
            throw new ArgumentOutOfRangeException(nameof(minimumWeaponDamage));
        if (normalizedWeaponSpeedSeconds <= 0m)
            throw new ArgumentOutOfRangeException(nameof(normalizedWeaponSpeedSeconds));
        if (string.IsNullOrWhiteSpace(attackSkillStatKey))
            throw new ArgumentException("An attack skill stat key is required.", nameof(attackSkillStatKey));

        var rankIndex = Array.FindLastIndex(Ranks,
            row => characterLevel >= row.Level);
        var rank = Ranks[rankIndex];
        var damage = new AbilityEffectDefinition
        {
            Key = "damage",
            EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy,
            WeaponHandKey = WeaponHandKeys.MainHand,
            MinimumValue = minimumWeaponDamage + rank.BonusDamage,
            MaximumValue = maximumWeaponDamage + rank.BonusDamage,
            ScalingStatKey = ForeverCombatStatKeys.AttackPower,
            ScalingCoefficient = normalizedWeaponSpeedSeconds /
                ForeverAutoAttackFactory.AttackPowerPerWeaponDamagePerSecond,
            MitigationType = DamageMitigationTypes.Armor
        };
        ForeverMeleeAttackProfiles.Apply(damage,
            ForeverMeleeAttackProfiles.PlayerSpecial, attackSkillStatKey);
        damage.CanBeDodged = false;
        damage.CanBeParried = false;
        damage.CanBeBlocked = false;

        var ability = new AbilityDefinition
        {
            Key = AbilityKey,
            Name = $"Overpower (Rank {rankIndex + 1})",
            ClassKey = ForeverCharacterClassKeys.Warrior,
            RequiredLevel = rank.Level,
            CooldownSeconds = 5m,
            GlobalCooldownSeconds = 1.5m,
            RequiredTargetOpportunityKey = OpportunityKey,
            ResourceCosts =
            [
                new AbilityResourceCost { ResourceKey = RageResourceKey, Amount = 5m }
            ],
            Effects = [damage]
        };
        if (requireBattleStance)
            ability.RequiredSourceAuraKeys.Add(ForeverWarriorAuraKeys.BattleStance);
        return ability;
    }

    public static ReactiveAbilityOpportunityDefinition CreateOpportunity(int bloodthrillTalentRank = 0)
    {
        if (bloodthrillTalentRank is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(bloodthrillTalentRank));
        var definition = new ReactiveAbilityOpportunityDefinition
        {
            OpportunityKey = OpportunityKey,
            AbilityKey = AbilityKey,
            DodgeWindowSeconds = DodgeWindowSeconds,
            ProcWindowSeconds = BloodthrillWindowSeconds,
            ProcChancePercent = bloodthrillTalentRank * 4m,
            RequiredTargetAuraKey = ForeverRendFactory.AuraKey
        };
        definition.EligibleResolutionTypes.Add(ForeverCombatResolutionTypes.PlayerMeleeAuto);
        definition.EligibleResolutionTypes.Add(ForeverCombatResolutionTypes.PlayerDualWieldMeleeAuto);
        definition.EligibleResolutionTypes.Add(ForeverCombatResolutionTypes.PlayerMeleeSpecial);
        return definition;
    }
}
