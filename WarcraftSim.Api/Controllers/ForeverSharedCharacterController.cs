using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;

namespace WarcraftSim.Api.Controllers;

/// <summary>CP175 shared builder discovery and fail-closed structural contracts.
/// Actual gameplay execution continues through already-certified typed adapters
/// until Stage G completes; a validate-envelope response is never a sim receipt.</summary>
[ApiController]
[Route("api/forever/shared")]
public sealed class ForeverSharedCharacterController : ControllerBase
{
    [HttpGet("capabilities")]
    public IActionResult Capabilities() => Ok(new {
        schemaVersion=1,level=60,
        classes=ForeverClassSimulationCapabilities.All,
        note="A capability advertises confirmed existing functionality, not future CP175 parity."
    });

    [HttpGet("capabilities/{classKey}")]
    public IActionResult Capability(string classKey)
    {
        try{return Ok(ForeverClassSimulationCapabilities.Get(classKey));}
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
    }

    [HttpPost("validate-envelope")]
    public IActionResult Validate([FromBody] ForeverSharedCharacterConfiguration? build)
    {
        try{return Ok(ForeverSharedCharacterConfigurationValidator.Validate(build));}
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
        catch(InvalidOperationException e){return BadRequest(new{error=e.Message});}
    }
}
