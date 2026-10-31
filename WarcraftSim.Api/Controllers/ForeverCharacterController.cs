using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Data.Forever.Simulation;
using WarcraftSim.Data.Forever.Items;

namespace WarcraftSim.Api.Controllers;

/// <summary>
/// Read-only CP173 character-builder contract. Every selected external source
/// resolves server-side by ID. Missing Forever base-stat growth never becomes
/// silently fabricated output or a replacement for legacy explicit profiles.
/// </summary>
[ApiController]
[Route("api/forever/character")]
public sealed class ForeverCharacterController : ControllerBase
{
    [HttpGet("races")]
    public IActionResult Races([FromQuery] string? classKey=null)
    {
        IEnumerable<ForeverRaceDefinition> source=ForeverPlayableRaceCatalog.Races;
        if(!string.IsNullOrWhiteSpace(classKey))
            source=source.Where(x=>x.Classes.Contains(classKey,StringComparer.OrdinalIgnoreCase));
        return Ok(new{sourceBuild=ForeverPlayableRaceCatalog.SourceBuild,
            races=source.OrderBy(x=>x.Key).ToArray(),supportedLevels=new{minimum=1,maximum=60}});
    }

    [HttpGet("base-stats")]
    public IActionResult BaseStats([FromQuery] string race,[FromQuery] string classKey,
        [FromQuery] int level,[FromQuery] bool provisional=false)
    {
        try{
            if(provisional)
            {
                if(level!=60)throw new ArgumentException("Historical provisional baselines support level 60 only.");
                return Ok(ForeverLevel60Baseline.Summary(
                    ForeverLevel60BaselineCatalog.FindProvisional(race,classKey)));
            }
            return Ok(ForeverPlayableRaceCatalog.BaseStats(race,classKey,level));
        }
        catch(ArgumentException ex){return BadRequest(new{error=ex.Message});}
    }

    [HttpGet("racials")]
    public IActionResult Racials([FromQuery] string? race=null,
        [FromQuery] bool? modeled=null)
    {
        var source=ForeverRacialCatalog.Entries.AsEnumerable();
        if(!string.IsNullOrWhiteSpace(race))
        {
            try{_ = ForeverPlayableRaceCatalog.Find(race);}
            catch(ArgumentException e){return BadRequest(new{error=e.Message});}
            source=source.Where(x=>x.Race.Equals(race,StringComparison.OrdinalIgnoreCase));
        }
        if(modeled.HasValue)source=source.Where(x=>x.Modeled==modeled.Value);
        var items=source.OrderBy(x=>x.Race).ThenBy(x=>x.Key).ToArray();
        return Ok(new{sourceBuild=ForeverRacialCatalog.SourceBuild,
            total=items.Length,racials=items});
    }

    [HttpGet("combat-consumables")]
    public IActionResult CombatConsumables()
    {
        var abilities=ForeverCombatItemCatalog.CreateAll()
            .OrderBy(x=>x.Key).ToArray();
        return Ok(new{total=abilities.Length,
            items=abilities.Select(x=>new {
                x.Key,x.Name,x.CooldownSeconds,
                x.SharedCooldownGroupKey,x.SharedCooldownSeconds,
                Effects=x.Effects,
                SourceStatus="existing-executable-provisional-consumable",
                SourceUrl=x.Key==ForeverCombatItemCatalog.MajorHealingPotion?
                    "https://wow-forever.gg/db/spells/17534-healing-potion/":
                    "existing-CP132-reviewed-catalog"
            }).ToArray()});
    }

    [HttpGet("modifiers")]
    public IActionResult Modifiers([FromQuery] string? category=null)
    {
        var list=ForeverExternalModifierCatalog.Entries
            .Where(x=>string.IsNullOrWhiteSpace(category)||
                x.Category.Equals(category,StringComparison.OrdinalIgnoreCase))
            .OrderBy(x=>x.Category).ThenBy(x=>x.Key).ToArray();
        return Ok(new{sourceBuild=ForeverExternalModifierCatalog.SourceBuild,
            total=list.Length,modifiers=list});
    }

    [HttpGet("level60-baseline-coverage")]
    public IActionResult Level60BaselineCoverage()
    {
        var rows=ForeverPlayableRaceCatalog.Races.SelectMany(r=>r.Classes.Select(c=>new {
            race=r.Key,classKey=c,level=60,
            status=ForeverLevel60BaselineCatalog.Entries
                .FirstOrDefault(x=>x.Race.Equals(r.Key,StringComparison.OrdinalIgnoreCase) &&
                x.ClassKey.Equals(c,StringComparison.OrdinalIgnoreCase))?.VerificationStatus ??
                "needs-forever-observation",
            baseMana=ForeverClientBaseManaCatalog.GetBaseMana(c,60),
            baseManaSourceBuild=ForeverClientBaseManaCatalog.SourceBuild
        })).OrderBy(x=>x.race).ThenBy(x=>x.classKey).ToArray();
        return Ok(new{
            catalogVersion=ForeverLevel60BaselineCatalog.CatalogVersion,
            sourceStatus=ForeverLevel60BaselineCatalog.SourceStatus,
            verifiedBaselineCount=ForeverLevel60BaselineCatalog.Entries.Count(
                x=>x.VerificationStatus=="reviewed-forever-in-game"),
            provisionalBaselineCount=ForeverLevel60BaselineCatalog.Entries.Count(
                x=>x.VerificationStatus=="provisional-documented-proxy"),
            totalCombinations=rows.Length,combinations=rows
        });
    }

