using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Characters;

namespace WarcraftSim.Data.Forever.Abilities.Warrior;

/// <summary>
/// Stance transitions implemented through generic self-aura and resource-cap
/// effects. The beta tooltip grants *additional* 3 Rage per Tactical Mastery
/// point; caller supplies any separately verified innate retention baseline.
/// </summary>
public static class ForeverWarriorStanceFactory
{
    private static readonly string[] Stances =
    [
        ForeverWarriorAuraKeys.BattleStance,
        ForeverWarriorAuraKeys.DefensiveStance,
        ForeverWarriorAuraKeys.BerserkerStance
    ];

    public static AbilityDefinition Create(
        string stanceAuraKey,
        int improvedTacticalMasteryRank = 0,
        decimal baseRetainedRage = 0m,
        decimal globalCooldownSeconds = 1.5m)
    {
        if (!Stances.Contains(stanceAuraKey, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Unknown Warrior stance.", nameof(stanceAuraKey));
        if (improvedTacticalMasteryRank is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(improvedTacticalMasteryRank));
        if (baseRetainedRage < 0m)
            throw new ArgumentOutOfRangeException(nameof(baseRetainedRage));
        if (globalCooldownSeconds < 0m)
            throw new ArgumentOutOfRangeException(nameof(globalCooldownSeconds));

        var effects = new List<AbilityEffectDefinition>
        {
            new()
            {
                Key = "retain-rage", EffectType = AbilityEffectTypes.ResourceChange,
                TargetType = AbilityTargetTypes.Self, ResourceKey = "rage",
                ResourceChangeOperation = ResourceChangeOperationTypes.Cap,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false,
                MinimumValue = baseRetainedRage + improvedTacticalMasteryRank * 3m,
                MaximumValue = baseRetainedRage + improvedTacticalMasteryRank * 3m
            }
        };
        foreach (var stance in Stances.Where(stance =>
                     !string.Equals(stance, stanceAuraKey, StringComparison.OrdinalIgnoreCase)))
        {
            effects.Add(new AbilityEffectDefinition
            {
                Key = $"remove-{stance}", EffectType = AbilityEffectTypes.RemoveAura,
                TargetType = AbilityTargetTypes.Self, AuraKey = stance,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false
            });
        }
        effects.Add(new AbilityEffectDefinition
        {
            Key = "activate-stance", EffectType = AbilityEffectTypes.ApplyAura,
            TargetType = AbilityTargetTypes.Self, AuraKey = stanceAuraKey,
            DurationSeconds = 86400m,
            ResolutionType = CombatResolutionTypes.AlwaysHits,
            CanMiss = false, CanCrit = false
        });

        return new AbilityDefinition
        {
            Key = $"enter-{stanceAuraKey}", Name = $"Enter {stanceAuraKey}",
            ClassKey = ForeverCharacterClassKeys.Warrior,
            // Stances have a shared cooldown independent of ability GCD.
            IsOffGlobalCooldown = true,
            GlobalCooldownSeconds = 0m,
            SharedCooldownGroupKey = "warrior-stance",
            SharedCooldownSeconds = globalCooldownSeconds,
            ForbiddenSourceAuraKeys = [stanceAuraKey],
            Effects = effects
        };
    }
}
