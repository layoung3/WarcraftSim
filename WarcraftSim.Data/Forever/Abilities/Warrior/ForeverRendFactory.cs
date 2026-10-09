using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Seven Forever Rend ranks through level 60. Client base damage is known,
/// but Blizzard's new total AP coefficient is not yet publicly quantified:
/// the caller can supply it explicitly without baking in an invented value.
/// </summary>
public static class ForeverRendFactory
{
    public const string AbilityKey = "rend";
    public const string AuraKey = "rend-bleed";
    public const string RageResourceKey = "rage";

    public static readonly (int Level, decimal TotalBaseDamage, decimal Duration)[] Ranks =
    [
        (4, 15m, 9m),
        (10, 28m, 12m),
        (20, 45m, 15m),
        (30, 66m, 18m),
        (40, 98m, 21m),
        (50, 126m, 21m),
        (60, 147m, 21m)
    ];

    public static AbilityDefinition CreateForLevel(int characterLevel,
        decimal totalAttackPowerCoefficient = 0m, int improvedRendRank = 0)
    {
        if (characterLevel < 4)
            throw new ArgumentOutOfRangeException(nameof(characterLevel),
                "Rend becomes available at level 4.");
        if (totalAttackPowerCoefficient < 0m)
            throw new ArgumentOutOfRangeException(nameof(totalAttackPowerCoefficient));
        if (improvedRendRank is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(improvedRendRank));

        var rankIndex = Array.FindLastIndex(Ranks,
            row => characterLevel >= row.Level);
        var rank = Ranks[rankIndex];
        var tickCount = rank.Duration / 3m;
        var improvedMultiplier = improvedRendRank switch
        {
            1 => 1.12m,
            2 => 1.23m,
            3 => 1.35m,
            _ => 1m
        };

        return new AbilityDefinition
        {
            Key = AbilityKey,
            Name = $"Rend (Rank {rankIndex + 1})",
            ClassKey = ForeverCharacterClassKeys.Warrior,
            RequiredLevel = rank.Level,
            GlobalCooldownSeconds = 1.5m,
            ResourceCosts =
            [
                new AbilityResourceCost { ResourceKey = RageResourceKey, Amount = 10m }
            ],
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = "bleed",
                    EffectType = AbilityEffectTypes.PeriodicDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    MitigationType = DamageMitigationTypes.None,
                    SchoolKey = "physical",
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = rank.TotalBaseDamage / tickCount,
                    MaximumValue = rank.TotalBaseDamage / tickCount,
                    ScalingStatKey = ForeverCombatStatKeys.AttackPower,
                    ScalingCoefficient = totalAttackPowerCoefficient,
                    ScalingCoefficientMode = AbilityEffectScalingCoefficientModes.TotalAcrossOccurrences,
                    DamageMultiplier = improvedMultiplier,
                    DurationSeconds = rank.Duration,
                    TickIntervalSeconds = 3m,
                    IncludeExpirationBoundaryTick = true,
                    AuraKey = AuraKey,
                    AuraStackingMode = AuraStackingMode.Refresh,
                    Tags = ["bleed", "rend"]
                }
            ]
        };
    }
}