    [HttpPost("generate-level60")]
    public IActionResult GenerateLevel60([FromBody] ForeverCharacterResolutionRequest? request)
    {
        if(request?.Character is null || request.Modifiers is null)
            return BadRequest(new{error="Race/class and modifier selections are required."});
        try
        {
            var baseline=ForeverLevel60Baseline.Resolve(
                request.Baseline,request.UseReviewedBaseline,
                request.Character.Race??"",request.Character.ClassKey,request.UseProvisionalBaseline) ??
                throw new ArgumentException("An explicit observation or reviewed baseline selection is required.");
            var generated=ForeverLevel60Baseline.Generate(request.Character,baseline);
            var profile=ForeverRacialPassives.ApplyToProfile(
                ForeverEquipmentStatBridge.ResolveProfile(
                    ForeverExternalModifierCatalog.ApplyToProfile(
                        generated,request.Modifiers)),
                observedPrimariesAlreadyContainRacials:baseline.IncludesRacialPassives);
            var calculated=ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(_=>1m).Calculate(profile);
            return Ok(new{
                classKey=generated.ClassKey,level=60,race=generated.Race,
                faction=ForeverRaceFactions.GetFaction(generated.Race),
                baseline=ForeverLevel60Baseline.Summary(baseline),
                // Safe roundtrip: simulator runners apply selected external/racial
                // modifiers themselves. Returning the already-contributed
                // resolved profile as an input would double-apply them.
                readyProfile=generated,
                resolvedProfile=profile,
                selectedTalents=profile.Talents,
                equipment=profile.Equipment,
                selectedModifiers=ForeverExternalModifierCatalog.Resolve(
                    request.Modifiers,60),
                simulationInputNotes="Pass readyProfile with the same modifier selection to the class runner. resolvedProfile is display-only and must not be re-applied.",
                effectiveStats=calculated.EffectiveStats,
                contributions=calculated.StatContributions,
                sourceStatus=baseline.VerificationStatus
            });
        }
        catch(ArgumentException ex){return BadRequest(new{error=ex.Message});}
        catch(InvalidOperationException ex){return BadRequest(new{error=ex.Message});}
    }

    [HttpPost("resolve")]
    public IActionResult Resolve([FromBody] ForeverCharacterResolutionRequest? request)
    {
        if(request?.Character is null || request.Modifiers is null)
            return BadRequest(new{error="Character profile and modifiers are required."});
        try
        {
            var baseline=ForeverLevel60Baseline.Resolve(
                request.Baseline,request.UseReviewedBaseline,
                request.Character.Race??"",request.Character.ClassKey,request.UseProvisionalBaseline);
            var profile=baseline is null ? request.Character :
                ForeverLevel60Baseline.Generate(request.Character,baseline);
            if(profile.Level is <1 or >60 || string.IsNullOrWhiteSpace(profile.ClassKey))
                throw new ArgumentException("Class and level 1–60 required.");
            if(!string.IsNullOrWhiteSpace(profile.Race))
                ForeverPlayableRaceCatalog.Validate(profile.Race,profile.ClassKey,profile.Level);
            if(profile.BaseStats is null || profile.BaseStats.Values is null ||
                profile.BaseStats.Values.Count==0)
                return BadRequest(new{
                    error="The pinned Forever client does not establish complete race/class/level base stats. Supply an explicit base profile or a verified imported baseline; no classic-era value was fabricated.",
                    sourceStatus="forever-primary-attributes-not-established"
                });
            var normalized=ForeverRacialPassives.ApplyToProfile(
                ForeverEquipmentStatBridge.ResolveProfile(
                    ForeverExternalModifierCatalog.ApplyToProfile(profile,request.Modifiers)),
                observedPrimariesAlreadyContainRacials:baseline?.IncludesRacialPassives==true);
            var calculated=ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(_=>1m).Calculate(normalized);
            return Ok(new{
                classKey=profile.ClassKey,level=profile.Level,race=profile.Race,
                faction=ForeverRaceFactions.GetFaction(profile.Race),
                baseSourceStatus=baseline?.VerificationStatus ??
                    "explicit-user-profile-not-client-verified",
                baseline=baseline is null ? null :
                    ForeverLevel60Baseline.Summary(baseline),
                effectiveStats=calculated.EffectiveStats,
                contributions=calculated.StatContributions,
                equipment=normalized.Equipment,
                selectedModifiers=ForeverExternalModifierCatalog.Resolve(request.Modifiers,profile.Level),
                selectedTalents=normalized.Talents,
                modelingNotes=new[]{
                    "Unknown Forever race/class/level stat progressions are not invented.",
                    "External buffs and consumables are opt-in and use CP170's existing derived contributors.",
                    "Target debuffs execute only within supported combat runners."
                }
            });
        }
        catch(ArgumentException ex){return BadRequest(new{error=ex.Message});}
        catch(InvalidOperationException ex){return BadRequest(new{error=ex.Message});}
    }
}

public sealed class ForeverCharacterResolutionRequest
{
    public CharacterProfile Character { get; set; }=new();
    public ForeverExternalModifierSelection Modifiers { get; set; }=new();
    public ForeverLevel60BaselineObservation? Baseline { get; set; }
    public bool UseReviewedBaseline { get; set; }
    public bool UseProvisionalBaseline { get; set; } // historical Classic proxy; explicitly opt in
}
