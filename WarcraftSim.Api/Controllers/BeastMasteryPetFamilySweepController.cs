using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/beast-mastery-hunter/pet-sweep")]
public sealed class BeastMasteryPetFamilySweepController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BeastMasteryPetFamilySweepReport>> Run(
        [FromBody] BeastMasteryHunterSimRequest request, CancellationToken token)
    {
        if (request is null) return BadRequest(new { Error = "Beast Mastery request required." });
        try
        {
            return Ok(await Task.Run(() => new BeastMasteryPetFamilySweep().Run(request, token), token));
        }
        catch (ArgumentException ex) { return BadRequest(new { Error = ex.Message }); }
        catch (SimulationEventLimitExceededException ex)
        { return UnprocessableEntity(new { Error = ex.Message }); }
    }
}
