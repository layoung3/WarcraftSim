using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/beast-mastery-hunter")]
public sealed class BeastMasteryHunterSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<BeastMasteryHunterSimRequest> Defaults() => Ok(new BeastMasteryHunterSimRequest());

    [HttpPost("run")]
    public async Task<ActionResult<BeastMasteryHunterSimResponse>> Run(
        [FromBody] BeastMasteryHunterSimRequest request, CancellationToken token)
    {
        if (request is null) return BadRequest(new { Error = "Beast Mastery request required." });
        try { return Ok(await Task.Run(() => new BeastMasteryHunterSimRunner().Run(request,token),token)); }
        catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (SimulationEventLimitExceededException ex) { return UnprocessableEntity(new { Error = ex.Message }); }
    }
}
