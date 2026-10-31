using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Hunter;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Warlock;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Single source of truth for typed runner and strict itemized regression fixtures.</summary>
internal static class Cp174ItemizedPresetFixture
{
    public static ForeverLevel60SimulationRequest Apply(ForeverLevel60SimulationRequest request)
    {
        var preset=ForeverItemizedPresetCatalog.Load(request.ClassKey,request.Specialization);
        request.ItemizedTalentMode=true;
        request.ItemizedTalentRanks=new Dictionary<string,int>(preset.Ranks,StringComparer.OrdinalIgnoreCase);
        var rankMap=preset.Ranks;
        var cls=request.ClassKey;
        if(cls=="mage"&&request.Mage is { } mage)
        {
            var known=ForeverMageTalentBuild.SupportedTalentMetadata;
            mage.Talents.Ranks=Filter(known.Select(x=>x.Key),rankMap);
            mage.Talents.ArcanePointsSpent=Spent("arcane",known.Select(x=>(x.Key,x.Tree)),rankMap);
            mage.Talents.FirePointsSpent=Spent("fire",known.Select(x=>(x.Key,x.Tree)),rankMap);
            mage.Talents.FrostPointsSpent=Spent("frost",known.Select(x=>(x.Key,x.Tree)),rankMap);
            // Aggregates also include historical-only source-backed talents.
            foreach(var t in ForeverTalentVisualReferenceCatalog.Records.Where(x=>x.ClassKey=="mage" && !known.Any(k=>k.Key==x.Key)))
                if(rankMap.TryGetValue(t.Key,out var r)){
                    if(t.Tree=="arcane")mage.Talents.ArcanePointsSpent+=r;
                    if(t.Tree=="fire")mage.Talents.FirePointsSpent+=r;
                    if(t.Tree=="frost")mage.Talents.FrostPointsSpent+=r;
                }
        }
        else if(cls=="warlock"&&request.Warlock is { } warlock)
        {
            var known=ForeverWarlockTalentBuild.SupportedTalentMetadata;
            warlock.Talents.Ranks=Filter(known.Select(x=>x.Key),rankMap);
            warlock.Talents.AfflictionPointsSpent=Spent("affliction",known.Select(x=>(x.Key,x.Tree)),rankMap);
            warlock.Talents.DemonologyPointsSpent=Spent("demonology",known.Select(x=>(x.Key,x.Tree)),rankMap);
            warlock.Talents.DestructionPointsSpent=Spent("destruction",known.Select(x=>(x.Key,x.Tree)),rankMap);
            foreach(var t in ForeverTalentVisualReferenceCatalog.Records.Where(x=>x.ClassKey=="warlock" && !known.Any(k=>k.Key==x.Key)))
                if(rankMap.TryGetValue(t.Key,out var r)){
                    if(t.Tree=="affliction")warlock.Talents.AfflictionPointsSpent+=r;
                    if(t.Tree=="demonology")warlock.Talents.DemonologyPointsSpent+=r;
                    if(t.Tree=="destruction")warlock.Talents.DestructionPointsSpent+=r;
                }
        }
        else if(cls=="hunter"&&request.Marksmanship is { } mm)
        {
            var known=ForeverMarksmanshipHunterTalents.SupportedTalentMetadata;
            mm.Talents.Ranks=Filter(known.Select(x=>x.Key),rankMap);
            mm.Talents.MarksmanshipPointsSpent=rankMap.Values.Sum();
            mm.Talents.BeastMasteryPointsSpent=0;
            mm.Talents.SurvivalPointsSpent=0;
        }
        else if(cls=="hunter"&&request.Survival is { } sv)
            sv.Talents.Ranks=Filter(ForeverSurvivalHunterTalents.SupportedTalentMetadata.Select(x=>x.Key),rankMap);
        else if(cls=="hunter"&&request.BeastMastery is { } bm)
            bm.Talents.Ranks=Filter(ForeverBeastMasteryHunterTalents.SupportedTalentMetadata.Select(x=>x.Key),rankMap);
        else throw new ArgumentException("Fixture requires one typed class request.");
        return request;
    }
    private static Dictionary<string,int> Filter(IEnumerable<string> known,Dictionary<string,int> ranks)=>
        known.Where(ranks.ContainsKey).ToDictionary(x=>x,x=>ranks[x],StringComparer.OrdinalIgnoreCase);
    private static int Spent(string tree,IEnumerable<(string Key,string Tree)> talents,
        Dictionary<string,int> ranks)=>talents.Where(x=>x.Tree==tree&&ranks.ContainsKey(x.Key))
            .Sum(x=>ranks[x.Key]);
}
