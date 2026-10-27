using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Data.Forever.Abilities.Warrior;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>Forever level-60 Protection actions. Offensive effects use Core's
/// ordinary hit/crit/mitigation pipeline; shield-only actions are not registered
/// unless a shield is equipped. Crowd-control outcomes require encounter AI.</summary>
public static class WarriorProtectionAbilityFactory
{
    public const string RevengeOpportunityKey = "revenge-opportunity";

    public static IEnumerable<AbilityDefinition> Create(WarriorSimRequest request)
    {
        // Taunt is a Defensive Stance action, not a shield-dependent talent.
        // Forever build 70532 lists an 8s cooldown and 3s forced-target window.
        yield return new AbilityDefinition {
            Key = "taunt", Name = "Taunt", RequiredLevel = 10,
            ClassKey = "warrior", IsOffGlobalCooldown = true,
            GlobalCooldownSeconds = 0m, CooldownSeconds = 8m,
            RequiredSourceAuraKeys = [ForeverWarriorAuraKeys.DefensiveStance],
            Effects = [new AbilityEffectDefinition {
                Key = "taunt-target", EffectType = AbilityEffectTypes.Taunt,
                TargetType = AbilityTargetTypes.Enemy,
                ResolutionType = CombatResolutionTypes.AlwaysHits,
                CanMiss = false, CanCrit = false,
                DurationSeconds = 3m,
                TauntThreatOperation = ThreatManipulationOperationTypes.MatchHighest
            }]
        };
        if (!request.ShieldEquipped) yield break;
        var focus = ForeverWarriorTalentCatalog.Rank(request, "focused-rage");
        var revengeRank = ForeverWarriorTalentCatalog.Rank(request, "improved-revenge");
        yield return Attack("revenge", "Revenge (Rank 6)", 138m * (1m + 0.20m * revengeRank),
            168m * (1m + 0.20m * revengeRank), Math.Max(0m, 5m - focus), 5m,
            opportunityKey: RevengeOpportunityKey);
        if (ForeverWarriorTalentCatalog.Rank(request, "shield-slam") > 0)
            yield return Attack("shield-slam", "Shield Slam (Forever)", 421m, 439m,
                Math.Max(0m, 20m - focus), 6m, blockValueScaling: true);
        yield return Utility("shield-block", "Shield Block", 10m, 5m, offGcd: true);
        yield return Utility("shield-wall", "Shield Wall", 0m,
            900m - 330m * ForeverWarriorTalentCatalog.Rank(request, "improved-shield-wall"), offGcd: true);
        if (ForeverWarriorTalentCatalog.Rank(request, "last-stand") > 0)
            yield return Utility("last-stand", "Last Stand", 0m, 180m, offGcd: true);
        if (ForeverWarriorTalentCatalog.Rank(request, "concussion-blow") > 0)
            yield return Utility("concussion-blow", "Concussion Blow", 10m, 45m, offGcd: false);
        yield return Attack("shield-bash", "Shield Bash (Rank 3)", 45m, 45m,
            Math.Max(0m, 10m - focus), 12m);
    }

    private static AbilityDefinition Attack(string key, string name, decimal min, decimal max,
        decimal cost, decimal cooldown, bool blockValueScaling = false, string? opportunityKey = null)
    {
        var damage = new AbilityEffectDefinition
        {
            Key = "damage", EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy, MinimumValue = min, MaximumValue = max,
            MitigationType = DamageMitigationTypes.Armor,
            ScalingStatKey = blockValueScaling ? ForeverCombatStatKeys.BlockValue : null,
            ScalingCoefficient = blockValueScaling ? 1m : 0m
        };
        ForeverMeleeAttackProfiles.Apply(damage, ForeverMeleeAttackProfiles.PlayerSpecial, "main-hand-weapon-skill");
        return new AbilityDefinition
        {
            Key = key, Name = name, RequiredLevel = 60,
            GlobalCooldownSeconds = 1.5m, CooldownSeconds = cooldown,
            RequiredTargetOpportunityKey = opportunityKey,
            RequiredSourceAuraKeys = [ForeverWarriorAuraKeys.DefensiveStance],
            ResourceCosts = [new AbilityResourceCost { ResourceKey = "rage", Amount = cost }],
            Effects = [damage]
        };
    }

    private static AbilityDefinition Utility(string key, string name, decimal cost, decimal cooldown, bool offGcd) =>
        new()
        {
            Key = key, Name = name, RequiredLevel = 1,
            IsOffGlobalCooldown = offGcd, GlobalCooldownSeconds = offGcd ? 0m : 1.5m,
            CooldownSeconds = cooldown,
            RequiredSourceAuraKeys = [ForeverWarriorAuraKeys.DefensiveStance],
            ResourceCosts = cost > 0m ? [new AbilityResourceCost { ResourceKey = "rage", Amount = cost }] : []
        };
}
