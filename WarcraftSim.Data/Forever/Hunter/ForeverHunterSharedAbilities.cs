using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Hunter;

/// <summary>Baseline abilities available to all Forever Hunter specializations.</summary>
public static class ForeverHunterSharedAbilities
{
    /// <summary>Forever 70532 level-60 Rank 7, 120 Mana, +120 ranged AP until cancelled.</summary>
    public static AbilityDefinition CreateAspectOfTheHawk() => new()
    {
        Key = "aspect-of-the-hawk", Name = "Aspect of the Hawk", ClassKey = "hunter",
        RequiredLevel = 60, GlobalCooldownSeconds = 1.5m,
        ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = 120m }],
        ForbiddenSourceAuraKeys = ["hunter-aspect-hawk"],
        Effects = [new AbilityEffectDefinition {
            Key = "hawk-aspect-aura", EffectType = AbilityEffectTypes.ApplyAura,
            TargetType = AbilityTargetTypes.Self, AuraKey = "hunter-aspect-hawk",
            DurationSeconds = 3600m, ResolutionType = CombatResolutionTypes.AlwaysHits,
            CanMiss = false, CanCrit = false
        }]
    };

    /// <summary>Forever 70532 Volley Rank 3: 490 Mana, six 112 Arcane pulses.</summary>
    public static AbilityDefinition CreateVolley(decimal damageMultiplier = 1m)
    {
        if (damageMultiplier < 0m) throw new ArgumentOutOfRangeException(nameof(damageMultiplier));
        return new AbilityDefinition {
            Key = "volley", Name = "Volley", ClassKey = "hunter", RequiredLevel = 58,
            GlobalCooldownSeconds = 1.5m,
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana", Amount = 490m }],
            ChannelDurationSeconds = 6m, ChannelTickIntervalSeconds = 1m,
            Effects = [new AbilityEffectDefinition {
                Key = "volley-pulse", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, SchoolKey = "arcane",
                MitigationType = DamageMitigationTypes.None,
                ResolutionType = CombatResolutionTypes.Spell,
                CanMiss = true, CanCrit = true, MaxTargets = 9,
                ApplyOnChannelTick = true, MinimumValue = 112m,
                MaximumValue = 112m, DamageMultiplier = damageMultiplier
            }]
        };
    }

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
