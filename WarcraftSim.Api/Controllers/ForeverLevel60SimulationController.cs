using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Warlock;
using WarcraftSim.Data.Forever.Hunter;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

/// <summary>
/// Frontend-oriented orchestration: select one typed class build (including
/// its actual talent ranks), a source-labelled level60 baseline, equipment,
/// enchants, racials, consumables and target modifiers. The established class
/// runners remain authoritative; do not re-apply an already resolved profile.
/// </summary>
[ApiController]
[Route("api/forever/character")]
public sealed class ForeverLevel60SimulationController : ControllerBase
{
    [HttpGet("simulation-specs")]
    public IActionResult Supported() => Ok(new {
        level=60,
        baselineModes=new[]{"strict-reviewed","historical-provisional","explicit-observation"},
        classes=new[]{
            new {classKey="mage",specializations=new[]{"arcane","fire","frost"},
                requestProperty="mage",defaultsRoute="api/sim/mage/defaults"},
            new {classKey="warlock",specializations=new[]{"affliction","demonology","destruction"},
                requestProperty="warlock",defaultsRoute="api/sim/warlock/defaults"},
            new {classKey="hunter",specializations=new[]{"marksmanship","survival","beast-mastery"},
                requestProperty="marksmanship | survival | beastMastery",
                defaultsRoute="api/sim/{specialization}-hunter/defaults"}
        },
        runRoute="api/forever/character/simulate-level60",
        notes="Submit exactly one typed class request. In each child request select a reviewed level60 baseline, explicit observation, or opt-in historical-provisional mode. Talents remain the class-specific talent object."
    });

    /// <summary>Stable report receipt; a local build without a stamped SHA says so explicitly.</summary>
    [HttpGet("report-metadata")]
    public IActionResult ReportMetadata() => Ok(new {
        reportSchema="warcraftsim.solo-dps-report",
        schemaVersion=1,
        product="WarcraftSim",
        ruleset="wow-forever",
        simulatorVersion=typeof(ForeverLevel60SimulationController).Assembly.GetName().Version?.ToString()??"unavailable",
        sourceCommit=Environment.GetEnvironmentVariable("CP174_SOURCE_COMMIT")??Environment.GetEnvironmentVariable("GITHUB_SHA")??"not-stamped-local-build",
        workflowCheckoutCommit=Environment.GetEnvironmentVariable("GITHUB_SHA")??"not-stamped-local-build",
        runtimeTargetBuild="1.60.1.70707",
        modifierSourceBuild=ForeverExternalModifierCatalog.SourceBuild,
        equipmentCatalogVersion=ForeverEquipmentCatalog.GetCatalog().CatalogVersion,
        historicalTalentReferenceCommit=ForeverTalentVisualReferenceCatalog.SourceCommit,
        notes=new[]{
            "The build identifier names the target client, not complete verified client formulas.",
            "Typed runners preserve their existing supported mechanics and cross-tree limits.",
            "Unverified defaults and formulas remain provisional; source commit may be unavailable in local development."
        }
    });

