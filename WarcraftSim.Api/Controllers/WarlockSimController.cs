using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/warlock")]
public sealed class WarlockSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<WarlockSimRequest> Defaults([FromQuery] string spec = "affliction")
    {
        if (spec is not ("affliction" or "demonology" or "destruction"))
            return BadRequest(new { Error = "Select affliction, demonology, or destruction." });
        return Ok(WarlockSimRequest.CreatePreset(spec));
    }

    [HttpPost("run")]
    public async Task<ActionResult<WarlockSimResponse>> Run(
        [FromBody] WarlockSimRequest request, CancellationToken token)
    {
        if (request is null) return BadRequest(new { Error = "Warlock configuration required." });
        try { return Ok(await Task.Run(() => new WarlockSimRunner().Run(request, token), token)); }
        catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (SimulationEventLimitExceededException ex) { return UnprocessableEntity(new { Error = ex.Message }); }
    }
}
