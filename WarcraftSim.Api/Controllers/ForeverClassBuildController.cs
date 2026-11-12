using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Batch;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Controllers;

[ApiController]
[Route("api/forever/shared/class-builds")]
public sealed class ForeverClassBuildController : ControllerBase
{
    [HttpGet("coverage")]
    public IActionResult Coverage() => Ok(new {reviewedAt=ForeverClassBuildCatalog.ReviewedAt,
        classes=ForeverClassBuildCoverage.All(),completeAllClasses=false});

    [HttpGet("{classKey}/catalog")]
    public IActionResult Catalog(string classKey)
    {
        try{return Ok(new {classKey,trees=ForeverClassBuildCatalog.Trees(classKey),
            talents=ForeverClassBuildCatalog.Talents(classKey),presets=ForeverClassBuildCatalog.Presets(classKey),
            executable=ForeverClassBuildCatalog.CanExecuteClassBuild(classKey),
            races=ForeverPlayableRaceCatalog.Races.Where(x=>x.Classes.Contains(classKey)).Select(x=>new{x.Key,x.Name}),
            level=60,pointBudget=51,defaultBuild=ForeverClassBuildCatalog.CanExecuteClassBuild(classKey)?ForeverClassBuildService.Create(classKey):null});}
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
    }

    [HttpGet("{classKey}/equipment")]
    public IActionResult Equipment(string classKey)
    {
        try{
            _=ForeverClassBuildCatalog.Trees(classKey);
            return Ok(new {items=ForeverEquipmentCatalog.GetCatalog().Items.Select(x=>new {
                item=x,eligibility=ForeverEquipmentEligibility.Assess(x,classKey)}),
                enchants=ForeverEquipmentCatalog.GetCatalog().Enchants,
                modifiers=ForeverExternalModifierCatalog.Entries,
                note="Pending eligibility remains editable and exportable. It is not permission to execute or a permanent class prohibition."});
        }
        catch(ArgumentException e){return BadRequest(new{error=e.Message});}
    }

    [HttpPost("validate")]
    public IActionResult Validate([FromBody] JsonElement input)
    {
        try{return Ok(ForeverClassBuildService.Validate(Read<ForeverClassBuild>(input)));}
        catch(Exception e) when(e is ArgumentException or JsonException or InvalidOperationException){return BadRequest(new{error=e.Message});}
    }

    [HttpPost("import")]
    public IActionResult Import([FromBody] JsonElement input)
    {
        try {
            var build=Read<ForeverClassBuild>(input);
            var validation=ForeverClassBuildService.Validate(build);
            if(!validation.CanSave)throw new ArgumentException(string.Join(" ",validation.LegalityErrors));
            // Server-defaulted transport fields are returned, without repairing
            // ranks, dropping pending gear or replacing a custom priority.
            return Ok(new {build,validation});
        }
        catch(Exception e) when(e is ArgumentException or JsonException or InvalidOperationException){return BadRequest(new{error=e.Message});}
    }

    public sealed class ApplyPresetRequest
    {
        public ForeverClassBuild Build {get;set;}=new();
        public string PresetKey {get;set;}="";
    }
    [HttpPost("apply-talent-preset")]
    public IActionResult Apply([FromBody] JsonElement input)
    {
        try{
            var request=Read<ApplyPresetRequest>(input);
            var build=ForeverClassBuildService.ApplyPreset(request.Build,request.PresetKey);
            return Ok(new {build,validation=ForeverClassBuildService.Validate(build)});
        }
        catch(Exception e) when(e is ArgumentException or JsonException or InvalidOperationException){return BadRequest(new{error=e.Message});}
    }

    [HttpPost("simulate")]
    public async Task<IActionResult> Simulate([FromBody] JsonElement input,CancellationToken token)
    {
        try{
            var build=Read<ForeverClassBuild>(input);
            return Ok(await Task.Run(()=>ForeverClassBuildService.Run(build,token),token));
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){return StatusCode(499,new{error="Simulation cancelled."});}
        catch(Exception e) when(e is ArgumentException or JsonException or InvalidOperationException){return BadRequest(new{error=e.Message});}
    }

