using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Druid;

/// <summary>
/// Client-confirmed, trainer-learned Druid cooldowns. Numeric base Mana is
/// supplied by the class profile rather than inferred from total equipped Mana.
/// </summary>
public static class ForeverDruidSharedCooldowns
{
    public const decimal Level60BaseMana = 1244m; // provisional Classic inherited base; reverify Forever

    public static AbilityDefinition CreateInnervate(decimal baseMana = Level60BaseMana)
    {
        if (baseMana <= 0m) throw new ArgumentOutOfRangeException(nameof(baseMana));
        return new AbilityDefinition
        {
            Key = "innervate", Name = "Innervate", ClassKey = "druid",
            RequiredLevel = 40, CooldownSeconds = 360m, GlobalCooldownSeconds = 1.5m,
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = baseMana * .05m }],
            Effects = [new AbilityEffectDefinition
            {
                Key = "innervate-regen", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Self, AuraKey = "druid-innervate",
                DurationSeconds = 20m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false
            }]
        };
    }

    public static AbilityDefinition CreateBarkskin()
    {
        return new AbilityDefinition
        {
            Key = "barkskin", Name = "Barkskin", ClassKey = "druid",
            RequiredLevel = 44, CooldownSeconds = 60m, GlobalCooldownSeconds = 1.5m,
            Effects = [new AbilityEffectDefinition
            {
                Key = "barkskin-protection", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Self, AuraKey = "druid-barkskin",
                DurationSeconds = 15m, ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false
            }]
        };
    }
}
