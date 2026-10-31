using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;

namespace WarcraftSim.Api.Controllers;

/// <summary>Read-only preview of the engine's existing caster rotation entries.
/// This endpoint does not manufacture talent or spec-specific abilities.</summary>
[ApiController]
[Route("api/forever/rotation")]
public sealed class ForeverSoloRotationController : ControllerBase
{
    [HttpPost("preview")]
    public IActionResult Preview([FromBody] ForeverLevel60SimulationRequest? request)
    {
        if(request is null)return BadRequest(new {error="A typed class configuration is required."});
        try
        {
            var cls=request.ClassKey.Trim().ToLowerInvariant();
            if(cls=="mage" && request.Mage is { } mage &&
                mage.Specialization==request.Specialization)
            {
                var profile=MageAdaptiveRotation.Build(mage);
                return Ok(new{mode=mage.CustomPriority.Count>0?"custom":"default",
                    supported=true,execution="MageAdaptiveRotation",
                    entries=profile.Entries.Select(x=>new{x.AbilityKey,x.IsEnabled,x.Priority,
                        x.Conditions}).ToArray()});
            }
            if(cls=="warlock" && request.Warlock is { } warlock &&
                warlock.Specialization==request.Specialization)
            {
                var profile=WarlockAdaptiveRotation.Build(warlock);
                return Ok(new{mode=warlock.CustomPriority.Count>0?"custom":"default",
                    supported=true,execution="WarlockAdaptiveRotation",
                    entries=profile.Entries.Select(x=>new{x.AbilityKey,x.IsEnabled,x.Priority,
                        x.Conditions}).ToArray()});
            }
            return BadRequest(new{error="Rotation preview currently supports typed Mage and Warlock requests. Hunter custom priorities execute in their respective runners but have no shared preview contract."});
        }
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
        catch(InvalidOperationException e){return BadRequest(new{error=e.Message});}
    }
}