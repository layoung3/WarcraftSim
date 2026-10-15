using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/enhancement-shaman")]
public sealed class EnhancementShamanSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<EnhancementShamanSimRequest> Defaults() => Ok(new EnhancementShamanSimRequest());

    [HttpPost("run")]
    public async Task<ActionResult<EnhancementShamanSimResponse>> Run(
        [FromBody] EnhancementShamanSimRequest request, CancellationToken token)
    {
        if (request is null) return BadRequest(new { Error = "An Enhancement setup is required." });
        try
        {
            return Ok(await Task.Run(() => new EnhancementShamanSimRunner().Run(request, token), token));
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
