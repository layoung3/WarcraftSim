using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;

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
