using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

/// <summary>Restricted exact-name/tree/cap matches to the pinned historical tree.
/// No numerical effects, current-client certification or upstream simulation flags are imported.</summary>
public static class ForeverHistoricalCasterTalentCatalog
{
    public const string SourceCommit = "1cc5ae1e9abdbf941b7326d2d90421822dce31ba";
    public const string SourceStatus = "historical-provisional-not-current-client-certified";
    public sealed record Entry(string ClassKey,string Key,string Tree,int MaxRank,int Row,
        int Column,string? PrerequisiteKey,int PrerequisiteRank);
    private static readonly Entry[] Entries =
    [
        new("druid","improved-wrath","balance",5,1,2,null,0),
        new("druid","genesis","balance",5,1,3,null,0),
        new("druid","improved-moonfire","balance",2,2,2,null,0),
        new("druid","moonglow","balance",3,2,1,null,0),
        new("druid","natures-majesty","balance",2,2,3,null,0),
        new("druid","natures-reach","balance",2,2,4,null,0),
        new("druid","natures-splendor","balance",1,3,3,"natures-majesty",2),
        new("druid","insect-swarm","balance",1,4,1,null,0),
        new("druid","vengeance","balance",5,4,2,"improved-moonfire",2),
        new("druid","eclipse","balance",3,5,3,null,0),
        new("druid","moonfury","balance",5,6,2,null,0),
        new("druid","moonkin-form","balance",1,7,2,null,0),
        new("druid","natures-grace","balance",1,5,2,null,0),
        new("druid","improved-starfire","balance",5,4,3,null,0),
        new("druid","naturalist","restoration",5,2,1,null,0),
        new("druid","subtlety","restoration",3,2,2,null,0),
        new("druid","reflection","restoration",3,3,2,null,0),
        new("druid","natures-focus","restoration",5,1,2,null,0),
        new("druid","furor","restoration",5,1,3,null,0),
        new("shaman","convection","elemental",5,1,2,null,0),
        new("shaman","concussion","elemental",5,1,3,null,0),
        new("shaman","call-of-flame","elemental",3,2,4,null,0),
        new("shaman","elemental-alacrity","elemental",3,2,3,null,0),
        new("shaman","lightning-overload","elemental",3,5,3,"elemental-alacrity",3),
        new("shaman","elemental-fury","elemental",5,6,3,"lightning-overload",3),
        new("shaman","lava-burst","elemental",1,5,2,null,0),
        new("shaman","elemental-mastery","elemental",1,7,2,"lava-burst",1),
        new("shaman","elemental-focus","elemental",1,3,1,null,0),
        new("shaman","elemental-devastation","elemental",3,2,1,null,0),
        new("shaman","reverberation","elemental",5,2,2,null,0),
        new("shaman","tidal-mastery","restoration",5,4,1,null,0),
        new("shaman","tidal-focus","restoration",5,1,3,null,0),
        new("shaman","mindfulness","restoration",3,2,1,null,0),
        new("shaman","totemic-focus","restoration",5,2,3,null,0),
        new("shaman","restorative-totems","restoration",5,4,2,null,0),
        new("shaman","natural-grace","restoration",3,2,2,null,0),
        new("shaman","improved-healing-wave","restoration",5,1,2,null,0),
        new("shaman","thundering-strikes","enhancement",5,1,2,null,0),
        new("shaman","ancestral-knowledge","enhancement",5,1,3,null,0),
        new("shaman","improved-ghost-wolf","enhancement",2,2,3,null,0),
        new("shaman","shamanistic-focus","enhancement",1,3,3,null,0),
        new("shaman","elemental-warding","elemental",3,3,2,null,0),
        new("shaman","eye-of-the-storm","elemental",3,4,2,null,0),
        new("shaman","elemental-reach","elemental",2,5,1,null,0),
        new("shaman","trial-by-fire","elemental",2,4,1,null,0),
    ];
    public static bool Supports(string cls,string spec) =>
        (cls=="druid" && spec=="balance") || (cls=="shaman" && spec=="elemental");
    public static string SourceUrl(string cls) =>
        $"https://github.com/gunba/wow-forever-sim/blob/{SourceCommit}/ui/core/talents/trees/{cls}.json";
    public static IReadOnlyList<Entry> ForClass(string cls)
    {
        IReadOnlyList<(string Key,string Tree,int MaxRank)> modeled=cls switch {
            "druid"=>ForeverBalanceDruidTalents.SupportedTalentMetadata,
            "shaman"=>ForeverElementalShamanTalents.SupportedTalentMetadata,
            _=>Array.Empty<(string Key,string Tree,int MaxRank)>()
        };
        return Entries.Where(x=>x.ClassKey==cls && modeled.Any(m=>
            m.Key==x.Key && m.Tree==x.Tree && m.MaxRank==x.MaxRank)).ToArray();
    }
    public static Entry? Find(string cls,string key) =>
        ForClass(cls).FirstOrDefault(x=>x.Key==key);
}
