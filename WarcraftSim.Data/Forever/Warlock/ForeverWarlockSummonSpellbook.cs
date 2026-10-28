using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Data.Forever.Warlock;

/// <summary>
/// Current level-60 Forever 70532 permanent demon summoning for opt-in
/// in-combat replacement. Level-60 base Mana is a configurable input, not a
/// server-proven constant. All five permanent families use the same 10s cast.
/// Temporary Infernal/Doomguard summons are excluded from these talents.
/// </summary>
public static class ForeverWarlockSummonSpellbook
{
    public static AbilityDefinition Replacement(string family,
        ForeverWarlockTalentBuild talents,decimal baseMana,bool useFelDomination)
    {
        var demon=ForeverWarlockDemons.Get(family);
        if(demon.IsTemporary)
            throw new ArgumentException("Temporary demon summoning is a separate mechanic.");
        if(baseMana<=0m)
            throw new ArgumentOutOfRangeException(nameof(baseMana));
        if(useFelDomination && talents.Rank("fel-domination")<=0)
            throw new ArgumentException("Fel Domination must be learned.");
        var rank=talents.Rank("master-summoner");
        var time=Math.Max(.5m,10m-2m*rank-(useFelDomination?5.5m:0m));
        var mana=baseMana*(demon.Key=="imp"?.8m:1m)*
            (1m-.2m*rank)*(useFelDomination?.5m:1m);
        var summon=new AbilityDefinition {
            Key="summon-"+demon.Key,Name="Summon "+demon.Key,
            ClassKey="warlock",CastTimeSeconds=time,
            GlobalCooldownSeconds=1.5m,
            ResourceCosts=[new AbilityResourceCost {ResourceKey="mana",Amount=mana}]
        };
        // 70532: Imp is reagent-free; the other four permanent demons
        // consume a Soul Shard. The executor spends both atomically.
        if(demon.Key!="imp")
            summon.ResourceCosts.Add(new AbilityResourceCost {
                ResourceKey="soul-shard",Amount=1m
            });
        return summon;
    }

    public static AbilityDefinition FelDomination() => new() {
        Key="fel-domination",Name="Fel Domination",ClassKey="warlock",
        CooldownSeconds=300m,CastTimeSeconds=0m,
        IsOffGlobalCooldown=true,GlobalCooldownSeconds=0m,
        Effects=[new AbilityEffectDefinition {
            Key="fel-domination-aura",EffectType=AbilityEffectTypes.ApplyAura,
            TargetType=AbilityTargetTypes.Self,AuraKey="warlock-fel-domination",
            DurationSeconds=15m,ResolutionType=CombatResolutionTypes.AlwaysHits,
            CanMiss=false,CanCrit=false
        }]
    };
}
