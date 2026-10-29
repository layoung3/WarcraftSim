using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Priest;

/// <summary>Current Forever talent abilities; only configured eligible Priests receive these.</summary>
public static class ForeverPriestMajorCooldowns
{
    public static AbilityDefinition InnerFocus() => new()
    {
        Key = "inner-focus", Name = "Inner Focus", ClassKey = "priest",
        CooldownSeconds = 180m, GlobalCooldownSeconds = 0m, IsOffGlobalCooldown = true,
        Effects = [new AbilityEffectDefinition {
            Key = "inner-focus-buff", EffectType = AbilityEffectTypes.ApplyAura,
            TargetType = AbilityTargetTypes.Self, AuraKey = "priest-inner-focus",
            DurationSeconds = 1201m, ResolutionType = CombatResolutionTypes.AlwaysHits,
            CanMiss = false, CanCrit = false
        }],
        ForbiddenSourceAuraKeys = ["priest-inner-focus"]
    };

    // The effect is ally-compatible by client tooltip, but solo runners explicitly
    // target self until party external-buff targeting is configured.
    public static AbilityDefinition PowerInfusion(decimal baseMana = 1376m)
    {
        if (baseMana <= 0m) throw new ArgumentOutOfRangeException(nameof(baseMana));
        return new AbilityDefinition {
            Key = "power-infusion", Name = "Power Infusion", ClassKey = "priest",
            CooldownSeconds = 180m, GlobalCooldownSeconds = 1.5m,
            ResourceCosts = [new AbilityResourceCost {ResourceKey="mana",Amount=baseMana*.2m}],
            Effects = [new AbilityEffectDefinition {
                Key = "power-infusion-buff",EffectType=AbilityEffectTypes.ApplyAura,
                TargetType=AbilityTargetTypes.Self,AuraKey="priest-power-infusion",
                DurationSeconds=15m,ResolutionType=CombatResolutionTypes.AlwaysHits,
                CanMiss=false,CanCrit=false
            }],
            ForbiddenSourceAuraKeys = ["priest-power-infusion"]
        };
    }
}