    [HttpPost("simulate-level60")]
    public async Task<IActionResult> Simulate(
        [FromBody] ForeverLevel60SimulationRequest? request,
        CancellationToken token)
    {
        if(request is null)return BadRequest(new{error="A level60 typed character configuration is required."});
        var count=new object?[]{request.Mage,request.Warlock,request.Marksmanship,
            request.Survival,request.BeastMastery}.Count(x=>x is not null);
        if(count!=1)return BadRequest(new{error="Provide exactly one Mage, Warlock, or Hunter specialization request."});
        // The new unified level-60 builder is strictly itemized. Legacy direct
        // per-class APIs remain backward compatible, but cannot be used to
        // bypass ranked points through this frontend-oriented route.
        if(!request.ItemizedTalentMode || request.ItemizedTalentRanks is null)
            return BadRequest(new{error="Unified level-60 builds require itemizedTalentMode=true and a complete itemizedTalentRanks map. Synthetic tree aggregates are not accepted."});
        try
        {
            var cls=request.ClassKey?.Trim().ToLowerInvariant()??"";
            if(request.ItemizedTalentMode)
            {
                object? selected=request.ClassKey?.ToLowerInvariant() switch {
                    "mage"=>request.Mage?.Talents,"warlock"=>request.Warlock?.Talents,
                    "hunter"=>request.Specialization?.ToLowerInvariant() switch {
                        "marksmanship"=>request.Marksmanship?.Talents,
                        "survival"=>request.Survival?.Talents,
                        "beast-mastery"=>request.BeastMastery?.Talents,_=>null},
                    _=>null};
                if(selected is null)return BadRequest(new{error="Strict itemized talent build is missing its typed allocation."});
                var modeledRanks=selected switch {
                    ForeverMageTalentBuild value=>value.Ranks,
                    ForeverWarlockTalentBuild value=>value.Ranks,
                    ForeverMarksmanshipHunterTalents value=>value.Ranks,
                    ForeverSurvivalHunterTalents value=>value.Ranks,
                    ForeverBeastMasteryHunterTalents value=>value.Ranks,
                    _=>null
                };
                if(modeledRanks is null)return BadRequest(new{error="Typed talent rank data is missing."});
                var allRanks=request.ItemizedTalentRanks??modeledRanks;
                foreach(var (key,rank) in modeledRanks)
                    if(allRanks.GetValueOrDefault(key)!=rank)
                        return BadRequest(new{error=$"Itemized talent '{key}' differs from its executable rank in the typed request."});
                if(request.ItemizedTalentRanks is not null){
                    // A modeled talent absent from this runner must not silently act
                    // as a historical-only point; that would ignore gameplay.
                    var modeledKeys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var talentController=new ForeverSoloTalentController();
                    var className=request.ClassKey?.ToLowerInvariant()??"";
                    foreach(var treeSpec in className=="mage"?new[]{"arcane"}:
                             className=="warlock"?new[]{"affliction"}:
                             new[]{"marksmanship","survival","beast-mastery"}){
                        var discovered=talentController.Catalog(className,treeSpec) as OkObjectResult;
                        if(discovered is null)continue;
                        using var metadata=JsonDocument.Parse(JsonSerializer.Serialize(discovered.Value,
                            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                        foreach(var item in metadata.RootElement.GetProperty("talents").EnumerateArray())
                            modeledKeys.Add(item.GetProperty("key").GetString()!);
                    }
                    if(allRanks.Any(x=>x.Value>0&&modeledKeys.Contains(x.Key)&&!modeledRanks.ContainsKey(x.Key)))
                        return BadRequest(new{error="A selected modeled talent belongs to another typed runner and cannot execute in this starting preset."});
                }
                var source=JsonNode.Parse(JsonSerializer.Serialize(selected,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)))!.AsObject();
                source["ranks"]=JsonSerializer.SerializeToNode(allRanks,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var talentValidation=new ForeverSoloTalentController().ValidateItemized(
                    request.ClassKey?.ToLowerInvariant()??"",request.Specialization?.ToLowerInvariant()??"",
                    JsonSerializer.SerializeToElement(source,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
                if(talentValidation is not OkObjectResult)
                    return BadRequest(new{error="Strict itemized talent allocation was rejected.",validation=talentValidation});
            }

            var spec=request.Specialization?.Trim().ToLowerInvariant()??"";
            if(cls=="mage" && request.Mage is { } mage &&
                spec==mage.Specialization && spec is ("arcane" or "fire" or "frost"))
            {
                var baseline=Required(mage.Level60Baseline,
                    mage.UseReviewedLevel60Baseline,mage.UseProvisionalLevel60Baseline,
                    mage.Character.Race,"mage");
                var result=await Task.Run(()=>new MageSimRunner().Run(mage,token),token);
                return Ok(new{classKey=cls,specialization=spec,level=60,
                    baseline=ForeverLevel60Baseline.Summary(baseline),
                    sim=result});
            }
            if(cls=="warlock" && request.Warlock is { } warlock &&
                spec==warlock.Specialization &&
                spec is ("affliction" or "demonology" or "destruction"))
            {
                var baseline=Required(warlock.Level60Baseline,
                    warlock.UseReviewedLevel60Baseline,warlock.UseProvisionalLevel60Baseline,
                    warlock.Character.Race,"warlock");
                var result=await Task.Run(()=>new WarlockSimRunner().Run(warlock,token),token);
                return Ok(new{classKey=cls,specialization=spec,level=60,
                    baseline=ForeverLevel60Baseline.Summary(baseline),
                    sim=result});
            }
            if(cls=="hunter" && spec=="marksmanship" && request.Marksmanship is { } mm)
            {
                var baseline=Required(mm.Level60Baseline,
                    mm.UseReviewedLevel60Baseline,mm.UseProvisionalLevel60Baseline,
                    mm.Race,"hunter");
                var result=await Task.Run(()=>new MarksmanshipHunterSimRunner().Run(mm,token),token);
                return Ok(new{classKey=cls,specialization=spec,level=60,
                    baseline=ForeverLevel60Baseline.Summary(baseline),sim=result});
            }
            if(cls=="hunter" && spec=="survival" && request.Survival is { } sv)
            {
                var baseline=Required(sv.Level60Baseline,
                    sv.UseReviewedLevel60Baseline,sv.UseProvisionalLevel60Baseline,
                    sv.Race,"hunter");
                var result=await Task.Run(()=>new SurvivalHunterSimRunner().Run(sv,token),token);
                return Ok(new{classKey=cls,specialization=spec,level=60,
                    baseline=ForeverLevel60Baseline.Summary(baseline),sim=result});
            }
            if(cls=="hunter" && spec=="beast-mastery" && request.BeastMastery is { } bm)
            {
                var baseline=Required(bm.Level60Baseline,
                    bm.UseReviewedLevel60Baseline,bm.UseProvisionalLevel60Baseline,
                    bm.Race,"hunter");
                var result=await Task.Run(()=>new BeastMasteryHunterSimRunner().Run(bm,token),token);
                return Ok(new{classKey=cls,specialization=spec,level=60,
                    baseline=ForeverLevel60Baseline.Summary(baseline),sim=result});
            }
            return BadRequest(new {error="Class/specialization does not match its typed request and selected talents."});
        }
        catch(ArgumentException ex){return BadRequest(new{error=ex.Message});}
        catch(InvalidOperationException ex){return BadRequest(new{error=ex.Message});}
        catch(SimulationEventLimitExceededException ex)
        {return UnprocessableEntity(new{error=ex.Message});}
    }

    private static ForeverLevel60BaselineObservation Required(
        ForeverLevel60BaselineObservation? inline,bool strict,bool provisional,
        string? race,string cls)
    {
        var baseline=ForeverLevel60Baseline.Resolve(inline,strict,race??"",cls,provisional) ??
            throw new ArgumentException(
                "No character baseline selected. Choose strict reviewed or historical-provisional mode, or provide a provenance-labelled observation.");
        ForeverLevel60Baseline.Validate(baseline);
        return baseline;
    }
}

public sealed class ForeverLevel60SimulationRequest
{
    public string ClassKey {get;set;}="";
    public string Specialization {get;set;}="";
    public bool ItemizedTalentMode {get;set;} // opt-in strict ranked allocation; old typed requests remain compatible
    public Dictionary<string,int>? ItemizedTalentRanks {get;set;} // full ranks including explicitly unmodeled historical talents
    public MageSimRequest? Mage {get;set;}
    public WarlockSimRequest? Warlock {get;set;}
    public MarksmanshipHunterSimRequest? Marksmanship {get;set;}
    public SurvivalHunterSimRequest? Survival {get;set;}
    public BeastMasteryHunterSimRequest? BeastMastery {get;set;}
}
