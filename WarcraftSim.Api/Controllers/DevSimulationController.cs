using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Development;
using WarcraftSim.Core.Simulation.Analysis;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/dev/simulation")]
public sealed class DevSimulationController :
    ControllerBase
{
    [HttpGet("combat-smoke")]
    public ActionResult<SimulationRunResult>
        RunCombatSmoke() =>
        Ok(
            DevelopmentScenarios
                .RunCombatSmoke()
        );

    [HttpGet("healing-throughput")]
    public ActionResult<HealingThroughputResult>
        RunHealingThroughput() =>
        Ok(
            DevelopmentScenarios
                .RunHealingThroughput()
        );

    [HttpGet("raid-healing")]
    public ActionResult<SimulationRunResult>
        RunRaidHealing() =>
        Ok(
            DevelopmentScenarios
                .RunRaidHealing()
        );
}
