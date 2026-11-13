using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;

namespace WarcraftSim.Api.Controllers;

/// <summary>CP175 shared builder discovery and fail-closed structural contracts.
/// Typed endpoint gates and class-wide adapters are advertised separately;
/// a validate-envelope response is never a simulation receipt.</summary>
[ApiController]
[Route("api/forever/shared")]
public sealed class ForeverSharedCharacterController : ControllerBase
{
    [HttpGet("capabilities")]
    public IActionResult Capabilities() => Ok(new {
        schemaVersion=1,level=60,
        classes=ForeverClassSimulationCapabilities.All,
        specializationOverrides=ForeverClassSimulationCapabilities.SpecializationOverrides,
        classWide=new {classes=new[]{"shaman","rogue","paladin"},builder="/class-build.html",
            catalog="/api/forever/shared/class-builds/{classKey}/catalog",
            validate="/api/forever/shared/class-builds/validate",simulate="/api/forever/shared/class-builds/simulate",
            compare="/api/forever/shared/class-builds/compare",coverage="/api/forever/shared/class-builds/coverage"},
        note="A capability advertises confirmed existing functionality, not future CP175 parity."
    });

    [HttpGet("capabilities/{classKey}")]
    public IActionResult Capability(string classKey, [FromQuery] string? specialization=null)
    {
        try{return Ok(ForeverClassSimulationCapabilities.Get(classKey,specialization));}
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
