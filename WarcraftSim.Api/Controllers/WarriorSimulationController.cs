using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/warrior")]
public sealed class WarriorSimulationController : ControllerBase
{
    [HttpPost("run")]
    public ActionResult<WarriorSimResponse> Run([FromBody] WarriorSimRequest request)
    {
        if (request is null)
            return BadRequest(new { Error = "A Warrior simulation request is required." });

        try
        {
            return Ok(new ForeverWarriorSimRunner().Run(request));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { Error = exception.Message });
        }
    }
}
