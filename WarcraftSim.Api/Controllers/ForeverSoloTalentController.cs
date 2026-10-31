using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Warlock;
using WarcraftSim.Data.Forever.Hunter;
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
            "mage"=>ForeverMageTalentBuild.SupportedTalentMetadata.Select(x=>new {key=x.Key,tree=x.Tree,maxRank=x.MaxRank,requiredEarlierPoints=(int?)null}).Cast<object>().ToArray(),
            "warlock"=>ForeverWarlockTalentBuild.SupportedTalentMetadata.Select(x=>new {key=x.Key,tree=x.Tree,maxRank=x.MaxRank,requiredEarlierPoints=(int?)null}).Cast<object>().ToArray(),
            "hunter"=>specialization switch {
                "marksmanship"=>ForeverMarksmanshipHunterTalents.SupportedTalentMetadata.Select(x=>new {key=x.Key,tree=x.Tree,maxRank=x.MaxRank,requiredEarlierPoints=(int?)x.RequiredEarlierPoints}).Cast<object>().ToArray(),
                "survival"=>ForeverSurvivalHunterTalents.SupportedTalentMetadata.Select(x=>new {key=x.Key,tree=x.Tree,maxRank=x.MaxRank,requiredEarlierPoints=(int?)x.RequiredEarlierPoints}).Cast<object>().ToArray(),
                _=>ForeverBeastMasteryHunterTalents.SupportedTalentMetadata.Select(x=>new {key=x.Key,tree=x.Tree,maxRank=x.MaxRank,requiredEarlierPoints=(int?)x.RequiredEarlierPoints}).Cast<object>().ToArray()
            },
            _=>Array.Empty<object>()
        };
        return Ok(new{classKey,specialization,level=60,maxPoints=51,
            modeledOnly=true,metadataCompleteness="partial-client-trees",
            description="Use /validate to enforce existing engine rules. Metadata is intentionally limited to supported talents; unmodeled filler is not a selectable combat bonus.",
            talents=rows});
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