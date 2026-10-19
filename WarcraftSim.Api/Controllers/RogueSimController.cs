using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/rogue")]
public sealed class RogueSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<RogueSimRequest> Defaults([FromQuery] string spec = "assassination")
    {
        if (spec is not ("assassination" or "combat" or "subtlety"))
            return BadRequest(new { Error = "Select assassination, combat, or subtlety." });
        return Ok(RogueSimRequest.CreatePreset(spec));
    }

    [HttpPost("run")]
    public async Task<ActionResult<RogueSimResponse>> Run([FromBody] RogueSimRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { Error = "Rogue configuration required." });
        try { return Ok(await Task.Run(() => new RogueSimRunner().Run(request, cancellationToken), cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (SimulationEventLimitExceededException ex)
        { return UnprocessableEntity(new { Error = ex.Message }); }
    }
}
