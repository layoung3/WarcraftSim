using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/warrior-priest")]
public sealed class WarriorPriestIntegratedController : ControllerBase
{
    [HttpPost("run")]
    public async Task<ActionResult<WarriorPriestIntegratedResponse>> Run(
        [FromBody] WarriorPriestIntegratedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { Error = "An integrated encounter request is required." });
        try
        {
            var result = await Task.Run(() =>
                new ForeverWarriorSimRunner().RunIntegrated(request, cancellationToken),
                cancellationToken);
            return Ok(result);
        }
        catch (SimulationEventLimitExceededException error)
        {
            return UnprocessableEntity(new { Error = error.Message });
        }
        catch (ArgumentException error)
        {
            return BadRequest(new { Error = error.Message });
        }
    }
}
