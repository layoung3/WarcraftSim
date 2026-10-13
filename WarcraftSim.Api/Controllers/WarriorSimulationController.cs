using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/sim/warrior")]
public sealed class WarriorSimulationController : ControllerBase
{
    [HttpGet("catalog")]
    public ActionResult<ForeverEquipmentCatalogDocument> Catalog() =>
        Ok(ForeverEquipmentCatalog.GetCatalog());

    [HttpGet("support-catalog")]
    public ActionResult<ForeverSupportCatalogDocument> SupportCatalog() =>
        Ok(ForeverSupportEffectCatalog.GetCatalog());

    [HttpGet("talent-catalog")]
    public IActionResult TalentCatalog() => Ok(new
    {
        Version = ForeverWarriorTalentCatalog.Version,
        Level = 60,
        PointBudget = 51,
        Talents = ForeverWarriorTalentCatalog.Talents
    });

    [HttpGet("race-catalog")]
    public IActionResult RaceCatalog() => Ok(new
    {
        Version = ForeverWarriorRaceProfiles.Version,
        SourceUrl = ForeverWarriorRaceProfiles.SourceUrl,
        Races = ForeverWarriorRaceProfiles.Races
    });

    [HttpPost("run")]
    public async Task<ActionResult<WarriorSimResponse>> Run(
        [FromBody] WarriorSimRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { Error = "A Warrior simulation request is required." });

        try
        {
            // Keep the synchronous seeded simulation off the Kestrel request
            // continuation. Cancellation propagates into each event loop.
            var result = await Task.Run(
                () => new ForeverWarriorSimRunner().Run(request, cancellationToken),
                cancellationToken);
            return Ok(result);
        }
        catch (SimulationEventLimitExceededException exception)
        {
            return UnprocessableEntity(new { Error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { Error = exception.Message });
        }
    }
}
