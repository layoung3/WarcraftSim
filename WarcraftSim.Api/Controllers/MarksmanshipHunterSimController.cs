using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/marksmanship-hunter")]
public sealed class MarksmanshipHunterSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<MarksmanshipHunterSimRequest> Defaults() => Ok(new MarksmanshipHunterSimRequest());

    [HttpPost("run")]
    public async Task<ActionResult<MarksmanshipHunterSimResponse>> Run(
        [FromBody] MarksmanshipHunterSimRequest request, CancellationToken token)
    {
        if (request is null) return BadRequest(new { Error = "Hunter setup required." });
        try { return Ok(await Task.Run(() => new MarksmanshipHunterSimRunner().Run(request, token), token)); }
        catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (SimulationEventLimitExceededException ex) { return UnprocessableEntity(new { Error = ex.Message }); }
    }
}
