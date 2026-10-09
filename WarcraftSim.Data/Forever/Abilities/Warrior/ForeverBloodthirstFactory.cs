using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Creates the current Forever Bloodthirst. Blizzard's October 2 beta update
/// raised its Attack Power ratio to 45%; the rank-specific flat damage remains
/// represented from current client data. Movement speed is tracked as an aura
/// marker because movement-speed simulation is outside the combat model.
/// </summary>
public static class ForeverBloodthirstFactory
{
    public const string AbilityKey =
        "bloodthirst";

    public const string RageResourceKey =
        "rage";

    public const decimal AttackPowerCoefficient =
        0.45m;

    public static AbilityDefinition CreateForLevel(
        int characterLevel,
        string attackSkillStatKey,
        decimal rageCostReduction = 0m)
    {
        if (string.IsNullOrWhiteSpace(attackSkillStatKey))
        {
            throw new ArgumentException(
                "Bloodthirst requires a melee attack-skill stat key.",
                nameof(attackSkillStatKey)
            );
        }

        if (rageCostReduction < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rageCostReduction),
                rageCostReduction,
                "Bloodthirst Rage-cost reduction cannot be negative."
            );
        }

        var rank =
            ForeverBloodthirstRanks.GetHighestAvailable(characterLevel);

        var damageEffect =
            new AbilityEffectDefinition
            {
                Key = "damage",
                EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy,
                MinimumValue = rank.FlatDamage,
                MaximumValue = rank.FlatDamage,
                ScalingStatKey = ForeverCombatStatKeys.AttackPower,
                ScalingCoefficient = AttackPowerCoefficient,
                MitigationType = DamageMitigationTypes.Armor
            };

        ForeverMeleeAttackProfiles.Apply(
            damageEffect,
            ForeverMeleeAttackProfiles.PlayerSpecial,
            attackSkillStatKey
        );

        var movementMarker =
            new AbilityEffectDefinition
            {
                Key = "movement-speed",
                EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Self,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false,
                CanCrit = false,
                AuraKey = ForeverWarriorAuraKeys.BloodthirstMovementSpeed,
                DurationSeconds = rank.MovementSpeedDurationSeconds,
                AuraStackingMode = AuraStackingMode.Refresh,
                DependsOnEffectKey = damageEffect.Key,
                DependencyCondition = EffectDependencyConditions.Landed,
                Tags =
                [
                    "movement-speed",
                    "movement-speed-10-percent",
                    "marker-only"
                ]
            };

        return new AbilityDefinition
        {
            Key = AbilityKey,
            Name = $"Bloodthirst (Rank {rank.Rank})",
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
                movementMarker
            ]
        };
    }
}