    [HttpPost("validate-report")]
    public IActionResult ValidateReport([FromBody] JsonElement input)
    {
        try {
            var report=Read<ForeverClassBuildReport>(input);
            if(report.Schema!="warcraftsim.class-build-report" || report.Version!=1 ||
                report.Validation is null || string.IsNullOrWhiteSpace(report.SourceCommit) ||
                report.Simulation.ValueKind!=JsonValueKind.Object)
                throw new ArgumentException("A complete version-1 class-build report is required.");
            var validation=ForeverClassBuildService.Validate(report.Build);
            if(!validation.CanExecute)throw new ArgumentException(string.Join(" ",validation.LegalityErrors.Concat(validation.ExecutionBlockers)));
            var prepared=ForeverClassBuildService.Prepare(report.Build);
            if(!SameSnapshot(ForeverClassBuildJson.Element(report.Validation),validation))
                throw new ArgumentException("Report preparation/coverage validation does not match the current build adapter.");
            if(!SameSnapshot(report.Preparation,prepared.Preview) ||
                !SameSnapshot(ForeverClassBuildJson.Element(report.Coverage),ForeverClassBuildCatalog.Talents(report.Build.Character.ClassKey)))
                throw new ArgumentException("Report top-level preparation or talent coverage snapshot is inconsistent.");
            var expected=JsonNode.Parse(JsonSerializer.Serialize(prepared.Request,ForeverClassBuildJson.Options))!;
            var recorded=JsonNode.Parse(report.OriginalSimulationRequest.GetRawText())!;
            // Profile identity is an ephemeral object identifier, not a combat input.
            expected["character"]?.AsObject().Remove("id");
            recorded["character"]?.AsObject().Remove("id");
            if(!SameSetting(JsonSerializer.SerializeToElement(expected),JsonSerializer.SerializeToElement(recorded),browserNumbers:true))
                throw new ArgumentException("Report request does not match its exported build under the current adapter.");
            decimal damage,dps;
            if(report.Build.Character.ClassKey=="shaman") {
                var simulation=Read<EnhancementShamanSimResponse>(report.Simulation);
                ValidateRun(simulation.Run,report.Build.Character.Encounter);
                damage=simulation.DamageDone;dps=simulation.Dps;
                if(simulation.Run is null || simulation.DamageByAbility is null ||
                    Math.Abs(simulation.DamageByAbility.Values.Sum()-damage)>.000001m)
                    throw new ArgumentException("Report damage summary is inconsistent.");
            } else {
                var simulation=Read<RogueSimResponse>(report.Simulation);
                ValidateRun(simulation.Run,report.Build.Character.Encounter);
                damage=simulation.DamageDone;dps=simulation.Dps;
                if(simulation.Run is null || simulation.DamageByAbility is null ||
                    Math.Abs(simulation.DamageByAbility.Values.Sum()-damage)>.000001m)
                    throw new ArgumentException("Report damage summary is inconsistent.");
            }
            if(damage<0m || dps<0m || Math.Abs(dps*report.Build.Character.Encounter.DurationSeconds-damage)>.000001m)
                throw new ArgumentException("Report damage, duration and DPS disagree.");
            return Ok(new{valid=true,validation,
                replayStatus="Structurally checked imported report; run deterministic replay to verify combat results."});
        }
        catch(Exception e) when(e is ArgumentException or JsonException or InvalidOperationException){return BadRequest(new{error=e.Message});}
    }

