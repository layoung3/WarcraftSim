using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;
using WarcraftSim.Core.Simulation.Building;

namespace WarcraftSim.Api.Simulation;

/// <summary>Display adapter only. Reuses combat's existing sourced coefficients;
/// never writes talent-derived values back into a reusable input profile.</summary>
public static class HunterPrecombatStatPreview
{
    public const string Scope = "Hunter precombat preview includes Lightning Reflexes, Lethal Attacks and Surefooted. Ranged AP includes generic item/buff AP. Other talent effects, active auras, temporary effects and companion stats are resolved in combat.";

    public static Dictionary<string,decimal> Apply(StatCollection stats,
        CharacterProfile profile,ForeverExternalModifierSelection modifiers)
    {
        var adjustments=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
        if(!profile.ClassKey.Equals("hunter",StringComparison.OrdinalIgnoreCase))return adjustments;
        int Rank(string key,int maximum)
        {
            var rank=profile.Talents.Selections.Where(x=>x.TalentKey.Equals(key,
                StringComparison.OrdinalIgnoreCase)).Sum(x=>x.Rank);
            if(rank<0 || rank>maximum)throw new ArgumentException("Invalid Hunter preview rank: "+key);
            return rank;
        }
        var lightning=Rank("lightning-reflexes",5);
        if(lightning>0 && profile.Level!=60)
            throw new ArgumentException("Hunter Lightning Reflexes preview currently supports level 60 only.");
        var lethal=Rank("lethal-attacks",5);
        var surefooted=Rank("surefooted",3);
        var keys=new[]{"agility","attack-power","ranged-attack-power",
            "attack-critical-chance-percent","hit-chance-percent","armor"};
        var before=keys.ToDictionary(key=>key,stats.Get,StringComparer.OrdinalIgnoreCase);
        SurvivalHunterAgilityStats.ApplyPreview(stats,lightning);
        stats.Add("attack-critical-chance-percent",lethal);
        stats.Add("hit-chance-percent",surefooted);
        // The generic calculator keeps generic AP and ranged-only AP separate.
        // Combat adds item/external generic AP to RAP. Do not add the naked
        // melee baseline's residual AP, which is an independently observed total.
        var equipment=ForeverSharedEquipmentResolver.Resolve(profile.Equipment,
            "hunter",profile.Level,profile.Race);
        var external=ForeverExternalModifierCatalog.SumSelfRawStats(modifiers,profile.Level);
        var authoredBuffAp=new CharacterBuffStatSourceAdapter().CreateSources(profile)
            .Where(source=>source.IsActive).Sum(source=>source.StatBonuses.GetValueOrDefault("attack-power"));
        stats.Add("ranged-attack-power",equipment.RawStats.Get("attack-power")+
            external.GetValueOrDefault("attack-power")+authoredBuffAp);
        foreach(var key in keys)
            if(stats.Get(key)!=before[key])adjustments[key]=stats.Get(key)-before[key];
        return adjustments;
    }
}
