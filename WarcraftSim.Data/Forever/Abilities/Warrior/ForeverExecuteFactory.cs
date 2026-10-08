using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Creates Forever Execute from client-visible rank data. The client confirms
/// the 20% target-health gate, fixed base Rage cost, and damage gained per
/// additional Rage consumed. Stance requirements and miss refunds remain
/// separate mechanics and are not guessed here.
/// </summary>
public static class ForeverExecuteFactory
{
    public const string AbilityKey = "execute";
    public const string RageResourceKey = "rage";
    public const decimal MaximumTargetHealthPercent = 20m;

    public static AbilityDefinition CreateForLevel(
        int characterLevel,
        string attackSkillStatKey,
        decimal rageCostReduction = 0m)
    {
        if (string.IsNullOrWhiteSpace(attackSkillStatKey))
        {
            throw new ArgumentException(
                "Execute requires a melee attack-skill stat key.",
                nameof(attackSkillStatKey)
            );
        }

        if (rageCostReduction < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rageCostReduction),
                rageCostReduction,
                "Execute Rage cost reduction cannot be negative."
            );
        }

        var rank = ForeverExecuteRanks.GetHighestAvailable(characterLevel);
        var rageCost = Math.Max(0m, rank.RageCost - rageCostReduction);

        var effect =
            new AbilityEffectDefinition
            {
                Key = "damage",
                EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy,
                WeaponHandKey = WeaponHandKeys.MainHand,
                MinimumValue = rank.BaseDamage,
                MaximumValue = rank.BaseDamage,
                ConsumedResourceScalingKey = RageResourceKey,
                ConsumedResourceScalingCoefficient = rank.DamagePerExtraRage,
                MitigationType = DamageMitigationTypes.Armor
            };

        ForeverMeleeAttackProfiles.Apply(
            effect,
            ForeverMeleeAttackProfiles.PlayerSpecial,
            attackSkillStatKey
        );

        return new AbilityDefinition
        {
            Key = AbilityKey,
            Name = $"Execute (Rank {rank.Rank})",
            ClassKey = ForeverCharacterClassKeys.Warrior,
            RequiredLevel = rank.RequiredLevel,
            GlobalCooldownSeconds = rank.GlobalCooldownSeconds,
            MaximumTargetHealthPercent = MaximumTargetHealthPercent,
            ResourceCosts =
            [
                new AbilityResourceCost
                {
                    ResourceKey = RageResourceKey,
                    Amount = rageCost
                }
            ],
            AdditionalResourceConsumptions =
            [
                new AbilityAdditionalResourceConsumption
                {
                    ResourceKey = RageResourceKey
                }
            ],
            Effects = [effect]
        };
    }
}
