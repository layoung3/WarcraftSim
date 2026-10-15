using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/mage")]
public sealed class MageSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<MageSimRequest> Defaults([FromQuery] string spec = "arcane")
    {
        if (spec is not ("arcane" or "fire" or "frost"))
            return BadRequest(new { Error = "Choose arcane, fire or frost." });
        return Ok(MageSimRequest.CreatePreset(spec));
    }

    [HttpPost("run")]
    public async Task<ActionResult<MageSimResponse>> Run([FromBody] MageSimRequest request,
        CancellationToken token)
    {
        if (request is null) return BadRequest(new { Error = "Mage setup required." });
        try { return Ok(await Task.Run(() => new MageSimRunner().Run(request, token), token)); }
        catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (SimulationEventLimitExceededException ex)
        { return UnprocessableEntity(new { Error = ex.Message }); }
    }
}
