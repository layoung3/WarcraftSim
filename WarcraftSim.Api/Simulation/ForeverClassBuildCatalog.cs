using System.Globalization;
using WarcraftSim.Data.Forever.Shaman;
using WarcraftSim.Data.Forever.Rogue;
using WarcraftSim.Data.Forever.Paladin;

namespace WarcraftSim.Api.Simulation;

public static class ForeverClassBuildCatalog
{
    public const string ReviewedAt = "2026-11-12";
    public static readonly string[] Classes = ["warrior","paladin","hunter","rogue","priest","shaman","mage","warlock","druid"];
    public static string[] Trees(string cls) => cls switch {
        "warrior"=>["arms","fury","protection"],"paladin"=>["holy","protection","retribution"],
        "hunter"=>["beast-mastery","marksmanship","survival"],"rogue"=>["assassination","combat","subtlety"],
        "priest"=>["discipline","holy","shadow"],"shaman"=>["elemental","enhancement","restoration"],
        "mage"=>["arcane","fire","frost"],"warlock"=>["affliction","demonology","destruction"],
        "druid"=>["balance","feral","restoration"],_=>throw new ArgumentException("Unknown class.")};
    public static bool CanExecuteClassBuild(string cls) => cls is "shaman" or "rogue" or "paladin";

