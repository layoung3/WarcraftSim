using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Batch;

namespace WarcraftSim.Api.Controllers;

/// <summary>
/// CP175 Stage E: repeated genuinely seeded Mage/Warlock/Hunter runs through the
/// SAME strict unified simulation gate. No duplicate combat formulas, no fake
/// distribution from a single simulation. Warrior's native batch remains on
/// /api/sim/warrior until its shared configuration adapter is validated.
/// </summary>
[ApiController]
[Route("api/forever/shared")]
public sealed class ForeverSharedIterationsController : ControllerBase
{
    public sealed class IterationRequest
    {
        public ForeverLevel60SimulationRequest? Configuration { get; set; }
        public int Iterations { get; set; } = 1;
    }

    private readonly record struct Sample(int Seed,decimal Dps);

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static ForeverLevel60SimulationRequest Clone(ForeverLevel60SimulationRequest source) =>
        JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(
            JsonSerializer.Serialize(source,JsonOptions),JsonOptions) ??
        throw new InvalidOperationException("Cannot clone the submitted typed character.");

    private static (int Seed,string Class,string Spec) SetSeed(
        ForeverLevel60SimulationRequest request,int? replaceSeed,bool captureTimeline)
    {
        var cls=request.ClassKey?.Trim().ToLowerInvariant()??"";
        var spec=request.Specialization?.Trim().ToLowerInvariant()??"";
        if(cls=="mage" && request.Mage is { } mage){
            var seed=mage.Seed;
            if(replaceSeed.HasValue)mage.Seed=replaceSeed.Value;
            mage.CaptureTimeline=captureTimeline;
            return(seed,cls,spec);
        }
        if(cls=="warlock" && request.Warlock is { } warlock){
            var seed=warlock.Seed;
            if(replaceSeed.HasValue)warlock.Seed=replaceSeed.Value;
            warlock.CaptureTimeline=captureTimeline;
            return(seed,cls,spec);
        }
        if(cls=="hunter" && spec=="marksmanship" && request.Marksmanship is { } mm){
            var seed=mm.Seed;
            if(replaceSeed.HasValue)mm.Seed=replaceSeed.Value;
            mm.CaptureTimeline=captureTimeline;
            return(seed,cls,spec);
        }
        if(cls=="hunter" && spec=="survival" && request.Survival is { } sv){
            var seed=sv.Seed;
            if(replaceSeed.HasValue)sv.Seed=replaceSeed.Value;
            sv.CaptureTimeline=captureTimeline;
            return(seed,cls,spec);
        }
        if(cls=="hunter" && spec=="beast-mastery" && request.BeastMastery is { } bm){
            var seed=bm.Seed;
            if(replaceSeed.HasValue)bm.Seed=replaceSeed.Value;
            bm.CaptureTimeline=captureTimeline;
            return(seed,cls,spec);
        }
        throw new ArgumentException("Iteration endpoint currently supports strict Mage, Warlock and Hunter typed requests. Warrior retains its existing seeded batch API.");
    }

    private static async Task<JsonElement> One(
        ForeverLevel60SimulationRequest configuration,int seed,bool timeline,
        CancellationToken token)
    {
        var instance=Clone(configuration);
        SetSeed(instance,seed,timeline);
        var action=await new ForeverLevel60SimulationController().Simulate(instance,token);
        if(action is not OkObjectResult ok)
            throw new ArgumentException("Iteration failed strict class/talent/baseline validation: "+
                JsonSerializer.Serialize((action as BadRequestObjectResult)?.Value,JsonOptions));
        return JsonSerializer.SerializeToElement(ok.Value,JsonOptions);
    }

    [HttpPost("simulate-iterations")]
    public async Task<IActionResult> SimulateIterations(
        [FromBody] IterationRequest? request,CancellationToken token)
    {
        if(request?.Configuration is null)
            return BadRequest(new{error="A strict typed level-60 configuration is required."});
        if(request.Iterations is <1 or >100)
            return BadRequest(new{error="Iteration count must be 1–100 in this initial endpoint."});
        try{
            var original=Clone(request.Configuration);
            var (baseSeed,cls,spec)=SetSeed(original,null,true);
            var samples=new List<Sample>(request.Iterations);
            JsonElement? oneRun=null;
            for(var i=0;i<request.Iterations;i++){
                token.ThrowIfCancellationRequested();
                var seed=unchecked(baseSeed+i);
                var response=await One(original,seed,request.Iterations==1,token);
                var sim=response.GetProperty("sim");
                samples.Add(new Sample(seed,sim.GetProperty("dps").GetDecimal()));
                if(request.Iterations==1)oneRun=response;
            }
            var distribution=SimulationBatchRunner.BuildDistribution(
                samples.Select(x=>x.Dps).ToArray());
            // Representative is a true rerun of a sampled seed, not a
            // synthetic timeline assembled from mean ability summaries.
            var representative=samples.OrderBy(x=>Math.Abs(x.Dps-distribution.Median))
                .ThenBy(x=>samples.IndexOf(x)).First();
            if(request.Iterations>1)
                oneRun=await One(original,representative.Seed,true,token);
            return Ok(new {
                schema="warcraftsim.level60-distribution",
                version=1,cls,specialization=spec,
                baseSeed,iterations=request.Iterations,
                seedStrategy="unchecked(baseSeed + iterationIndex), new engine state per run",
                standardDeviationMethod="population: sqrt(sum((x - mean)^2) / N)",
                percentileMethod="linear interpolation at p * (N - 1), sorted samples",
                hasStatisticalDistribution=request.Iterations>1,
                dps=distribution,
                samples=samples.Select(x=>new{seed=x.Seed,dps=x.Dps}).ToArray(),
                representativeSeed=representative.Seed,
                representative=oneRun,
                modelingNotes=new[]{
                    "Values are independent seeded simulations of the same input, not verified live-server balance.",
                    "Single-iteration mode is deterministic debugging; its P95/P99 values equal the one observed DPS, not a meaningful distribution.",
                    "Warrior has its own already-seeded batch route until its CP175 shared adapter is certified."
                }
            });
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){
            return StatusCode(499,new{error="Iteration run cancelled."});
        }
        catch(ArgumentException ex){return BadRequest(new{error=ex.Message});}
        catch(InvalidOperationException ex){return BadRequest(new{error=ex.Message});}
    }
}
