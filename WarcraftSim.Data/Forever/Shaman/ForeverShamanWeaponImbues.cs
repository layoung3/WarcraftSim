using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>
/// Supported Forever 70532 level-60 weapon imbues. Hand-specific aura keys
/// preserve separate MH/OH ownership. Windfury retains its CP130 extra-swing
/// processor and is selected separately by the existing runner.
/// </summary>
public static class ForeverShamanWeaponImbues
{
    public static IReadOnlyList<AbilityDefinition> CreateForHand(string hand)
    {
        if (hand is not ("main-hand" or "off-hand"))
            throw new ArgumentException("Unrecognized Shaman weapon hand.", nameof(hand));
        return new [] {
            Create("flametongue",hand,155m),
            Create("rockbiter",hand,150m),
            Create("frostbrand",hand,160m)
        };
    }

    private static AbilityDefinition Create(string kind,string hand,decimal mana)
    {
        var auraKey=$"shaman-imbue-{hand}-{kind}";
        return new AbilityDefinition {
            Key=$"{kind}-weapon-{hand}",Name=$"{kind} Weapon ({hand})",
            ClassKey="shaman",SpecializationKey="enhancement",RequiredLevel=60,
            GlobalCooldownSeconds=1.5m,
            ResourceCosts=[new AbilityResourceCost {ResourceKey="mana",Amount=mana}],
            ForbiddenSourceAuraKeys=[auraKey],
            Effects=[new AbilityEffectDefinition {
                Key=$"{kind}-{hand}-aura",EffectType=AbilityEffectTypes.ApplyAura,
                TargetType=AbilityTargetTypes.Self,AuraKey=auraKey,
                DurationSeconds=3600m,ResolutionType=CombatResolutionTypes.AlwaysHits,
                CanMiss=false,CanCrit=false
            }]
        };
    }
}