    private static void ValidateRun(SimulationRunResult? run,ForeverSharedEncounterConfiguration encounter)
    {
        if(run?.Summary is null || run.Timeline is null || run.CustomMetrics is null ||
            run.Timeline.Any(e=>e is null || !Enum.IsDefined(e.Type) || e.TimeSeconds<0m ||
                e.TimeSeconds>encounter.DurationSeconds) || encounter.CaptureTimeline && run.Timeline.Count==0)
            throw new ArgumentException("Report requires a well-formed combat summary and captured event collection.");
    }

    [HttpPost("compare")]
    public async Task<IActionResult> Compare([FromBody] JsonElement input,CancellationToken token)
    {
        try{
            var r=Read<ForeverClassBuildComparisonRequest>(input);
            if(r.Iterations is <2 or >40 || r.ValidationIterations is <2 or >40)
                throw new ArgumentException("Use 2–40 comparison seeds and 2–40 separate validation seeds (at most 160 actual runs).");
            var a=ForeverClassBuildJson.Clone(r.Baseline);var b=ForeverClassBuildJson.Clone(r.Candidate);
            if(a.Character?.Encounter is not {Seed:>=0 and <=2146483604})
                throw new ArgumentException("Comparison baseline seed must be 0–2146483604 so both cohorts have distinct nonnegative PRNG seeds without overflow.");
            foreach(var build in new[]{a,b}){
                var validation=ForeverClassBuildService.Validate(build);
                if(!validation.CanExecute)throw new ArgumentException(string.Join(" ",validation.LegalityErrors.Concat(validation.ExecutionBlockers)));
            }
            RequireSameScenario(a,b);
            var seed=a.Character.Encounter.Seed;
            var training=await Cohort(a,b,seed,r.Iterations,token);
            var validationCohort=await Cohort(a,b,seed+1000003,r.ValidationIterations,token);
            return Ok(new {schema="warcraftsim.class-build-comparison",version=1,
                baseline=a,candidate=b,training,validation=validationCohort,
                seedStrategy="paired consecutive seeds from the baseline seed; validation uses baselineSeed + 1000003; distinct nonnegative PRNG seeds and a fresh isolated engine per run",
                uncertaintyMethod="Approximate paired Student-t 95% interval: mean difference ± t(0.975, N−1) × sample standard deviation of paired differences / sqrt(N). Small samples remain uncertain.",
                uncertaintySource="https://itl.nist.gov/div898/handbook/eda/section3/eda3672.htm",
                notes=new[]{"These finite seed cohorts compare the selected builds and policies only.",
                    "The validation seeds are distinct from the comparison seeds. No universal optimality or current-server numerical certification is implied.",
                    "Each adapter retains its documented target and survival semantics; incompatible scenarios are rejected."}});
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested){return StatusCode(499,new{error="Comparison cancelled."});}
        catch(Exception e) when(e is ArgumentException or JsonException or InvalidOperationException){return BadRequest(new{error=e.Message});}
    }

    private static async Task<object> Cohort(ForeverClassBuild a,ForeverClassBuild b,int start,int count,CancellationToken token)
    {
        var samples=new List<(int Seed,decimal Baseline,decimal Candidate)>();
        for(var i=0;i<count;i++){
            token.ThrowIfCancellationRequested();var seed=start+i;
            async Task<decimal> One(ForeverClassBuild original){
                var copy=ForeverClassBuildJson.Clone(original);
                copy.Character.Encounter.Seed=seed;copy.Character.Encounter.CaptureTimeline=false;
                var result=await Task.Run(()=>ForeverClassBuildJson.Element(ForeverClassBuildService.Run(copy,token)),token);
                return result.GetProperty("simulation").GetProperty("dps").GetDecimal();
            }
            var baseline=await One(a);var candidate=await One(b);
            samples.Add((seed,baseline,candidate));
        }
        var delta=samples.Select(x=>x.Candidate-x.Baseline).ToArray();
        var distribution=SimulationBatchRunner.BuildDistribution(delta);
        var sampleSd=(decimal)Math.Sqrt((double)(delta.Sum(x=>(x-distribution.Mean)*(x-distribution.Mean))/(count-1)));
        var margin=StudentTCritical95[count-2]*sampleSd/(decimal)Math.Sqrt(count);
        return new {iterations=count,baseline=SimulationBatchRunner.BuildDistribution(samples.Select(x=>x.Baseline).ToArray()),
            candidate=SimulationBatchRunner.BuildDistribution(samples.Select(x=>x.Candidate).ToArray()),
            delta=distribution,meanDeltaLower95=distribution.Mean-margin,meanDeltaUpper95=distribution.Mean+margin,
            positiveDeltaSeeds=delta.Count(x=>x>0),samples=samples.Select(x=>new {seed=x.Seed,baselineDps=x.Baseline,candidateDps=x.Candidate,deltaDps=x.Candidate-x.Baseline})};
    }