    public static IReadOnlyList<ForeverClassTalentNode> Talents(string cls)
    {
        if(cls=="shaman")return ForeverShamanClassTalents.Entries.Select(x=>Node(cls,x.Key,x.Tree,
            x.MaxRank,x.Row,x.Column,x.ParentKey,x.ParentRank,x.SourceUrl,x.SourceConfidence)).ToArray();
        if(cls=="rogue")return ForeverRogueClassTalents.Entries.Select(x=>Node(cls,x.Key,x.Tree,
            x.MaxRank,x.Row,x.Column,x.ParentKey,x.ParentRank,x.SourceUrl,x.SourceConfidence)).ToArray();
        if(cls=="paladin")return ForeverPaladinClassTalents.Entries.Select(x=>Node(cls,x.Key,x.Tree,
            x.MaxRank,x.Row,x.Column,x.ParentKey,x.ParentRank,x.SourceUrl,x.SourceConfidence) with {
                Name=x.Name,IconKey=x.IconKey,ReviewedAt=ForeverPaladinClassTalents.ReviewedDate,
                TalentId=x.TalentId,TalentSpellId=x.SpellId,TalentSpellIdSourceUrl=x.SpellIdSourceUrl,
                RankSpellIds=x.RankSpellIds,EffectComponents=x.EffectComponents,
                IconSourceConfidence="published-70888-talent-icon-key; local artwork availability is separate"
            }).ToArray();
        _=Trees(cls);
        return ForeverTalentVisualReferenceCatalog.Records.Where(x=>x.ClassKey==cls).Select(x=>Node(cls,
            x.Key,x.Tree,x.MaxRank,x.Row,x.Column,x.PrerequisiteKey,
            x.PrerequisiteKey is null?0:ForeverTalentVisualReferenceCatalog.Records.FirstOrDefault(
                p=>p.ClassKey==cls&&p.Key==x.PrerequisiteKey)?.MaxRank??0,
            $"https://github.com/gunba/wow-forever-sim/blob/{ForeverTalentVisualReferenceCatalog.SourceCommit}/ui/core/talents/trees/{cls}.json",
            "historical-reference-current-rework-not-reconciled")).ToArray();
    }
    private static ForeverClassTalentNode Node(string cls,string key,string tree,int cap,int row,int column,
        string? parent,int parentRank,string url,string confidence)
    {
        var coverage=ForeverClassBuildCoverage.ForTalent(cls,key);
        var visual=ForeverTalentVisualReferenceCatalog.Match(cls,tree,key,cap);
        var icon=cls=="rogue"?ForeverRogueClassTalents.Find(key)?.IconKey??visual?.IconKey:visual?.IconKey;
        return new(key,CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('-',' ')),tree,cap,row,column,
            parent,parentRank,url,confidence,ReviewedAt,coverage.Status,coverage.Implemented,
            coverage.Missing,coverage.Conditions) {IconKey=icon,
                IconSourceConfidence=icon is null?"unresolved":"source-matched-key-with-pinned-historical-artwork-only"};
    }

    public static IReadOnlyList<ForeverClassTalentPreset> Presets(string cls) => cls switch {
        "shaman"=>[
            Preset("shaman-elemental-restoration","shaman","Elemental + Restoration starter",
                new(){["convection"]=5,["concussion"]=5,["elemental-alacrity"]=3,["call-of-flame"]=3,
                    ["elemental-focus"]=1,["elemental-warding"]=3,["call-of-thunder"]=1,
                    ["improved-healing-wave"]=5,["tidal-focus"]=5,["mindfulness"]=3,["natural-grace"]=3,
                    ["water-shield"]=1,["restorative-totems"]=2,["mana-tide-totem"]=1,["nature-swiftness"]=1},
                "Bolt damage with Restoration mana and self-healing support; 42 points, 9 unspent."),
            Preset("shaman-enhancement-restoration","shaman","Enhancement + Restoration starter",
                new(){["thundering-strikes"]=5,["ancestral-knowledge"]=5,["mental-dexterity"]=3,
                    ["improved-lightning-shield"]=3,["elemental-weapons"]=3,["shamanistic-focus"]=1,
                    ["stormstrike"]=1,["flurry"]=5,["maelstrom-weapon"]=1,
                    ["improved-healing-wave"]=5,["tidal-focus"]=5,["mindfulness"]=3,["natural-grace"]=3,
                    ["water-shield"]=1,["restorative-totems"]=2,["mana-tide-totem"]=1,["nature-swiftness"]=1},
                "Melee and spell weaving with Restoration support; 48 points, 3 unspent."),
            Preset("shaman-restoration-effects","shaman","Restoration focused effect witness",
                new(){["improved-healing-wave"]=5,["tidal-focus"]=5,["mindfulness"]=3,["totemic-focus"]=2,
                    ["ancestral-healing"]=3,["water-shield"]=1,["healing-focus"]=1,["healing-way"]=3,
                    ["nature-swiftness"]=1,["restorative-totems"]=1,["purification"]=5,["riptide"]=1},
                "31 points, 20 unspent. Focused self-healing and defensive effects under incoming damage; a full healer workflow remains separate.")],
        "rogue"=>RoguePresets(),
        "paladin"=>PaladinPresets(),
        _=>[]};

    private static IReadOnlyList<ForeverClassTalentPreset> PaladinPresets() =>
        new[]{"retribution","holy","protection"}.Select(tree=>new ForeverClassTalentPreset(
            "paladin-"+tree,"paladin",CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tree)+" mixed-tree reviewed example",
            ForeverPaladinClassTalents.Starter(tree),"https://theforeverera.com/en/talents/paladin/",
            "1.60.1.70888 topology; official November 3/11 overrides; see component source ledger",ForeverPaladinClassTalents.ReviewedDate,
            tree switch {
                "holy"=>"44 points, 7 unspent. Holy healing and spell effects with Protection and Retribution support; configure a friendly target for healing.",
                "protection"=>"51 points. Shield, incoming-hit and block interactions with Holy and Retribution support; equip a reviewed shield and enable an explicit incoming-hit scenario.",
                _=>"51 points. Retribution seal switching with meaningful Holy and Protection off-tree ranks; a weapon and different secondary seal are required for seal echoes."},
            "Reviewed legal mixed allocation; per-component execution coverage and source confidence are shown separately.",
            "Software example authored from the current sourced tree. No popularity, BiS or optimality claim; the source ledger distinguishes recommendation research from acceptance fixtures.")).ToArray();

    private static IReadOnlyList<ForeverClassTalentPreset> RoguePresets() =>
        new[]{"assassination","combat","subtlety"}.Select(tree=>new ForeverClassTalentPreset(
            "rogue-"+tree,"rogue",CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tree)+" mixed-tree starter",
            ForeverRogueClassTalents.Starter(tree).Ranks,ForeverRogueClassTalents.TreeUrl,
            ForeverRogueClassTalents.SourceBuild,ReviewedAt,
            "Level-60 damage practice with explicit manual weapon assumptions; all three trees remain editable.",
            "Locally reviewed legal rank map; consult per-talent coverage and action/weapon prerequisites.",
            "Software acceptance starter authored against the current sourced tree. No community popularity or optimality claim.")).ToArray();

    private static ForeverClassTalentPreset Preset(string key,string cls,string name,Dictionary<string,int> ranks,string scenario) =>
        new(key,cls,name,ranks,"https://theforeverera.com/en/talents/shaman/",
            "1.60.1.70888-with-official-November-10-2026-overrides",ReviewedAt,scenario,
            "Reviewed partial allocation; consult per-talent coverage before execution.",
            "Locally reviewed software starter assembled from sourced talent ranks and prerequisites. Not a claimed community-popular, BiS or optimized build.");
}
