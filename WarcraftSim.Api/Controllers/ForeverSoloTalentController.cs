using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Warlock;
using WarcraftSim.Data.Forever.Hunter;
using WarcraftSim.Api.Simulation;
using System.Text.Json;

namespace WarcraftSim.Api.Controllers;

/// <summary>CP174 model-backed talent discovery and validation. Only combat-connected
/// talents are exposed; unavailable tree fillers are not manufactured.</summary>
[ApiController]
[Route("api/forever/talents")]
public sealed class ForeverSoloTalentController : ControllerBase
{
    [HttpGet("{classKey}/{specialization}")]
    public IActionResult Catalog(string classKey,string specialization)
    {
        if(!Allowed(classKey,specialization))return BadRequest(new{error="Unsupported class/spec."});
        var rows=classKey switch {
            "mage"=>ForeverMageTalentBuild.SupportedTalentMetadata
                .Select(x=>Describe(classKey,x.Key,x.Tree,x.MaxRank,ForeverMageTalentBuild.RequiredEarlierPoints(x.Key))).ToArray(),
            "warlock"=>ForeverWarlockTalentBuild.SupportedTalentMetadata
                .Select(x=>Describe(classKey,x.Key,x.Tree,x.MaxRank,ForeverWarlockTalentBuild.RequiredEarlierPoints(x.Key))).ToArray(),
            "hunter"=>specialization switch {
                "marksmanship"=>ForeverMarksmanshipHunterTalents.SupportedTalentMetadata
                    .Select(x=>Describe(classKey,x.Key,x.Tree,x.MaxRank,x.RequiredEarlierPoints)).ToArray(),
                "survival"=>ForeverSurvivalHunterTalents.SupportedTalentMetadata
                    .Select(x=>Describe(classKey,x.Key,x.Tree,x.MaxRank,x.RequiredEarlierPoints)).ToArray(),
                _=>ForeverBeastMasteryHunterTalents.SupportedTalentMetadata
                    .Select(x=>Describe(classKey,x.Key,x.Tree,x.MaxRank,x.RequiredEarlierPoints)).ToArray()
            },
            _=>[]
        };
        return Ok(new{classKey,specialization,level=60,maxPoints=51,
            modeledOnly=true,metadataCompleteness="partial-client-trees",
            description="Use /validate to enforce existing engine rules. Metadata is intentionally limited to supported talents; unmodeled filler is not a selectable combat bonus.",
            talents=rows});
    }
    // Verified executable prerequisites only. Historical visual links are not rules.
    private static readonly IReadOnlyDictionary<string,(string Parent,int RequiredRank)> EnginePrerequisites =
        new Dictionary<string,(string,int)>(StringComparer.OrdinalIgnoreCase) {
            ["mage:arcane-meditation"]=("arcane-concentration",1),
            ["mage:arcane-power"]=("presence-of-mind",1),
            ["mage:arcane-wrath"]=("arcane-blast",1),
            ["mage:heating-up"]=("pyroblast",1),
            ["mage:combustion"]=("critical-mass",3),
            ["mage:ice-barrier"]=("cold-snap",1),
            ["mage:fingers-of-frost"]=("ice-lance",1),
            ["warlock:fel-domination"]=("master-summoner",2),
            ["warlock:pyroclasm"]=("intensity",3),
            ["warlock:fire-and-brimstone"]=("conflagrate",1),
            ["warlock:wrack"]=("siphon-life",1),
            ["warlock:demonic-pact"]=("soul-link",1),
            ["warlock:soul-link"]=("demonic-sacrifice",1),
            ["warlock:incinerate"]=("bane-of-havoc",1),
            ["hunter:sniper-shot"]=("trueshot-aura",1),
            ["hunter:mortal-shots"]=("careful-aim",5),
            ["hunter:rapid-recuperation"]=("rapid-killing",2),
            ["hunter:frenzy"]=("ferocity",5),
            ["hunter:intimidation"]=("bestial-swiftness",1),
            ["hunter:bestial-wrath"]=("intimidation",1),
            ["hunter:counterattack"]=("deterrence",1),
            ["hunter:lacerating-strikes"]=("expose-prey",2)
        };

