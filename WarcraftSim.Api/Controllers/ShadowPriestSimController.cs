using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/shadow-priest")]
public sealed class ShadowPriestSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<ShadowPriestSimRequest> Defaults() => Ok(new ShadowPriestSimRequest());

    [HttpPost("run")]
    public async Task<ActionResult<ShadowPriestSimResponse>> Run(
        [FromBody] ShadowPriestSimRequest request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { Error = "A Shadow Priest setup is required." });
        try
        {
            return Ok(await Task.Run(() =>
                new ShadowPriestSimRunner().Run(request, cancellationToken), cancellationToken));
        }
        catch (SimulationEventLimitExceededException ex)
        {
            return UnprocessableEntity(new { Error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}
