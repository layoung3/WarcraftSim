using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Characters;
using WarcraftSim.Data.Forever.Simulation;

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

    [HttpPost("resolve")]
    public IActionResult Resolve([FromBody] ForeverCharacterResolutionRequest? request)
    {
        if(request?.Character is null || request.Modifiers is null)
            return BadRequest(new{error="Character profile and modifiers are required."});
        try
        {
            var profile=request.Character;
            if(profile.Level is <1 or >60 || string.IsNullOrWhiteSpace(profile.ClassKey))
                throw new ArgumentException("Class and level 1–60 required.");
            if(!string.IsNullOrWhiteSpace(profile.Race))
                ForeverPlayableRaceCatalog.Validate(profile.Race,profile.ClassKey,profile.Level);
            if(profile.BaseStats is null || profile.BaseStats.Values is null ||
                profile.BaseStats.Values.Count==0)
                return BadRequest(new{
                    error="The pinned Forever client does not establish complete race/class/level base stats. Supply an explicit base profile or a verified imported baseline; no classic-era value was fabricated.",
                    sourceStatus="current-forever-base-stats-not-acquired"
                });
            var normalized=ForeverEquipmentStatBridge.ResolveProfile(
                ForeverExternalModifierCatalog.ApplyToProfile(profile,request.Modifiers));
            var calculated=ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(_=>1m).Calculate(normalized);
            return Ok(new{
                classKey=profile.ClassKey,level=profile.Level,race=profile.Race,
                faction=ForeverRaceFactions.GetFaction(profile.Race),
                baseSourceStatus="explicit-user-profile-not-client-verified",
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
}
