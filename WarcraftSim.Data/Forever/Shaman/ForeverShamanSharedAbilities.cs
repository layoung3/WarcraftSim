using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>Common, independently client-confirmed Forever level-60 Shaman spells.</summary>
public static class ForeverShamanSharedAbilities
{
    /// <summary>Forever Windfury Totem rank 3, 5-minute air totem, 250 Mana.</summary>
    public static AbilityDefinition CreateWindfuryTotem() => new()
    {
        Key="windfury-totem",Name="Windfury Totem",ClassKey="shaman",
        RequiredLevel=52,GlobalCooldownSeconds=1m,
        ResourceCosts=[new AbilityResourceCost {ResourceKey="mana",Amount=250m}],
        ForbiddenSourceAuraKeys=["shaman-windfury-totem"],
        Effects=[new AbilityEffectDefinition {
            Key="windfury-totem-air",EffectType=AbilityEffectTypes.ApplyAura,
            TargetType=AbilityTargetTypes.Self,AuraKey="shaman-windfury-totem",
            DurationSeconds=300m,ResolutionType=CombatResolutionTypes.AlwaysHits,
            CanMiss=false,CanCrit=false
        }]
    };

    public static AbilityDefinition CreateLightningShield() => new()
    {
        Key="lightning-shield",Name="Lightning Shield",ClassKey="shaman",
        RequiredLevel=56,GlobalCooldownSeconds=1.5m,
        ResourceCosts=[new AbilityResourceCost {ResourceKey="mana",Amount=370m}],
        ForbiddenSourceAuraKeys=["shaman-lightning-shield"],
        Effects=[new AbilityEffectDefinition {
            Key="lightning-shield-aura",EffectType=AbilityEffectTypes.ApplyAura,
            TargetType=AbilityTargetTypes.Self,AuraKey="shaman-lightning-shield",
            DurationSeconds=600m,ResolutionType=CombatResolutionTypes.AlwaysHits,
            CanMiss=false,CanCrit=false
        }]
    };
}
