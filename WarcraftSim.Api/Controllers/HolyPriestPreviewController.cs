using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/healer-preview")]
public sealed class HolyPriestPreviewController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<HolyPriestPreviewRequest> Defaults() =>
        Ok(new HolyPriestPreviewRequest());

    [HttpPost("run")]
    public async Task<ActionResult<HolyPriestPreviewResponse>> Run(
        [FromBody] HolyPriestPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { Error = "A healer preview request is required." });
        try
        {
            var result = await Task.Run(() =>
                new HolyPriestPreviewRunner().Run(request, cancellationToken), cancellationToken);
            return Ok(result);
        }
        catch (SimulationEventLimitExceededException exception)
        {
            return UnprocessableEntity(new { Error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { Error = exception.Message });
        }
    }
}
