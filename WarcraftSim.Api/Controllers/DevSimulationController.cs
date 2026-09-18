using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Development;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Batch;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/dev/simulation")]
public sealed class DevSimulationController : ControllerBase
{
    [HttpGet("priority-test")]
    public ActionResult<SimulationRunResult> RunPriorityTest() =>
        Ok(DevelopmentSimulationFactory.RunPrioritySimulation(12345, true));

    [HttpGet("batch-test")]
    public ActionResult<SimulationBatchResult> RunBatchTest(
        [FromQuery] int iterations = 1000)
    {
        var safeIterations = Math.Clamp(iterations, 1, 5000);
        var runner = new SimulationBatchRunner();

        return Ok(runner.Run(
            new SimulationBatchOptions
            {
                Iterations = safeIterations,
                BaseSeed = 12345,
                SimulationType = SimulationType.Dps
            },
            (seed, captureTimeline) =>
                DevelopmentSimulationFactory.RunPrioritySimulation(
                    seed,
                    captureTimeline)));
    }

    [HttpGet("resource-test")]
    public ActionResult<SimulationRunResult> RunResourceTest() =>
        Ok(DevelopmentResourceSimulationFactory.Run());

    [HttpGet("condition-test")]
    public ActionResult<SimulationRunResult> RunConditionTest() =>
        Ok(DevelopmentConditionSimulationFactory.Run());

    [HttpGet("encounter-phase-test")]
    public ActionResult<SimulationRunResult> RunEncounterPhaseTest() =>
        Ok(DevelopmentEncounterSimulationFactory.Run());

    [HttpGet("timing-test")]
    public ActionResult<SimulationRunResult> RunTimingTest() =>
        Ok(DevelopmentActionTimingSimulationFactory.Run());

    [HttpGet("healing-test")]
    public ActionResult<SimulationRunResult> RunHealingTest() =>
        Ok(DevelopmentHealingSimulationFactory.Run());

    [HttpGet("healing-interrupt-test")]
    public ActionResult<SimulationRunResult> RunHealingInterruptTest() =>
        Ok(DevelopmentHealingInterruptionSimulationFactory.Run());

    [HttpGet("healing-target-test")]
    public ActionResult<SimulationRunResult> RunHealingTargetTest() =>
        Ok(DevelopmentHealingTargetSimulationFactory.Run());
}
