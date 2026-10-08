using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Characters;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Creates Forever Bloodrage from current client-visible data. The health cost
/// is 20% of unmodified base health, so callers provide that base-health value
/// explicitly until character base-health tables own it centrally.
/// </summary>
public static class ForeverBloodrageFactory
{
    public const string AbilityKey = "bloodrage";
    public const string RageResourceKey = "rage";
    public const decimal HealthCostPercentOfBaseHealth = 20m;
    public const decimal ImmediateRage = 10m;
    public const decimal PeriodicRage = 10m;
    public const decimal DurationSeconds = 10m;
    public const decimal TickIntervalSeconds = 1m;
    public const decimal CooldownSeconds = 60m;

    public static AbilityDefinition Create(
        decimal baseHealth,
        decimal rageGenerationMultiplier = 1m)
    {
        if (baseHealth <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseHealth),
                baseHealth,
                "Bloodrage requires base health greater than zero."
            );
        }

        if (rageGenerationMultiplier < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rageGenerationMultiplier),
                rageGenerationMultiplier,
                "Bloodrage Rage generation multiplier cannot be negative."
            );
        }

        var healthCost =
            baseHealth *
            HealthCostPercentOfBaseHealth /
            100m;

        return new AbilityDefinition
        {
            Key = AbilityKey,
            Name = "Bloodrage",
            ClassKey = ForeverCharacterClassKeys.Warrior,
            RequiredLevel = 10,
            CooldownSeconds = CooldownSeconds,
            GlobalCooldownSeconds = 0m,
            IsOffGlobalCooldown = true,
            Effects =
            [
                AlwaysHitSelfEffect(
                    key: "health-cost",
                    effectType: AbilityEffectTypes.HealthChange,
                    amount: healthCost,
                    healthOperation: HealthChangeOperationTypes.Damage
                ),
                AlwaysHitSelfEffect(
                    key: "immediate-rage",
                    effectType: AbilityEffectTypes.ResourceChange,
                    amount: ImmediateRage * rageGenerationMultiplier,
                    resourceKey: RageResourceKey
                ),
                new AbilityEffectDefinition
                {
                    Key = "periodic-rage",
                    EffectType = AbilityEffectTypes.PeriodicResourceChange,
                    TargetType = AbilityTargetTypes.Self,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue =
                        PeriodicRage /
                        (DurationSeconds / TickIntervalSeconds) *
                        rageGenerationMultiplier,
                    MaximumValue =
                        PeriodicRage /
                        (DurationSeconds / TickIntervalSeconds) *
                        rageGenerationMultiplier,
                    ResourceKey = RageResourceKey,
                    ResourceChangeOperation = ResourceChangeOperationTypes.Gain,
                    DurationSeconds = DurationSeconds,
                    TickIntervalSeconds = TickIntervalSeconds,
                    IncludeExpirationBoundaryTick = true,
                    AuraKey = "bloodrage-rage-generation"
                }
            ]
        };
    }

    private static AbilityEffectDefinition AlwaysHitSelfEffect(
        string key,
        string effectType,
        decimal amount,
        string? resourceKey = null,
        string? healthOperation = null)
    {
        return new AbilityEffectDefinition
        {
            Key = key,
            EffectType = effectType,
            TargetType = AbilityTargetTypes.Self,
            ResolutionType = CombatResolutionTypes.AlwaysHits,
            CanMiss = false,
            CanCrit = false,
            MinimumValue = amount,
            MaximumValue = amount,
            ResourceKey = resourceKey,
            ResourceChangeOperation = ResourceChangeOperationTypes.Gain,
            HealthChangeOperation =
                healthOperation ?? HealthChangeOperationTypes.Heal
        };
    }
}
