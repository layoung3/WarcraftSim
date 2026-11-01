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
    /// <summary>Source-labelled, nonexecuting historical talents for itemized allocation.</summary>
    [HttpGet("{classKey}/{specialization}/itemized-catalog")]
    public IActionResult ItemizedCatalog(string classKey,string specialization)
    {
        if(!Allowed(classKey,specialization))return BadRequest(new{error="Unsupported class/spec."});
        var engine=(OkObjectResult)Catalog(classKey,specialization);
        using var data=JsonDocument.Parse(JsonSerializer.Serialize(engine.Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var engineKeys=data.RootElement.GetProperty("talents").EnumerateArray()
            .Select(x=>x.GetProperty("key").GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var source=ForeverTalentVisualReferenceCatalog.Records
            .Where(x=>x.ClassKey.Equals(classKey,StringComparison.OrdinalIgnoreCase) &&
                !engineKeys.Contains(x.Key) &&
                !(classKey=="mage"&&new[]{"arcane-focus","improved-fireball","elemental-precision","winter-s-chill"}.Contains(x.Key)) &&
                !(classKey=="hunter"&&new[]{"predator-s-edge","survivalist-s-discipline"}.Contains(x.Key)) &&
                x.MaxRank is >=1 and <=5 && x.Row is >=1 and <=7 &&
                x.Column is >=1 and <=4)
            .Select(x=>new{
                key=x.Key,name=x.Name,tree=x.Tree,maxRank=x.MaxRank,
                row=(int?)x.Row,column=(int?)x.Column,modeled=false,
                requiredEarlierPoints=(x.Row-1)*5,
                enginePrerequisiteKey=x.PrerequisiteKey,
                enginePrerequisiteRank=x.PrerequisiteKey is null?0:
                    ForeverTalentVisualReferenceCatalog.Records
                        .FirstOrDefault(y=>y.ClassKey==classKey&&y.Key==x.PrerequisiteKey)?.MaxRank??1,
                iconKey=x.IconKey,descriptionTemplate=x.DescriptionTemplate,
                historicalRankDescriptions=ForeverTalentVisualReferenceCatalog.HistoricalRankDescriptions(x),
                visualSourceStatus=ForeverTalentVisualReferenceCatalog.VerificationStatus,
                visualSourceCommit=ForeverTalentVisualReferenceCatalog.SourceCommit,
                visualSourceUrl=ForeverTalentVisualReferenceCatalog.SourceUrl
            }).ToArray();
        return Ok(new{
            classKey,specialization,unmodeled=source,
            note="Historical-only talents count toward itemized points; do not grant combat effects. Positions and prerequisites remain provisional until current client verified."
        });
    }

    [HttpGet("{classKey}/{specialization}/default-itemized")]
    public IActionResult DefaultItemized(string classKey,string specialization)
    {
        if(!Allowed(classKey,specialization))
            return BadRequest(new {error="Unsupported class/spec."});
        try
        {
            var preset=ForeverItemizedPresetCatalog.Load(classKey,specialization);
            var validation=ValidateItemized(classKey,specialization,
                JsonSerializer.SerializeToElement(new{ranks=preset.Ranks},
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            if(validation is not OkObjectResult)
                throw new InvalidOperationException($"Curated preset {classKey}/{specialization} failed the strict rank, tier or prerequisite gate.");
            return Ok(preset);
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException)
        {return BadRequest(new{error=ex.Message});}
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
        // Explicit current rename reconciliation. Older tooltip rank caps are
        // not imported when they no longer match executable engine ranks.
        visual ??= (cls,tree,key) switch {
            ("mage","fire","accelerant")=>ForeverTalentVisualReferenceCatalog.Records
                .FirstOrDefault(x=>x.ClassKey=="mage"&&x.Tree=="fire"&&x.Key=="improved-fireball"),
            ("mage","frost","precision")=>ForeverTalentVisualReferenceCatalog.Records
                .FirstOrDefault(x=>x.ClassKey=="mage"&&x.Tree=="frost"&&x.Key=="elemental-precision"),
            ("mage","frost","winters-chill")=>ForeverTalentVisualReferenceCatalog.Records
                .FirstOrDefault(x=>x.ClassKey=="mage"&&x.Tree=="frost"&&x.Key=="winter-s-chill"),
            ("mage","fire","incineration")=>ForeverTalentVisualReferenceCatalog.Records
                .FirstOrDefault(x=>x.ClassKey=="mage"&&x.Tree=="fire"&&x.Key=="incineration"),
            ("mage","fire","critical-mass")=>ForeverTalentVisualReferenceCatalog.Records
                .FirstOrDefault(x=>x.ClassKey=="mage"&&x.Tree=="fire"&&x.Key=="critical-mass"),
            ("hunter","survival","predators-edge")=>ForeverTalentVisualReferenceCatalog.Records
                .FirstOrDefault(x=>x.ClassKey=="hunter"&&x.Tree=="survival"&&x.Key=="predator-s-edge"),
            ("hunter","survival","survivalists-discipline")=>ForeverTalentVisualReferenceCatalog.Records
                .FirstOrDefault(x=>x.ClassKey=="hunter"&&x.Tree=="survival"&&x.Key=="survivalist-s-discipline"),
            _=>null
        };
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
            historicalRankDescriptions=visual is null||visual.MaxRank!=maxRank?[]:
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
    private sealed record ItemizedTalent(string Key,string Tree,int MaxRank,int? Row,string? Prerequisite,int PrerequisiteRank,bool Modeled,string Source);
    /// <summary>Strict itemized-only validation; synthetic tree totals confer no tier access.</summary>
    [HttpPost("{classKey}/{specialization}/validate-itemized")]
    public IActionResult ValidateItemized(string classKey,string specialization,[FromBody] JsonElement selection)
    {
        if(!Allowed(classKey,specialization))return BadRequest(new{error="Unsupported class/spec."});
        try
        {
            if(selection.ValueKind!=JsonValueKind.Object ||
                !selection.TryGetProperty("ranks",out var ranked) || ranked.ValueKind!=JsonValueKind.Object)
                throw new ArgumentException("Itemized talent ranks are required.");
            // The existing engine metadata is authoritative for all executable ranks.
            var current=(OkObjectResult)Catalog(classKey,specialization);
            using var document=JsonDocument.Parse(JsonSerializer.Serialize(current.Value,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var entries=new Dictionary<string,ItemizedTalent>(StringComparer.OrdinalIgnoreCase);
            foreach(var t in document.RootElement.GetProperty("talents").EnumerateArray())
            {
                var key=t.GetProperty("key").GetString()!;
                var tree=t.GetProperty("tree").GetString()!;
                var row=t.GetProperty("row");
                var parent=t.GetProperty("enginePrerequisiteKey");
                entries.Add(key,new(key,tree,t.GetProperty("maxRank").GetInt32(),
                    row.ValueKind==JsonValueKind.Number?row.GetInt32():null,
                    parent.ValueKind==JsonValueKind.String?parent.GetString():null,
                    t.GetProperty("enginePrerequisiteRank").GetInt32(),true,"engine-model"));
            }
            // Historical-only entries may spend points, but never grant invented gameplay.
            // The removed 70685 Arcane Focus explicitly cannot be selected.
            foreach(var visual in ForeverTalentVisualReferenceCatalog.Records)
            {
                if(!visual.ClassKey.Equals(classKey,StringComparison.OrdinalIgnoreCase) ||
                    entries.ContainsKey(visual.Key) ||
                    (classKey=="mage"&&new[]{"arcane-focus","improved-fireball","elemental-precision","winter-s-chill"}.Contains(visual.Key)) ||
                    (classKey=="hunter"&&new[]{"predator-s-edge","survivalist-s-discipline"}.Contains(visual.Key)) ||
                    visual.Row is <1 or >7 || visual.Column is <1 or >4 ||
                    visual.MaxRank is <1 or >5)continue;
                entries.Add(visual.Key,new(visual.Key,visual.Tree,visual.MaxRank,
                    visual.Row,visual.PrerequisiteKey,0,false,
                    ForeverTalentVisualReferenceCatalog.VerificationStatus));
            }
            var ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            var byTree=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            foreach(var choice in ranked.EnumerateObject())
            {
                if(!entries.TryGetValue(choice.Name,out var t) ||
                    choice.Value.ValueKind!=JsonValueKind.Number ||
                    !choice.Value.TryGetInt32(out var rank) || rank<0 || rank>t.MaxRank)
                    throw new ArgumentException($"Unrecognized talent or invalid rank: {choice.Name}.");
                if(rank==0)continue;
                ranks.Add(t.Key,rank);
                byTree[t.Tree]=byTree.GetValueOrDefault(t.Tree)+rank;
            }
            var spent=ranks.Values.Sum();
            if(spent>51)throw new ArgumentException($"Level 60 permits 51 talent points; received {spent} ranked points.");
            foreach(var (key,rank) in ranks)
            {
                var t=entries[key];
                if(t.Row is null)
                    throw new ArgumentException($"No verified tree row is available for {key}; cannot validate its tier.");
                var threshold=(t.Row.Value-1)*5;
                var earlier=ranks.Where(x=>entries[x.Key].Tree==t.Tree &&
                    entries[x.Key].Row is int row && row<t.Row.Value).Sum(x=>x.Value);
                if(earlier<threshold)
                    throw new ArgumentException($"{key} needs {threshold} points in earlier {t.Tree} rows, found {earlier}.");
                if(EnginePrerequisites.TryGetValue(classKey+":"+key,out var known) &&
                    ranks.GetValueOrDefault(known.Parent)<known.RequiredRank)
                    throw new ArgumentException($"{key} needs {known.RequiredRank} ranks of {known.Parent}.");
                if(!t.Modeled && t.Prerequisite is not null &&
                    entries.TryGetValue(t.Prerequisite,out var parent) &&
                    ranks.GetValueOrDefault(parent.Key)<parent.MaxRank)
                    throw new ArgumentException($"{key} needs {parent.MaxRank} ranks of historically linked {parent.Key} (provisional).");
            }
            // Any provided historical aggregate must match actual rank sums exactly.
            var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
                ["arcanePointsSpent"]="arcane",["firePointsSpent"]="fire",["frostPointsSpent"]="frost",
                ["afflictionPointsSpent"]="affliction",["demonologyPointsSpent"]="demonology",
                ["destructionPointsSpent"]="destruction",["marksmanshipPointsSpent"]="marksmanship",
                ["beastMasteryPointsSpent"]="beast-mastery",["survivalPointsSpent"]="survival"};
            foreach(var property in selection.EnumerateObject())
                if(names.TryGetValue(property.Name,out var tree) &&
                   (property.Value.ValueKind!=JsonValueKind.Number ||
                    !property.Value.TryGetInt32(out var count) || count!=byTree.GetValueOrDefault(tree)))
                    throw new ArgumentException($"Synthetic aggregate {property.Name} differs from ranked {tree} talent points.");
            return Ok(new{valid=true,pointsSpent=spent,remaining=51-spent,
                treePoints=byTree,
                unmodeledSelections=ranks.Where(x=>!entries[x.Key].Modeled)
                    .Select(x=>new{key=x.Key,rank=x.Value,source=entries[x.Key].Source}).ToArray(),
                sourceStatus="engine-ranks-plus-pinned-historical-unmodeled-provisional",
                note="Historical-only talents consume real itemized points but are not yet executable by all typed runners."});
        }
        catch(Exception ex) when(ex is ArgumentException or JsonException or InvalidOperationException)
        {
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