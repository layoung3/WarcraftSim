using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/restoration-shaman")]
public sealed class RestorationShamanSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<RestorationShamanSimRequest> Defaults() => Ok(new RestorationShamanSimRequest());

    [HttpPost("run")]
    public async Task<ActionResult<RestorationShamanSimResponse>> Run(
        [FromBody] RestorationShamanSimRequest request, CancellationToken token)
    {
        if (request is null) return BadRequest(new { Error = "Restoration request required." });
        try { return Ok(await Task.Run(() => new RestorationShamanSimRunner().Run(request, token), token)); }
        catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (SimulationEventLimitExceededException ex)
        { return UnprocessableEntity(new { Error = ex.Message }); }
    }
}
