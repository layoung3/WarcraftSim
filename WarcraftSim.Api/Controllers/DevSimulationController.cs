using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Development;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Batch;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/dev/simulation")]
public sealed class DevSimulationController :
    ControllerBase
{
    [HttpGet("priority-test")]
    public ActionResult<SimulationRunResult>
        RunPriorityTest()
    {
        var result =
            DevelopmentSimulationFactory
                .RunPrioritySimulation(
                    seed: 12345,
                    captureTimeline: true
                );

        return Ok(
            result
        );
    }

    [HttpGet("batch-test")]
    public ActionResult<SimulationBatchResult>
        RunBatchTest(
            [FromQuery] int iterations = 1000)
    {
        var safeIterations =
            Math.Clamp(
                iterations,
                1,
                5000
            );

        var runner =
            new SimulationBatchRunner();

        var result =
            runner.Run(
                new SimulationBatchOptions
                {
                    Iterations =
                        safeIterations,

                    BaseSeed =
                        12345,

                    SimulationType =
                        SimulationType.Dps
                },

                (seed, captureTimeline) =>
                    DevelopmentSimulationFactory
                        .RunPrioritySimulation(
                            seed,
                            captureTimeline
                        )
            );

        return Ok(
            result
        );
    }
}
