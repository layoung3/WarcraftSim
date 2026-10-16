using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/balance-druid")]
public sealed class BalanceDruidSimController : ControllerBase
{
    [HttpGet("defaults")]
    public ActionResult<BalanceDruidSimRequest> Defaults()=>Ok(new BalanceDruidSimRequest());

    [HttpPost("run")]
    public async Task<ActionResult<BalanceDruidSimResponse>> Run([FromBody] BalanceDruidSimRequest request,
        CancellationToken token)
    {
        if(request is null)return BadRequest(new {Error="Balance Druid setup required."});
        try{return Ok(await Task.Run(()=>new BalanceDruidSimRunner().Run(request,token),token));}
        catch(ArgumentException ex){return BadRequest(new {Error=ex.Message});}
        catch(SimulationEventLimitExceededException ex){return UnprocessableEntity(new {Error=ex.Message});}
    }
}
