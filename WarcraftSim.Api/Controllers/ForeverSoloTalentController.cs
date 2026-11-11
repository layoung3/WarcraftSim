using WarcraftSim.Data.Forever.Rogue;
using WarcraftSim.Data.Forever.Priest;
using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Data.Forever.Shaman;
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
    /// <summary>
    /// Live coverage numbers from engine contracts and the pinned historical
    /// visual dataset. No older description is upgraded to a verified 70707
    /// tooltip, and a modeled rank is not called full mechanical certification.
    /// </summary>
    [HttpGet("source-coverage")]
    public IActionResult SourceCoverage()
    {
        var trees=new[]{
            (Class:"warrior",Tree:"arms"),(Class:"warrior",Tree:"fury"),
            (Class:"warrior",Tree:"protection"),
            (Class:"mage",Tree:"arcane"),(Class:"mage",Tree:"fire"),
            (Class:"mage",Tree:"frost"),
            (Class:"warlock",Tree:"affliction"),(Class:"warlock",Tree:"demonology"),
            (Class:"warlock",Tree:"destruction"),
            (Class:"hunter",Tree:"beast-mastery"),(Class:"hunter",Tree:"marksmanship"),
            (Class:"hunter",Tree:"survival")
        };
        var rows=trees.Select(x=>{
            var executable=x.Class switch {
                "warrior"=>ForeverWarriorTalentCatalog.Talents
                    .Where(t=>t.Tree==x.Tree&&t.SimulationStatus=="modeled")
                    .Select(t=>t.Id).ToArray(),
                "mage"=>ForeverMageTalentBuild.SupportedTalentMetadata
                    .Where(t=>t.Tree==x.Tree).Select(t=>t.Key).ToArray(),
                "warlock"=>ForeverWarlockTalentBuild.SupportedTalentMetadata
                    .Where(t=>t.Tree==x.Tree).Select(t=>t.Key).ToArray(),
                _=>x.Tree switch {
                    "marksmanship"=>ForeverMarksmanshipHunterTalents.SupportedTalentMetadata.Select(t=>t.Key).ToArray(),
                    "survival"=>ForeverSurvivalHunterTalents.SupportedTalentMetadata.Select(t=>t.Key).ToArray(),
                    _=>ForeverBeastMasteryHunterTalents.SupportedTalentMetadata.Where(t=>t.Tree==x.Tree).Select(t=>t.Key).ToArray()
                }
            };
            var engineSet=executable.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var historical=ForeverTalentVisualReferenceCatalog.Records
                .Where(t=>t.ClassKey==x.Class&&t.Tree==x.Tree).ToArray();
            var warrior=x.Class=="warrior"?
                ForeverWarriorTalentCatalog.Talents.Where(t=>t.Tree==x.Tree).ToArray():[];
            return new {
                classKey=x.Class,tree=x.Tree,
                engineDeclaredConnectedKeys=executable.Length,
                historicalVisualRecords=historical.Length,
                historicalOnlyKeys=historical.Count(t=>!engineSet.Contains(t.Key)),
                warriorExplicitModeled=warrior.Count(t=>t.SimulationStatus=="modeled"),
                warriorExplicitPartial=warrior.Count(t=>t.SimulationStatus=="partial"),
                warriorExplicitUnsupported=warrior.Count(t=>t.SimulationStatus=="not-modeled"),
                current70707TooltipVerifiedInRepository=0,
                mechanicalCertification="per-talent-execution-not-universally-certified",
                tooltipProvenance="historical-visual-reference; not verified as current client tooltip"
            };
        }).ToArray();
        return Ok(new {
            clientTargetBuild="1.60.1.70707",
            historicalVisualReferenceCommit=ForeverTalentVisualReferenceCatalog.SourceCommit,
            historicalVisualReferenceStatus=ForeverTalentVisualReferenceCatalog.VerificationStatus,
            rows,
            note="These are actual local coverage counts. Current 70707 rank-specific tooltip export and complete active/passive certification require a pinned reviewed client source; none is silently invented."
        });
    }

    [HttpGet("{classKey}/{specialization}")]
    public IActionResult Catalog(string classKey,string specialization)
    {
        if(!Allowed(classKey,specialization))return BadRequest(new{error="Unsupported class/spec."});
        var rows=classKey switch {
            "druid" or "shaman" or "priest" or "rogue"=>ForeverHistoricalCasterTalentCatalog.ForSpecialization(classKey,specialization)
                .Select(x=>Describe(classKey,x.Key,x.Tree,x.MaxRank,(x.Row-1)*5,specialization)).ToArray(),
            "mage"=>ForeverMageTalentBuild.SupportedTalentMetadata
                .Select(x=>Describe(classKey,x.Key,x.Tree,x.MaxRank,ForeverMageTalentBuild.RequiredEarlierPoints(x.Key))).ToArray(),
            "warlock"=>ForeverWarlockTalentBuild.SupportedTalentMetadata
                .Select(x=>Describe(classKey,x.Key,x.Tree,x.MaxRank,ForeverWarlockTalentBuild.RequiredEarlierPoints(x.Key))).ToArray(),
            "hunter"=>specialization switch {
                "marksmanship"=>ForeverMarksmanshipHunterTalents.SupportedTalentMetadata
                    .Concat(ForeverSurvivalHunterTalents.SupportedTalentMetadata)
                    .Concat(ForeverBeastMasteryHunterTalents.SupportedTalentMetadata
                        .Where(x=>x.Tree=="beast-mastery"))
                    .Select(x=>Describe(classKey,x.Key,x.Tree,x.MaxRank,x.RequiredEarlierPoints)).ToArray(),
                "survival"=>ForeverSurvivalHunterTalents.SupportedTalentMetadata
                    .Concat(ForeverBeastMasteryHunterTalents.SupportedTalentMetadata
                        .Where(x=>x.Tree=="beast-mastery"))
                    .Concat(ForeverMarksmanshipHunterTalents.SupportedTalentMetadata)
                    .Select(x=>Describe(classKey,x.Key,x.Tree,x.MaxRank,x.RequiredEarlierPoints)).ToArray(),
                _=>ForeverBeastMasteryHunterTalents.SupportedTalentMetadata
                    .Concat(ForeverMarksmanshipHunterTalents.SupportedTalentMetadata
                        .Where(x=>x.Key is not ("lethal-attacks" or "improved-concussive-shot")))
                    .Concat(ForeverSurvivalHunterTalents.SupportedTalentMetadata)
                    .Select(x=>Describe(classKey,x.Key,x.Tree,x.MaxRank,x.RequiredEarlierPoints)).ToArray()
            },
            _=>[]
        };
        return Ok(new{classKey,specialization,level=60,maxPoints=51,
            modeledOnly=!(classKey=="druid" && specialization=="feral"),metadataCompleteness=classKey=="hunter"?"hunter-70753-reconciled-talents":"partial-client-trees",
            description="Use /validate to enforce existing engine rules. Metadata is intentionally limited to supported talents; unmodeled filler is not a selectable combat bonus.",
            talents=rows});
    }
    /// <summary>Source-labelled, nonexecuting historical talents for itemized allocation.</summary>
    [HttpGet("{classKey}/{specialization}/itemized-catalog")]
    public IActionResult ItemizedCatalog(string classKey,string specialization)
    {
        if(!Allowed(classKey,specialization))return BadRequest(new{error="Unsupported class/spec."});
        if(classKey=="druid" && specialization=="feral")return Ok(new{classKey,specialization,unmodeled=Array.Empty<object>(),
            note="Restricted fixed Cat supports exactly fourteen source-backed keys and a partial historical-provisional 0/31/5 default allocation: 36 allocated, 15 unspent. Legal underallocations remain allowed; no unmodeled fillers or automatic completion."});
        if(classKey=="rogue")return Ok(new{classKey,specialization,unmodeled=Array.Empty<object>(),
            note=specialization=="combat"?"Restricted fixed-sword Combat supports exactly eleven historical keys, including the explicit Precision cap-three compatibility contract; no unmodeled fillers or curated default are available.":"Restricted Assassination supports exactly twelve historical source-backed keys; no unmodeled fillers or other Rogue specializations are available."});
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
        // Backend admission does not create or discover a curated Combat preset.
        if(classKey=="rogue" && specialization=="combat")
            return BadRequest(new {error="Unsupported class/spec."});
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
            ["mage:arcane-meditation"]=("arcane-concentration",5),
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

    private static object Describe(string cls,string key,string tree,int maxRank,int? requiredEarlierPoints,string? specialization=null)
    {
        var historicalCaster=specialization is null?ForeverHistoricalCasterTalentCatalog.Find(cls,key):
            ForeverHistoricalCasterTalentCatalog.Find(cls,specialization,key);
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
            // A historical visual is allowed for 70707 Arcane Resilience's
            // NEW three-rank node, but its old two-rank tooltip is NEVER reused.
            ("mage","arcane","arcane-resilience")=>ForeverTalentVisualReferenceCatalog.Records
                .FirstOrDefault(x=>x.ClassKey=="mage"&&x.Tree=="arcane"&&
                    x.Key=="arcane-resilience"&&x.MaxRank==2&&maxRank==3),
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
        var warlockCoverage=cls=="warlock"?Warlock70753TalentCoverageCatalog.Find(key):null;
        var hunterCoverage=cls=="hunter"?Hunter70753TalentCoverageCatalog.Find(key):null;
        int? row=historicalCaster?.Row ?? warlockCoverage?.Row ?? hunterCoverage?.Row ?? (mageHotfix?corrected.Row:visual?.Row);
        int? column=historicalCaster?.Column ?? (mageHotfix?corrected.Column:visual?.Column);
        var known=EnginePrerequisites.TryGetValue(cls+":"+key,out var dependency);
        var mageCoverage=cls=="mage"?Mage70707TalentCoverageCatalog.Find(key):null;
        return new {
            key,tree,maxRank,requiredEarlierPoints,
            simulationStatus=cls=="druid" && specialization=="feral"?
                (key is "feral-swiftness" or "furor"?"inactive-in-fixed-cat":"fixed-cat-partial"):historicalCaster?.ClassKey=="rogue"?
                (specialization=="combat" && key is "lightning-reflexes" or "puncturing-wounds" or "endurance"?"inactive-in-fixed-sword-combat":"partial"):historicalCaster?.ClassKey=="priest" && historicalCaster.Tree=="discipline"?
                "unreachable-in-restricted-shadow-allocation":mageCoverage?.SimulationStatus??warlockCoverage?.SimulationStatus??
                hunterCoverage?.SimulationStatus,
            implementationStatus=mageCoverage is not null?
                Mage70707TalentCoverageCatalog.ImplementationStatus(mageCoverage):
                warlockCoverage is not null?
                Warlock70753TalentCoverageCatalog.ImplementationStatus(warlockCoverage):
                hunterCoverage is not null?
                Hunter70753TalentCoverageCatalog.ImplementationStatus(hunterCoverage):null,
            dataConfidence=cls=="druid" && specialization=="feral"?"historical-provisional":mageCoverage is null && warlockCoverage is null && hunterCoverage is null?null:"provisional",
            sourceBuild=mageCoverage is not null?Mage70707TalentCoverageCatalog.SourceBuild:
                warlockCoverage is not null?Warlock70753TalentCoverageCatalog.SourceBuild:
                hunterCoverage is not null?Hunter70753TalentCoverageCatalog.SourceBuild:null,
            historicalValidationSourceStatus=historicalCaster is null?null:ForeverHistoricalCasterTalentCatalog.SourceStatus,
            historicalValidationSourceCommit=historicalCaster is null?null:ForeverHistoricalCasterTalentCatalog.SourceCommit,
            sourceUrl=historicalCaster is not null?ForeverHistoricalCasterTalentCatalog.SourceUrl(cls):mageCoverage is not null?Mage70707TalentCoverageCatalog.SourceUrl:
                warlockCoverage is not null?Warlock70753TalentCoverageCatalog.SourceUrl:
                hunterCoverage is not null?Hunter70753TalentCoverageCatalog.SourceUrl:null,
            sourceReviewedAt=mageCoverage is not null?Mage70707TalentCoverageCatalog.ReviewedDate:
                warlockCoverage is not null?Warlock70753TalentCoverageCatalog.ReviewedDate:
                hunterCoverage is not null?Hunter70753TalentCoverageCatalog.ReviewedDate:null,
            knownLimitations=cls=="druid" && specialization=="feral"?
                ForeverHistoricalCasterTalentCatalog.FeralLimitation(key):historicalCaster?.ClassKey=="rogue"?
                string.Join(" ",ForeverHistoricalCasterTalentCatalog.RogueNotes(specialization??"assassination")):historicalCaster?.ClassKey=="priest"?
                "Historical validation metadata only. No admitted earlier-row Discipline fillers; cross-tree cooldown allocations remain unreachable. Penance is not advertised as an executable Shadow action.":mageCoverage?.RemainingLimitations??warlockCoverage?.Limitations??
                hunterCoverage?.Limitations,
            gapCategories=warlockCoverage?.Gaps??hunterCoverage?.Gaps,
            enginePrerequisiteKey=historicalCaster?.PrerequisiteKey ?? (known?dependency.Parent:null),
            enginePrerequisiteRank=historicalCaster?.PrerequisiteRank ?? (known?dependency.RequiredRank:0),
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
                visual.MaxRank!=maxRank?"engine-70707-cap-historical-visual":
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
        // Rogue and Feral expose only their strict historical allocation paths. Calling the
        // validation endpoint cannot admit legacy synthetic aggregates or aliases.
        if(classKey=="rogue" || classKey=="druid" && specialization=="feral")return ValidateItemized(classKey,specialization,selection);
        try {
            var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            switch(classKey) {
                case "priest":
                    var shadow=selection.Deserialize<ForeverPriestShadowTalentBuild>(options)??throw new ArgumentException("Missing talents");
                    if(shadow.Ranks is null)throw new ArgumentException("Missing talent ranks");
                    shadow.Validate(60);
                    break;
                case "druid":
                    (selection.Deserialize<ForeverBalanceDruidTalents>(options)??throw new ArgumentException("Missing talents")).Validate();
                    break;
                case "shaman":
                    if(specialization=="enhancement")
                    {
                        var enhancement=selection.Deserialize<ForeverEnhancementShamanTalents>(options)
                            ??throw new ArgumentException("Missing talents");
                        if(enhancement.Ranks is null)throw new ArgumentException("Missing talent ranks");
                        enhancement.Validate(60);
                    }
                    else
                        (selection.Deserialize<ForeverElementalShamanTalents>(options)??throw new ArgumentException("Missing talents")).Validate();
                    break;
                case "mage":
                    (selection.Deserialize<ForeverMageTalentBuild>(options)??throw new ArgumentException("Missing talents")).Validate(specialization);
                    break;
                case "warlock":
                    (selection.Deserialize<ForeverWarlockTalentBuild>(options)??throw new ArgumentException("Missing talents")).Validate(60,specialization);
                    break;
                case "hunter":
                    // Unified itemized builds transport modeled off-tree ranks
                    // inside the selected preset's typed dictionary. Normalize
                    // into the same tree-specific owners used by its runtime.
                    // Unsupported effects still fail closed, not silently inert.
                    switch(specialization){
                        case "marksmanship":
                        {
                            var mm=selection.Deserialize<ForeverMarksmanshipHunterTalents>(options)
                                ??throw new ArgumentException("Missing talents");
                            var ranks=new Dictionary<string,int>(mm.Ranks,
                                StringComparer.OrdinalIgnoreCase);
                            var beastKeys=ForeverBeastMasteryHunterTalents.SupportedTalentMetadata
                                .Where(x=>x.Tree=="beast-mastery")
                                .Select(x=>x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var petRanks=ranks.Where(x=>beastKeys.Contains(x.Key))
                                .ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
                            foreach(var key in petRanks.Keys)ranks.Remove(key);
                            var unsupportedBm=new[]{
                                "improved-mend-pet",
                                "improved-aspect-of-the-monkey","pathfinding"
                            };
                            if(unsupportedBm.Any(key=>petRanks.GetValueOrDefault(key)>0))
                                throw new ArgumentException(
                                    "Selected Beast Mastery talent cannot execute in the Marksmanship runner.");
                            var svKeys=ForeverSurvivalHunterTalents.SupportedTalentMetadata
                                .Select(x=>x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var svRanks=ranks.Where(x=>svKeys.Contains(x.Key))
                                .ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
                            foreach(var key in svRanks.Keys)ranks.Remove(key);
                            mm.Ranks=ranks;
                            mm.Validate();
                            new ForeverSurvivalHunterTalents{Ranks=svRanks}.Validate();
                            new ForeverBeastMasteryHunterTalents{Ranks=petRanks}.Validate();
                            if(ranks.Values.Sum()+svRanks.Values.Sum()+petRanks.Values.Sum()>51 ||
                               svRanks.Values.Sum()>mm.SurvivalPointsSpent ||
                               petRanks.Values.Sum()>mm.BeastMasteryPointsSpent)
                                throw new ArgumentException("Off-tree Hunter ranks exceed real allocation.");
                            break;
                        }
                        case "survival":
                        {
                            var survival=selection.Deserialize<ForeverSurvivalHunterTalents>(options)
                                ??throw new ArgumentException("Missing talents");
                            var petKeys=ForeverBeastMasteryHunterTalents.SupportedTalentMetadata
                                .Where(x=>x.Tree=="beast-mastery")
                                .Select(x=>x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var petRanks=survival.Ranks.Where(x=>petKeys.Contains(x.Key))
                                .ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
                            survival.Ranks=survival.Ranks.Where(x=>!petKeys.Contains(x.Key))
                                .ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
                            var mmKeys=ForeverMarksmanshipHunterTalents.SupportedTalentMetadata
                                .Select(x=>x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var mmRanks=survival.Ranks.Where(x=>mmKeys.Contains(x.Key))
                                .ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
                            foreach(var key in mmRanks.Keys)survival.Ranks.Remove(key);
                            survival.Validate();
                            new ForeverBeastMasteryHunterTalents{Ranks=petRanks}.Validate();
                            new ForeverMarksmanshipHunterTalents{
                                Ranks=mmRanks,MarksmanshipPointsSpent=mmRanks.Values.Sum(),
                                SurvivalPointsSpent=survival.Ranks.Values.Sum(),
                                BeastMasteryPointsSpent=petRanks.Values.Sum()
                            }.Validate();
                            if(new[]{"deadly-aspects","improved-mend-pet",
                                     "improved-aspect-of-the-monkey","pathfinding"}
                               .Any(key=>petRanks.GetValueOrDefault(key)>0))
                                throw new ArgumentException(
                                    "Selected Beast Mastery talent cannot execute in the Survival runner.");
                            if(survival.Ranks.Values.Sum()+petRanks.Values.Sum()+mmRanks.Values.Sum()>51)
                                throw new ArgumentException("Survival plus BM and MM talents exceed 51 actual ranks.");
                            break;
                        }
                        default:
                        {
                            var bm=selection.Deserialize<ForeverBeastMasteryHunterTalents>(options)
                                ??throw new ArgumentException("Missing talents");
                            var svKeys=ForeverSurvivalHunterTalents.SupportedTalentMetadata
                                .Select(x=>x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var svRanks=bm.Ranks.Where(x=>svKeys.Contains(x.Key))
                                .ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
                            foreach(var key in svRanks.Keys)bm.Ranks.Remove(key);
                            new ForeverSurvivalHunterTalents{Ranks=svRanks}.Validate();
                            var mmKeys=ForeverMarksmanshipHunterTalents.SupportedTalentMetadata
                                .Select(x=>x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var mmRanks=bm.Ranks.Where(x=>mmKeys.Contains(x.Key))
                                .ToDictionary(x=>x.Key,x=>x.Value,StringComparer.OrdinalIgnoreCase);
                            foreach(var key in mmRanks.Keys)
                                if(key is not ("lethal-attacks" or "improved-concussive-shot"))
                                    bm.Ranks.Remove(key);
                            if(bm.Ranks.Values.Sum()+svRanks.Values.Sum()+
                               mmRanks.Where(x=>x.Key is not ("lethal-attacks" or "improved-concussive-shot"))
                                   .Sum(x=>x.Value)>51)
                                throw new ArgumentException("Invalid BM/Survival/Marksmanship allocation.");
                            bm.Validate();
                            new ForeverMarksmanshipHunterTalents {
                                Ranks=mmRanks,MarksmanshipPointsSpent=mmRanks.Values.Sum(),
                                SurvivalPointsSpent=svRanks.Values.Sum(),
                                BeastMasteryPointsSpent=bm.Ranks
                                    .Where(x=>!mmKeys.Contains(x.Key)).Sum(x=>x.Value)
                            }.Validate();
                            break;
                        }
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
            if(classKey=="rogue" && specialization=="combat")
            {
                var allowed=new HashSet<string>(StringComparer.Ordinal) { "ranks", "assassinationPointsSpent",
                    "combatPointsSpent", "subtletyPointsSpent", "totalPoints", "unspentPoints" };
                var seen=new HashSet<string>(StringComparer.Ordinal);
                if(selection.EnumerateObject().Any(x=>!allowed.Contains(x.Name) || !seen.Add(x.Name)))
                    throw new ArgumentException("Strict Combat accepts only unique canonical ranks and declared point totals.");
            }
            // Restricted historical adapters never normalize aliases or
            // silently discards an unknown zero-rank entry. Keep raw JSON duplicates
            // visible here; the typed dispatch independently checks both rank maps.
            if(classKey=="rogue" || classKey=="shaman" && specialization=="enhancement" || classKey=="druid" && specialization=="feral")
            {
                var canonical=ForeverHistoricalCasterTalentCatalog.ForSpecialization(classKey,specialization)
                    .Select(x=>x.Key).ToHashSet(StringComparer.Ordinal);
                var seen=new HashSet<string>(StringComparer.Ordinal);
                foreach(var choice in ranked.EnumerateObject())
                    if(!canonical.Contains(choice.Name) || !seen.Add(choice.Name))
                        throw new ArgumentException(classKey=="rogue"?
                            "Strict Rogue ranks require unique exact canonical reconciled keys.":
                            classKey=="druid"?"Strict Feral ranks require unique exact canonical reconciled keys.":
                            "Strict Enhancement ranks require unique exact canonical reconciled keys.");
            }
            if(classKey=="druid" && specialization=="feral")
            {
                var allowed=new HashSet<string>(StringComparer.Ordinal) { "ranks", "balancePointsSpent",
                    "feralPointsSpent", "restorationPointsSpent", "totalPoints", "unspentPoints" };
                var seen=new HashSet<string>(StringComparer.Ordinal);
                if(selection.EnumerateObject().Any(x=>!allowed.Contains(x.Name) || !seen.Add(x.Name)))
                    throw new ArgumentException("Strict Feral accepts only unique canonical ranks and declared point totals.");
            }
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
                if(classKey=="rogue" || classKey=="druid" && specialization=="feral" || !visual.ClassKey.Equals(classKey,StringComparison.OrdinalIgnoreCase) ||
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
                if(ForeverHistoricalCasterTalentCatalog.Supports(classKey,specialization) &&
                    t.Prerequisite is not null && ranks.GetValueOrDefault(t.Prerequisite)<t.PrerequisiteRank)
                    throw new ArgumentException($"{key} requires {t.PrerequisiteRank} ranks of {t.Prerequisite} in the pinned historical tree (provisional).");
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
                ["disciplinePointsSpent"]="discipline",["holyPointsSpent"]="holy",["shadowPointsSpent"]="shadow",
                ["balancePointsSpent"]="balance",["feralPointsSpent"]="feral",
                ["restorationPointsSpent"]="restoration",["elementalPointsSpent"]="elemental",
                ["enhancementPointsSpent"]="enhancement",
                ["arcanePointsSpent"]="arcane",["firePointsSpent"]="fire",["frostPointsSpent"]="frost",
                ["afflictionPointsSpent"]="affliction",["demonologyPointsSpent"]="demonology",
                ["destructionPointsSpent"]="destruction",["marksmanshipPointsSpent"]="marksmanship",
                ["beastMasteryPointsSpent"]="beast-mastery",["survivalPointsSpent"]="survival"};
            if(classKey=="rogue")
            {
                names["assassinationPointsSpent"]="assassination";
                names["combatPointsSpent"]="combat";
                names["subtletyPointsSpent"]="subtlety";
            }
            foreach(var property in selection.EnumerateObject())
                if(names.TryGetValue(property.Name,out var tree) &&
                   (property.Value.ValueKind!=JsonValueKind.Number ||
                    !property.Value.TryGetInt32(out var count) || count!=byTree.GetValueOrDefault(tree)))
                    throw new ArgumentException($"Synthetic aggregate {property.Name} differs from ranked {tree} talent points.");
            if(classKey=="rogue" && specialization=="combat")
                foreach(var (name,expected) in new[]{("totalPoints",spent),("unspentPoints",51-spent)})
                    if(selection.TryGetProperty(name,out var declaration) &&
                       (declaration.ValueKind!=JsonValueKind.Number || !declaration.TryGetInt32(out var value) || value!=expected))
                        throw new ArgumentException($"Declared Combat {name} must equal the actual ranked allocation ({expected}).");
            if(classKey=="rogue")new ForeverRogueTalentBuild {
                Ranks=ranks,AssassinationPointsSpent=byTree.GetValueOrDefault("assassination"),
                CombatPointsSpent=byTree.GetValueOrDefault("combat"),
                SubtletyPointsSpent=byTree.GetValueOrDefault("subtlety")}.Validate(60,specialization);
            if(classKey=="priest")new ForeverPriestShadowTalentBuild {
                Ranks=ranks,DisciplinePointsSpent=byTree.GetValueOrDefault("discipline"),
                HolyPointsSpent=byTree.GetValueOrDefault("holy"),
                ShadowPointsSpent=byTree.GetValueOrDefault("shadow")}.Validate(60);
            if(classKey=="druid" && specialization=="feral")
            {
                foreach(var (name,expected) in new[]{("totalPoints",spent),("unspentPoints",51-spent)})
                    if(selection.TryGetProperty(name,out var declared) &&
                       (declared.ValueKind!=JsonValueKind.Number || !declared.TryGetInt32(out var value) || value!=expected))
                        throw new ArgumentException($"Declared {name} differs from the actual ranked allocation.");
                if(ranks.GetValueOrDefault("furor") is not (0 or 5))
                    throw new ArgumentException("Strict fixed-Cat Feral admits Furor rank zero or five only; general lower-rank behavior is unsupported.");
                new ForeverFeralDruidTalents { Ranks=ranks }.Validate();
            }
            else if(classKey=="druid")new ForeverBalanceDruidTalents {
                Ranks=ranks,BalancePointsSpent=byTree.GetValueOrDefault("balance"),
                FeralPointsSpent=byTree.GetValueOrDefault("feral"),
                RestorationPointsSpent=byTree.GetValueOrDefault("restoration")}.Validate();
            if(classKey=="shaman" && specialization=="enhancement")new ForeverEnhancementShamanTalents {
                Ranks=ranks,ElementalPointsSpent=byTree.GetValueOrDefault("elemental"),
                EnhancementPointsSpent=byTree.GetValueOrDefault("enhancement"),
                RestorationPointsSpent=byTree.GetValueOrDefault("restoration")}.Validate(60);
            else if(classKey=="shaman")new ForeverElementalShamanTalents {
                Ranks=ranks,ElementalPointsSpent=byTree.GetValueOrDefault("elemental"),
                EnhancementPointsSpent=byTree.GetValueOrDefault("enhancement"),
                RestorationPointsSpent=byTree.GetValueOrDefault("restoration")}.Validate();
            return Ok(new{valid=true,pointsSpent=spent,remaining=51-spent,
                treePoints=byTree,
                unmodeledSelections=ranks.Where(x=>!entries[x.Key].Modeled)
                    .Select(x=>new{key=x.Key,rank=x.Value,source=entries[x.Key].Source}).ToArray(),
                sourceStatus=ForeverHistoricalCasterTalentCatalog.Supports(classKey,specialization)?
                    ForeverHistoricalCasterTalentCatalog.SourceStatus:"engine-ranks-plus-pinned-historical-unmodeled-provisional",
                note=classKey=="druid" && specialization=="feral"?string.Join(" ",ForeverHistoricalCasterTalentCatalog.FeralModelingNotes):classKey=="rogue"?string.Join(" ",ForeverHistoricalCasterTalentCatalog.RogueNotes(specialization??"assassination")):
                    "Historical-only talents consume real itemized points but are not yet executable by all typed runners."});
        }
        catch(Exception ex) when(ex is ArgumentException or JsonException or InvalidOperationException)
        {
            return BadRequest(new{valid=false,error=ex.Message});
        }
    }

    private static bool Allowed(string c,string s)=> c switch {
        "rogue"=>s is "assassination" or "combat",
        "priest"=>s=="shadow",
        "druid"=>s is "balance" or "feral",
        "shaman"=>s is "elemental" or "enhancement",
        "mage"=>s is "arcane" or "fire" or "frost",
        "warlock"=>s is "affliction" or "demonology" or "destruction",
        "hunter"=>s is "marksmanship" or "survival" or "beast-mastery",
        _=>false
    };
}
