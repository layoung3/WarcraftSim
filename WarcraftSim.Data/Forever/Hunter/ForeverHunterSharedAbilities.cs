using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Hunter;

/// <summary>Baseline abilities available to all Forever Hunter specializations.</summary>
public static class ForeverHunterSharedAbilities
{
    /// <summary>Level 26 Rapid Fire: 100 Mana, 300-second cooldown, 40% melee and ranged haste for 15 seconds.</summary>
    public static AbilityDefinition CreateRapidFire(decimal cooldownSeconds = 300m, decimal durationSeconds = 15m)
    {
        var ability = new AbilityDefinition {
            Key = "rapid-fire", Name = "Rapid Fire", ClassKey = "hunter",
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = 100m }],
            CooldownSeconds = cooldownSeconds,
            IsOffGlobalCooldown = true,
            GlobalCooldownSeconds = 0m,
            Effects = [new AbilityEffectDefinition {
                Key = "rapid-fire-aura", EffectType = AbilityEffectTypes.ApplyAura,
                TargetType = AbilityTargetTypes.Self, AuraKey = "hunter-rapid-fire",
                DurationSeconds = durationSeconds,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false
            }]
        };
        ability.ForbiddenSourceAuraKeys.Add("hunter-rapid-fire");
        return ability;
    }
}
