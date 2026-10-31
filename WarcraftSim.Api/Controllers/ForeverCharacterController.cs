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
        [FromQuery] int level)
    {
        try{return Ok(ForeverPlayableRaceCatalog.BaseStats(race,classKey,level));}
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

    [HttpPost("generate-level60")]
    public IActionResult GenerateLevel60([FromBody] ForeverCharacterResolutionRequest? request)
    {
        if(request?.Character is null || request.Modifiers is null ||
            request.Baseline is null)
            return BadRequest(new{error="Race/class, modifiers and a provenance-labelled naked level-60 observation are required."});
        try
        {
            var generated=ForeverLevel60Baseline.Generate(request.Character,request.Baseline);
            var profile=ForeverRacialPassives.ApplyToProfile(
                ForeverEquipmentStatBridge.ResolveProfile(
                    ForeverExternalModifierCatalog.ApplyToProfile(
                        generated,request.Modifiers)),
                observedPrimariesAlreadyContainRacials:true);
            var calculated=ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(_=>1m).Calculate(profile);
            return Ok(new{
                classKey=generated.ClassKey,level=60,race=generated.Race,
                faction=ForeverRaceFactions.GetFaction(generated.Race),
                baseline=ForeverLevel60Baseline.Summary(request.Baseline),
                readyProfile=profile,
                selectedTalents=profile.Talents,
                equipment=profile.Equipment,
                selectedModifiers=ForeverExternalModifierCatalog.Resolve(
                    request.Modifiers,60),
                effectiveStats=calculated.EffectiveStats,
                contributions=calculated.StatContributions,
                sourceStatus="baseline-user-provided-provenance-labelled"
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
            var profile=request.Baseline is null ? request.Character :
                ForeverLevel60Baseline.Generate(request.Character,request.Baseline);
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
                observedPrimariesAlreadyContainRacials:request.Baseline is not null);
            var calculated=ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(_=>1m).Calculate(normalized);
            return Ok(new{
                classKey=profile.ClassKey,level=profile.Level,race=profile.Race,
                faction=ForeverRaceFactions.GetFaction(profile.Race),
                baseSourceStatus=request.Baseline?.VerificationStatus ??
                    "explicit-user-profile-not-client-verified",
                baseline=request.Baseline is null ? null :
                    ForeverLevel60Baseline.Summary(request.Baseline),
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
}
