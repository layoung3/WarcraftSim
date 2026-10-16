using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/elemental-shaman")]
public sealed class ElementalShamanSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<ElementalShamanSimRequest> Defaults() => Ok(new ElementalShamanSimRequest());

    [HttpPost("run")]
    public async Task<ActionResult<ElementalShamanSimResponse>> Run([FromBody] ElementalShamanSimRequest request,
        CancellationToken token)
    {
        if(request is null)return BadRequest(new {Error="Elemental Shaman setup required."});
        try{return Ok(await Task.Run(()=>new ElementalShamanSimRunner().Run(request,token),token));}
        catch(ArgumentException ex){return BadRequest(new {Error=ex.Message});}
        catch(SimulationEventLimitExceededException ex){return UnprocessableEntity(new {Error=ex.Message});}
    }
}
