using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Current 70707 provisional Arcane Resilience: +25% effective Intellect
/// as armor per rank (max 3). Shared by precombat preview and live Mage.
/// https://wowfdb.com/talents/mage/
/// </summary>
public static class MageArcaneResilienceArmor
{
    public static decimal Apply(StatCollection stats, int resilienceRanks, int arcaneMindRanks)
    {
        ArgumentNullException.ThrowIfNull(stats);
        if(resilienceRanks is <0 or >3 || arcaneMindRanks is <0 or >5)
            throw new ArgumentException("Invalid Mage Arcane Resilience / Arcane Mind ranks.");
        var intellect=Math.Max(0m,stats.Get("intellect"))*(1m+.02m*arcaneMindRanks);
        var armor=intellect*resilienceRanks*.25m;
        stats.Set("armor",stats.Get("armor")+armor);
        return armor;
    }

    public static decimal ApplyFromProfile(StatCollection stats, CharacterProfile character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var selections=character.Talents?.Selections;
        if(selections is null)return 0m;
        int Rank(string key)=>selections.Where(t=>string.Equals(t.TalentKey,key,
            StringComparison.OrdinalIgnoreCase)).Sum(t=>t.Rank);
        return Apply(stats,Rank("arcane-resilience"),Rank("arcane-mind"));
    }
}