    // NIST/SEMATECH 0.975 quantiles, df1..39, three decimal places.
    // Each admitted cohort has2..40 samples; no large-sample1.96 shortcut.
    private static readonly decimal[] StudentTCritical95 = [
        12.706m,4.303m,3.182m,2.776m,2.571m,2.447m,2.365m,2.306m,2.262m,2.228m,
        2.201m,2.179m,2.160m,2.145m,2.131m,2.120m,2.110m,2.101m,2.093m,2.086m,
        2.080m,2.074m,2.069m,2.064m,2.060m,2.056m,2.052m,2.048m,2.045m,2.042m,
        2.040m,2.037m,2.035m,2.032m,2.030m,2.028m,2.026m,2.024m,2.023m];

    private static void RequireSameScenario(ForeverClassBuild a,ForeverClassBuild b)
    {
        if(a.Character.ClassKey!=b.Character.ClassKey || a.Character.Level!=b.Character.Level)
            throw new ArgumentException("Compare the same class and level.");
        var ea=ForeverClassBuildJson.Clone(a.Character.Encounter);var eb=ForeverClassBuildJson.Clone(b.Character.Encounter);
        ea.Seed=eb.Seed=0;ea.CaptureTimeline=eb.CaptureTimeline=false;
        if(JsonSerializer.Serialize(ea)!=JsonSerializer.Serialize(eb))
            throw new ArgumentException("Comparison duration, target mitigation/Health and survival policy must match.");
        var fields=new[]{"enableBossAttacks","bossSwingSeconds","bossHitDamage","incomingBossHitDamage",
            "incomingBossSwingSeconds","incomingDamageSchool","additionalTargets","targetCount",
            "targetDistanceYards","targetInitialHealthPercent","classWideTargetCount",
            "classWideTargetInitialHealthPercent","bossAttacksUseWeapon","targetDodgePercent",
            "targetParryPercent","isBehindTarget","targetCreatureType","utilityScenario"};
        // Compare resolved defaults, not the spelling/omission of an equivalent
        // setting in an imported build. Preparation is isolated and read-only.
        var preparedA=ForeverClassBuildJson.Element(ForeverClassBuildService.Prepare(a).Request);
        var preparedB=ForeverClassBuildJson.Element(ForeverClassBuildService.Prepare(b).Request);
        foreach(var key in fields){
            var av=Setting(preparedA,key);var bv=Setting(preparedB,key);
            if(!SameSetting(av,bv))throw new ArgumentException($"Comparison scenario setting differs: {key}.");
        }
    }
    private static JsonElement Setting(JsonElement request,string key)
    {
        if(request.TryGetProperty(key,out var value) && value.ValueKind!=JsonValueKind.Null)return value;
        var shaman=request.TryGetProperty("classWideRotationPolicy",out _);
        object? fallback=shaman?key switch {
            "classWideTargetCount"=>1,"classWideTargetInitialHealthPercent"=>100m,
            "targetDistanceYards"=>3m,"incomingDamageSchool"=>"physical",
            "utilityScenario"=>new ShamanClassWideUtilityScenario {
                IncomingAttackKind=request.TryGetProperty("incomingDamageSchool",out var school) &&
                    school.ValueKind==JsonValueKind.String && school.GetString()!="physical"?"spell":"melee"},
            _=>null}:null;
        return JsonSerializer.SerializeToElement(fallback,ForeverClassBuildJson.Options);
    }
    private static bool SameSnapshot(JsonElement recorded,object expected) =>
        SameSetting(recorded,ForeverClassBuildJson.Element(expected),browserNumbers:true);