    private static object Describe(string cls,string key,string tree,int maxRank,int? requiredEarlierPoints)
    {
        var visual=ForeverTalentVisualReferenceCatalog.Match(cls,tree,key,maxRank);
        (int Row,int Column) corrected=default;
        var mageHotfix=cls=="mage" &&
            ForeverMageTalentBuild.HotfixPositions.TryGetValue(key,out corrected);
        int? row=mageHotfix?corrected.Row:visual?.Row;
        int? column=mageHotfix?corrected.Column:visual?.Column;
        var known=EnginePrerequisites.TryGetValue(cls+":"+key,out var dependency);
        return new {
            key,tree,maxRank,requiredEarlierPoints,
            enginePrerequisiteKey=known?dependency.Parent:null,
            enginePrerequisiteRank=known?dependency.RequiredRank:0,
            name=visual?.Name ?? string.Join(" ",key.Split('-').Select(x=>
                char.ToUpperInvariant(x[0])+x[1..])),
            row,column,
            iconKey=visual?.IconKey,
            iconStatus=visual is null?"unresolved":ForeverTalentVisualReferenceCatalog.VerificationStatus,
            descriptionTemplate=visual?.DescriptionTemplate,
            historicalRankDescriptions=visual is null?[]:
                ForeverTalentVisualReferenceCatalog.HistoricalRankDescriptions(visual).ToArray(),
            prerequisiteKey=visual?.PrerequisiteKey,
            visualSourceStatus=visual is null?"unmatched-engine-talent":
                mageHotfix?"engine-hotfix-position-historical-tooltip":
                    ForeverTalentVisualReferenceCatalog.VerificationStatus,
            visualSourceCommit=visual is null?null:ForeverTalentVisualReferenceCatalog.SourceCommit,
            visualSourceUrl=visual is null?null:ForeverTalentVisualReferenceCatalog.SourceUrl
        };
    }

    [HttpPost("{classKey}/{specialization}/validate")]
    public IActionResult Validate(string classKey,string specialization,[FromBody] JsonElement selection)
    {
        if(!Allowed(classKey,specialization))return BadRequest(new{error="Unsupported class/spec."});
        try {
            var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            switch(classKey) {
                case "mage":
                    (selection.Deserialize<ForeverMageTalentBuild>(options)??throw new ArgumentException("Missing talents")).Validate(specialization);
                    break;
                case "warlock":
                    (selection.Deserialize<ForeverWarlockTalentBuild>(options)??throw new ArgumentException("Missing talents")).Validate(60,specialization);
                    break;
                case "hunter":
                    switch(specialization){
                        case "marksmanship": (selection.Deserialize<ForeverMarksmanshipHunterTalents>(options)??throw new ArgumentException("Missing talents")).Validate();break;
                        case "survival": (selection.Deserialize<ForeverSurvivalHunterTalents>(options)??throw new ArgumentException("Missing talents")).Validate();break;
                        default: (selection.Deserialize<ForeverBeastMasteryHunterTalents>(options)??throw new ArgumentException("Missing talents")).Validate();break;
                    }
                    break;
            }
            return Ok(new{valid=true});
        }catch(Exception ex) when(ex is ArgumentException or JsonException or InvalidOperationException){
            return BadRequest(new{valid=false,error=ex.Message});
        }
    }
    private static bool Allowed(string c,string s)=> c switch {
        "mage"=>s is "arcane" or "fire" or "frost",
        "warlock"=>s is "affliction" or "demonology" or "destruction",
        "hunter"=>s is "marksmanship" or "survival" or "beast-mastery",
        _=>false
    };
}