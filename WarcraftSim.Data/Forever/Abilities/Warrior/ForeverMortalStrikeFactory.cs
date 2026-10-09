using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Creates the level-appropriate Forever Mortal Strike rank. Its damage uses
/// normalized instant-weapon Attack Power scaling. The healing-reduction aura
/// is represented as an explicit marker so rotations/logging can observe it;
/// healing-taken percentage modification remains a later generic subsystem.
/// </summary>
public static class ForeverMortalStrikeFactory
{
    public const string AbilityKey =
        "mortal-strike";

    public const string RageResourceKey =
        "rage";

    public static AbilityDefinition CreateForLevel(
        int characterLevel,
        decimal minimumWeaponDamage,
        decimal maximumWeaponDamage,
        decimal normalizedWeaponSpeedSeconds,
        string attackSkillStatKey,
        decimal rageCostReduction = 0m)
    {
        if (normalizedWeaponSpeedSeconds <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(normalizedWeaponSpeedSeconds),
                normalizedWeaponSpeedSeconds,
                "Normalized weapon speed must be greater than zero."
            );
        }

        if (string.IsNullOrWhiteSpace(attackSkillStatKey))
        {
            throw new ArgumentException(
                "Mortal Strike requires a melee attack-skill stat key.",
                nameof(attackSkillStatKey)
            );
        }

        if (rageCostReduction < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rageCostReduction),
                rageCostReduction,
                "Mortal Strike Rage-cost reduction cannot be negative."
            );
        }

        var rank =
            ForeverMortalStrikeRanks.GetHighestAvailable(characterLevel);

        var lowerWeaponDamage =
            Math.Max(
                0m,
                Math.Min(minimumWeaponDamage, maximumWeaponDamage)
            );

        var upperWeaponDamage =
            Math.Max(
                0m,
                Math.Max(minimumWeaponDamage, maximumWeaponDamage)
            );

        var damageEffect =
            new AbilityEffectDefinition
            {
                Key = "damage",
                EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy,
                WeaponHandKey = WeaponHandKeys.MainHand,
                MinimumValue = lowerWeaponDamage + rank.BonusWeaponDamage,
                MaximumValue = upperWeaponDamage + rank.BonusWeaponDamage,
                ScalingStatKey = ForeverCombatStatKeys.AttackPower,
                ScalingCoefficient =
                    normalizedWeaponSpeedSeconds /
                    ForeverAutoAttackFactory.AttackPowerPerWeaponDamagePerSecond,
                MitigationType = DamageMitigationTypes.Armor
            };

        ForeverMeleeAttackProfiles.Apply(
            damageEffect,
            ForeverMeleeAttackProfiles.PlayerSpecial,
            attackSkillStatKey
        );

        var healingReductionMarker =
            new AbilityEffectDefinition
            {
                Key = "healing-reduction",
                EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Enemy,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false,
                CanCrit = false,
                AuraKey = ForeverWarriorAuraKeys.MortalStrikeHealingReduction,
                DurationSeconds = rank.HealingReductionDurationSeconds,
                AuraStackingMode = AuraStackingMode.Refresh,
                DependsOnEffectKey = damageEffect.Key,
                DependencyCondition = EffectDependencyConditions.Landed,
                Tags =
                [
                    "healing-reduction",
                    "healing-reduction-50-percent",
                    "marker-only"
                ]
            };

        return new AbilityDefinition
        {
            Key = AbilityKey,
            Name = $"Mortal Strike (Rank {rank.Rank})",
            ClassKey = ForeverCharacterClassKeys.Warrior,
            RequiredLevel = rank.RequiredLevel,
            CooldownSeconds = rank.CooldownSeconds,
            GlobalCooldownSeconds = rank.GlobalCooldownSeconds,
            ResourceCosts =
            [
                new AbilityResourceCost
                {
                    ResourceKey = RageResourceKey,
                    Amount = Math.Max(0m, rank.RageCost - rageCostReduction)
                }
            ],
            Effects =
            [
                damageEffect,
                healingReductionMarker
            ]
        };
    }
}