    private static bool SameSetting(JsonElement a,JsonElement b,bool browserNumbers=false)
    {
        if(a.ValueKind!=b.ValueKind)return false;
        // Browser exports round decimal JSON numbers to IEEE754 doubles. Match
        // that exact transport representation, not a broad numeric tolerance.
        // Combat formula assertions in .NET retain exact decimal arithmetic.
        if(a.ValueKind==JsonValueKind.Number)return browserNumbers?a.GetDouble()==b.GetDouble():a.GetDecimal()==b.GetDecimal();
        if(a.ValueKind==JsonValueKind.Object){
            var left=a.EnumerateObject().ToArray();var right=b.EnumerateObject().ToArray();
            return left.Length==right.Length && left.All(x=>b.TryGetProperty(x.Name,out var v)&&SameSetting(x.Value,v,browserNumbers));
        }
        if(a.ValueKind==JsonValueKind.Array)return a.GetArrayLength()==b.GetArrayLength() &&
            a.EnumerateArray().Zip(b.EnumerateArray()).All(x=>SameSetting(x.First,x.Second,browserNumbers));
        return a.ValueKind==JsonValueKind.String?a.GetString()==b.GetString():a.GetRawText()==b.GetRawText();
    }

    internal static T Read<T>(JsonElement value)
    {
        RejectDuplicates(value);
        if(typeof(T)==typeof(ForeverClassBuild))RequireWireBuild(value);
        if(typeof(T)==typeof(ApplyPresetRequest) || typeof(T)==typeof(ForeverClassBuildReport))
            RequireWireBuild(value.ValueKind==JsonValueKind.Object&&value.TryGetProperty("build",out var build)?build:default);
        if(typeof(T)==typeof(ForeverClassBuildComparisonRequest)){
            RequireWireBuild(value.ValueKind==JsonValueKind.Object&&value.TryGetProperty("baseline",out var baseline)?baseline:default);
            RequireWireBuild(value.ValueKind==JsonValueKind.Object&&value.TryGetProperty("candidate",out var candidate)?candidate:default);
        }
        return value.Deserialize<T>(ForeverClassBuildJson.Options)??throw new ArgumentException("Request cannot be null.");
    }
    private static void RequireWireBuild(JsonElement build)
    {
        if(build.ValueKind!=JsonValueKind.Object || !build.TryGetProperty("schema",out _) ||
            !build.TryGetProperty("version",out _) || !build.TryGetProperty("character",out var c) ||
            c.ValueKind!=JsonValueKind.Object || !c.TryGetProperty("classKey",out _) ||
            !c.TryGetProperty("race",out _) || !c.TryGetProperty("level",out _) ||
            !c.TryGetProperty("itemizedTalents",out var ranks) || ranks.ValueKind!=JsonValueKind.Object)
            throw new ArgumentException("Build import requires explicit schema, version, class, race, level and a rank map (empty is allowed). Missing ranks are never silently cleared.");
    }
    private static void RejectDuplicates(JsonElement element)
    {
        if(element.ValueKind==JsonValueKind.Array){foreach(var item in element.EnumerateArray())RejectDuplicates(item);return;}
        if(element.ValueKind!=JsonValueKind.Object)return;
        var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var property in element.EnumerateObject()){
            if(!keys.Add(property.Name))throw new ArgumentException("Duplicate or case-aliased JSON property: "+property.Name);
            RejectDuplicates(property.Value);
        }
    }
}
