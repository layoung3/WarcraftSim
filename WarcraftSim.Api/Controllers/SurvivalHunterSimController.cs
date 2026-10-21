using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/survival-hunter")]
public sealed class SurvivalHunterSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<SurvivalHunterSimRequest> Defaults() => Ok(new SurvivalHunterSimRequest());

    [HttpPost("run")]
    public async Task<ActionResult<SurvivalHunterSimResponse>> Run(
        [FromBody] SurvivalHunterSimRequest request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { Error = "Survival Hunter request required." });
        try { return Ok(await Task.Run(() => new SurvivalHunterSimRunner().Run(request, cancellationToken), cancellationToken)); }
        catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (SimulationEventLimitExceededException ex) { return UnprocessableEntity(new { Error = ex.Message }); }
    }
}
