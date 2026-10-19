using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/restoration-druid")]
public sealed class RestorationDruidSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<RestorationDruidSimRequest> Defaults()=>Ok(new RestorationDruidSimRequest());
    [HttpPost("run")]
    public async Task<ActionResult<RestorationDruidSimResponse>> Run(
        [FromBody] RestorationDruidSimRequest request,CancellationToken token)
    {
        if(request is null)return BadRequest(new {Error="Restoration Druid request required."});
        try{return Ok(await Task.Run(()=>new RestorationDruidSimRunner().Run(request,token),token));}
        catch(ArgumentException ex){return BadRequest(new {Error=ex.Message});}
        catch(SimulationEventLimitExceededException ex)
        {return UnprocessableEntity(new {Error=ex.Message});}
    }
}
