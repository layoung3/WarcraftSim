using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Shaman;

/// <summary>Current70888 client utility ranks. Their focused scenario consumers
/// live in the canonical adapter; these are not inert coverage placeholders.
/// Source: https://theforeverera.com/en/spells/shaman/ reviewed2026-11-12.</summary>
public static class ForeverShamanClassUtilityAbilities
{
    public static IReadOnlyList<AbilityDefinition> Create(ForeverEnhancementShamanTalents talents,decimal baseMana)
    {
        AbilityDefinition totem(string key,string effect,string aura,decimal mana,decimal duration,decimal cooldown=0m)=>new()
        {
            Key=key,Name=key,ClassKey="shaman",GlobalCooldownSeconds=1m,CooldownSeconds=cooldown,
            ResourceCosts=[new AbilityResourceCost{ResourceKey="mana",Amount=mana}],
            ForbiddenSourceAuraKeys=[aura],
            Effects=[new AbilityEffectDefinition{Key=effect,EffectType=AbilityEffectTypes.ApplyAura,
                TargetType=AbilityTargetTypes.Self,AuraKey=aura,DurationSeconds=duration,
                ResolutionType=CombatResolutionTypes.AlwaysHits,CanMiss=false,CanCrit=false}]
        };
        var wolf=totem("ghost-wolf","ghost-wolf-form","shaman-ghost-wolf",100m,1201m);
        wolf.GlobalCooldownSeconds=1.5m;
        wolf.CastTimeSeconds=talents.Rank("improved-ghost-wolf") switch {1=>2m,2=>0m,_=>3m};
        var cancel=new AbilityDefinition
        {
            Key="cancel-ghost-wolf",Name="Leave Ghost Wolf",ClassKey="shaman",IsOffGlobalCooldown=true,
            RequiredSourceAuraKeys=["shaman-ghost-wolf"],
            Effects=[new AbilityEffectDefinition{Key="cancel-ghost-wolf-form",EffectType=AbilityEffectTypes.RemoveAura,
                TargetType=AbilityTargetTypes.Self,AuraKey="shaman-ghost-wolf",RemoveAuraOnlyFromSource=true,
                ResolutionType=CombatResolutionTypes.AlwaysHits,CanMiss=false,CanCrit=false}]
        };
        return [wolf,cancel,
            totem("earthbind-totem","earthbind-aura","shaman-earthbind-totem",baseMana*.06m,45m,15m),
            totem("stoneclaw-totem","stoneclaw-aura","shaman-stoneclaw-totem",140m,15m,30m),
            totem("windwall-totem","windwall-aura","shaman-windwall-totem",225m,300m),
            totem("grounding-totem","grounding-aura","shaman-grounding-totem",baseMana*.06m,45m,15m-talents.Rank("guardian-totems")),
            totem("flametongue-totem","flametongue-totem-aura","shaman-flametongue-totem",275m,300m),
            new AbilityDefinition
            {
                Key="totemic-call",Name="Totemic Call",ClassKey="shaman",CooldownSeconds=30m,
                GlobalCooldownSeconds=1m,ResourceCosts=[new AbilityResourceCost{ResourceKey="mana",
                    Amount=baseMana*.25m*(1m-.05m*talents.Rank("totemic-focus"))}]
            },
            new AbilityDefinition
            {
                Key="reincarnation",Name="Reincarnation",ClassKey="shaman",IsPassive=true,
                CooldownSeconds=3600m-600m*talents.Rank("improved-reincarnation")
            }];
    }
}
