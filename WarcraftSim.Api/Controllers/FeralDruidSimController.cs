using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/feral-druid")]
public sealed class FeralDruidSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<FeralDruidSimRequest> Defaults([FromQuery] string role = "cat") =>
        Ok(new FeralDruidSimRequest { Role = role });

    [HttpPost("run")]
    public async Task<ActionResult<FeralDruidSimResponse>> Run(
        [FromBody] FeralDruidSimRequest request, CancellationToken token)
    {
        if (request is null) return BadRequest(new { Error = "Druid request required." });
        try { return Ok(await Task.Run(() => new FeralDruidSimRunner().Run(request,token),token)); }
        catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (SimulationEventLimitExceededException ex)
        { return UnprocessableEntity(new { Error = ex.Message }); }
    }
}
