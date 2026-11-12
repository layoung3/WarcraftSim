using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175ClassBuildWorkflow
{
    private static ForeverClassBuild Build(string cls="rogue")
    {
        var build=ForeverClassBuildService.Create(cls);
        build.AcceptPartialCoverage=true;
        build.Character.Encounter.DurationSeconds=12m;
        return build;
    }
    private static JsonElement Json(object value)=>ForeverClassBuildJson.Element(value);
    private static JsonElement Ok(IActionResult response)=>Json(Assert.IsType<OkObjectResult>(response).Value!);
    private static object? BrowserNumbers(JsonElement e)=>e.ValueKind switch {
        JsonValueKind.Object=>e.EnumerateObject().ToDictionary(x=>x.Name,x=>BrowserNumbers(x.Value)),
        JsonValueKind.Array=>e.EnumerateArray().Select(BrowserNumbers).ToArray(),
        JsonValueKind.Number=>e.GetDouble(),JsonValueKind.String=>e.GetString(),
        JsonValueKind.True=>true,JsonValueKind.False=>false,_=>null};

    [Theory]
    [InlineData("shaman",50)]
    [InlineData("rogue",54)]
    public void EveryCurrentNodeHasIndependentSourceCoverageAndLegalRankContract(string cls,int count)
    {
        var nodes=ForeverClassBuildCatalog.Talents(cls);
        Assert.Equal(count,nodes.Count);Assert.Equal(3,nodes.Select(x=>x.Tree).Distinct().Count());
        Assert.Equal(count,nodes.Select(x=>x.Key).Distinct().Count());
        Assert.All(nodes,node=>{
            Assert.InRange(node.MaxRank,1,5);Assert.InRange(node.Row,1,7);
            Assert.StartsWith("https://",node.SourceUrl);
            Assert.NotEmpty(node.SourceConfidence);
            Assert.Contains(node.ImplementationStatus,new[]{"implemented","partial","unimplemented"});
            Assert.NotEmpty(node.ScenarioConditions);
            if(node.ImplementationStatus!="implemented")Assert.NotEmpty(node.MissingEffects);
        });
        foreach(var preset in ForeverClassBuildCatalog.Presets(cls)){
            var build=ForeverClassBuildService.ApplyPreset(Build(cls),preset.Key);
            var result=ForeverClassBuildService.Validate(build);
            Assert.True(result.IsLegal,string.Join(" ",result.LegalityErrors));
            Assert.True(result.CanExecute,string.Join(" ",result.ExecutionBlockers));
            Assert.Equal(preset.Ranks.Values.Sum(),result.AllocatedPoints);
            Assert.Equal(51-result.AllocatedPoints,result.UnspentPoints);
            Assert.NotEmpty(preset.Provenance);
        }
    }

    [Theory]
    [InlineData("rogue","master-of-deception")]
    public void MissingMechanicsDoNotMakeRankAllocationIllegal(string cls,string key)
    {
        var build=Build(cls);build.Character.ItemizedTalents=new(){[key]=1};build.AcceptPartialCoverage=false;
        var result=ForeverClassBuildService.Validate(build);
        Assert.True(result.IsLegal);Assert.True(result.CanSave);Assert.False(result.CanExecute);
        Assert.Contains(result.CoverageWarnings,x=>x.Contains("unimplemented"));
        build.AcceptPartialCoverage=true;
        result=ForeverClassBuildService.Validate(build);
        Assert.True(result.CanExecute,string.Join(" ",result.ExecutionBlockers));
        Assert.NotEmpty(result.CoverageWarnings);
    }

    [Theory]
    [InlineData("shaman","shaman-enhancement-restoration")]
    [InlineData("rogue","rogue-subtlety")]
    public void ApplyingPresetChangesOnlyRanksAndProvenanceAndRevalidatesCustomActions(string cls,string preset)
    {
        var original=Build(cls);var c=original.Character;
        c.Race="troll";c.Specialization="origin-label-only";
        c.Equipment.Items.Add(new(){SlotKey="chest",ItemKey="manual",Stats=new(){Values=new(){["stamina"]=17m}}});
        c.Modifiers.Buffs=["battle-shout"];c.CombatItems.MajorHealingPotionCharges=2;
        c.Encounter.TargetArmor=1777m;c.Encounter.Seed=991;
        c.Rotation.Mode="custom";c.Rotation.Priority=["unavailable-action-kept-for-editing"];
        c.ClassSettings=cls=="shaman"?Json(new{targetDistanceYards=2m}):Json(new{isBehindTarget=true});
        var before=JsonSerializer.Serialize(original,ForeverClassBuildJson.Options);
        var applied=ForeverClassBuildService.ApplyPreset(original,preset);
        var expected=ForeverClassBuildJson.Clone(original);expected.Character.ItemizedTalents=applied.Character.ItemizedTalents;
        expected.PresetOrigin=preset;
        Assert.Equal(JsonSerializer.Serialize(expected,ForeverClassBuildJson.Options),JsonSerializer.Serialize(applied,ForeverClassBuildJson.Options));
        Assert.Equal(before,JsonSerializer.Serialize(original,ForeverClassBuildJson.Options));
        var result=ForeverClassBuildService.Validate(applied);
        Assert.True(result.IsLegal);Assert.False(result.CanExecute);Assert.NotEmpty(result.ExecutionBlockers);
        Assert.Equal("unavailable-action-kept-for-editing",Assert.Single(applied.Character.Rotation.Priority));
    }

    [Theory]
    [InlineData("shaman")]
    [InlineData("rogue")]
    public void IdenticalEffectiveBuildsIgnoreStartingPresetAndSpecialization(string cls)
    {
        var a=Build(cls);var b=ForeverClassBuildJson.Clone(a);
        a.PresetOrigin="first";b.PresetOrigin="second";
        a.Character.Specialization="first-origin";b.Character.Specialization="another-origin";
        a.Character.Encounter.CaptureTimeline=false;b.Character.Encounter.CaptureTimeline=false;
        var aa=Json(ForeverClassBuildService.Run(a,default));var bb=Json(ForeverClassBuildService.Run(b,default));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(aa.GetProperty("simulation").GetRawText()),JsonNode.Parse(bb.GetProperty("simulation").GetRawText())));
    }

    [Fact]
    public void ZeroAndPartialRanksRemainEditableButTierParentAndBudgetErrorsBlockExecution()
    {
        var build=Build();build.Character.ItemizedTalents=new(){["malice"]=0};
        Assert.True(ForeverClassBuildService.Validate(build).CanExecute);
        build.Character.ItemizedTalents["malice"]=2;
        Assert.Equal(2,ForeverClassBuildService.Validate(build).AllocatedPoints);
        build.Character.ItemizedTalents["lethality"]=1;
        var invalid=ForeverClassBuildService.Validate(build);
        Assert.False(invalid.IsLegal);Assert.Contains(invalid.LegalityErrors,x=>x.Contains("malice rank 5"));
        build.Character.ItemizedTalents=new(){["made-up-zero"]=0};
        Assert.False(ForeverClassBuildService.Validate(build).CanSave);
        build.Character.ItemizedTalents=new(){["malice"]=6};
        Assert.False(ForeverClassBuildService.Validate(build).IsLegal);
    }

    [Fact]
    public void StrictBoundaryRejectsUnknownDuplicateAndCaseAliasedInputsWithoutMutation()
    {
        var build=Build();var text=JsonSerializer.Serialize(build,ForeverClassBuildJson.Options);
        var controller=new ForeverClassBuildController();
        foreach(var invalid in new[]{text.Insert(1,"\"unknown\":true,"),text.Insert(1,"\"schema\":\"duplicate\","),text.Insert(1,"\"SCHEMA\":\"alias\"," )}){
            using var doc=JsonDocument.Parse(invalid);
            Assert.IsType<BadRequestObjectResult>(controller.Validate(doc.RootElement));
        }
        Assert.Equal(text,JsonSerializer.Serialize(build,ForeverClassBuildJson.Options));
        build.Character.Rotation.Priority=null!;
        Assert.False(ForeverClassBuildService.Validate(build).IsLegal);
        Assert.IsType<BadRequestObjectResult>(controller.Apply(Json(new {
            build=new ForeverClassBuild{Character=null!},presetKey="rogue-subtlety"})));
    }

    [Fact]
    public void ImportReturnsCanonicalDefaultsAndRejectsNullCollectionsBeforeReplacement()
    {
        var controller=new ForeverClassBuildController();var original=Build();
        var canonical=JsonNode.Parse(Json(original).GetRawText())!;
        canonical["character"]!.AsObject().Remove("classSettings");
        var imported=Ok(controller.Import(Json(canonical)));
        Assert.Equal(JsonValueKind.Object,imported.GetProperty("build").GetProperty("character").GetProperty("classSettings").ValueKind);
        canonical["character"]!.AsObject().Remove("itemizedTalents");
        Assert.IsType<BadRequestObjectResult>(controller.Import(Json(canonical)));
        foreach(var field in new[]{"settings","buffs","gear"}){
            var bad=JsonNode.Parse(Json(original).GetRawText())!;
            if(field=="settings")bad["character"]!["classSettings"]=null;
            else if(field=="buffs")bad["character"]!["modifiers"]!["buffs"]=null;
            else bad["character"]!["equipment"]!["items"]!.AsArray().Add((JsonNode?)null);
            Assert.IsType<BadRequestObjectResult>(controller.Import(Json(bad)));
        }
        Assert.True(ForeverClassBuildService.Validate(original).CanExecute);
    }

    [Theory]
    [InlineData("shaman")]
    [InlineData("rogue")]
    public async Task CompleteReportRoundtripChecksRequestAndRejectsAlteredDamage(string cls)
    {
        var controller=new ForeverClassBuildController();
        var report=Ok(await controller.Simulate(Json(Build(cls)),default));
        Assert.True(Ok(controller.ValidateReport(report)).GetProperty("valid").GetBoolean());
        var browserReport=Json(BrowserNumbers(report)!);
        Assert.True(Ok(controller.ValidateReport(browserReport)).GetProperty("valid").GetBoolean());
        var changed=JsonNode.Parse(report.GetRawText())!;
        changed["simulation"]!["dps"]=999999;
        Assert.IsType<BadRequestObjectResult>(controller.ValidateReport(Json(changed)));
        changed=JsonNode.Parse(report.GetRawText())!;
        changed["originalSimulationRequest"]!["seed"]=881;
        Assert.IsType<BadRequestObjectResult>(controller.ValidateReport(Json(changed)));
        changed=JsonNode.Parse(report.GetRawText())!;changed["preparation"]!["attackPower"]=99999;
        Assert.IsType<BadRequestObjectResult>(controller.ValidateReport(Json(changed)));
        changed=JsonNode.Parse(report.GetRawText())!;changed["coverage"]!.AsArray().RemoveAt(0);
        Assert.IsType<BadRequestObjectResult>(controller.ValidateReport(Json(changed)));
        changed=JsonNode.Parse(report.GetRawText())!;changed["simulation"]!["run"]!["timeline"]=new JsonArray((JsonNode?)null);
        Assert.IsType<BadRequestObjectResult>(controller.ValidateReport(Json(changed)));
    }

    [Fact]
    public async Task PairedComparisonUsesActualIsolatedRunsAndSeparateValidationSeeds()
    {
        var a=Build();a.Character.ItemizedTalents=new(){["malice"]=2};a.Character.Encounter.Seed=7;
        var b=ForeverClassBuildJson.Clone(a);b.Character.ItemizedTalents["malice"]=5;
        var controller=new ForeverClassBuildController();
        var request=new ForeverClassBuildComparisonRequest{Baseline=a,Candidate=b,Iterations=3,ValidationIterations=2};
        var result=Ok(await controller.Compare(Json(request),default));
        var seeds=result.GetProperty("training").GetProperty("samples").EnumerateArray().Select(x=>x.GetProperty("seed").GetInt32()).ToArray();
        var validationSeeds=result.GetProperty("validation").GetProperty("samples").EnumerateArray().Select(x=>x.GetProperty("seed").GetInt32()).ToArray();
        Assert.Equal(new[]{7,8,9},seeds);Assert.Equal(new[]{1000010,1000011},validationSeeds);Assert.Empty(seeds.Intersect(validationSeeds));
        var first=result.GetProperty("training").GetProperty("samples")[0];
        var aa=ForeverClassBuildJson.Clone(a);aa.Character.Encounter.CaptureTimeline=false;
        var direct=Json(ForeverClassBuildService.Run(aa,default)).GetProperty("simulation").GetProperty("dps").GetDecimal();
        Assert.Equal(direct,first.GetProperty("baselineDps").GetDecimal());
        // Three samples use t(0.975,2)=4.303, not a large-sample normal interval.
        var differences=result.GetProperty("training").GetProperty("samples").EnumerateArray().Select(x=>x.GetProperty("deltaDps").GetDecimal()).ToArray();
        var mean=differences.Average();var sampleSd=(decimal)Math.Sqrt((double)(differences.Sum(x=>(x-mean)*(x-mean))/2m));
        Assert.Equal(mean+4.303m*sampleSd/(decimal)Math.Sqrt(3),result.GetProperty("training").GetProperty("meanDeltaUpper95").GetDecimal());
        Assert.Equal(7,a.Character.Encounter.Seed);Assert.True(a.Character.Encounter.CaptureTimeline);
        b.Character.Encounter.TargetArmor++;
        Assert.IsType<BadRequestObjectResult>(await controller.Compare(Json(request),default));
    }

    [Fact]
    public async Task ComparisonBoundsAndCancellationStopBeforeUnboundedWork()
    {
        var controller=new ForeverClassBuildController();var build=Build();
        var request=new ForeverClassBuildComparisonRequest{Baseline=build,Candidate=build,Iterations=41,ValidationIterations=2};
        Assert.IsType<BadRequestObjectResult>(await controller.Compare(Json(request),default));
        request.Iterations=2;using var cts=new CancellationTokenSource();cts.Cancel();
        var cancelled=Assert.IsType<ObjectResult>(await controller.Compare(Json(request),cts.Token));
        Assert.Equal(499,cancelled.StatusCode);
    }

    [Theory]
    [InlineData(-500002)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task ComparisonRejectsAliasedNegativeOrOverflowingRandomSeeds(int seed)
    {
        var build=Build();build.Character.Encounter.Seed=seed;
        var result=await new ForeverClassBuildController().Compare(Json(new ForeverClassBuildComparisonRequest{
            Baseline=build,Candidate=build,Iterations=2,ValidationIterations=2}),default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ComparisonNormalizesEffectiveShamanDefaultsAndDecimalRepresentations()
    {
        var a=Build("shaman");var b=ForeverClassBuildJson.Clone(a);
        b.Character.ClassSettings=Json(new {classWideTargetCount=1,targetDistanceYards=3.0m,
            incomingDamageSchool="physical",classWideTargetInitialHealthPercent=100.0m,
            utilityScenario=new ShamanClassWideUtilityScenario()});
        var result=Ok(await new ForeverClassBuildController().Compare(Json(new ForeverClassBuildComparisonRequest{
            Baseline=a,Candidate=b,Iterations=2,ValidationIterations=2}),default));
        Assert.Equal(0m,result.GetProperty("training").GetProperty("delta").GetProperty("mean").GetDecimal());
        Assert.Equal(0m,result.GetProperty("validation").GetProperty("delta").GetProperty("mean").GetDecimal());
    }

    [Fact]
    public void InventoryIncludesEveryClassAndEveryTreeWithoutClaimingAllClassCompletion()
    {
        var result=Ok(new ForeverClassBuildController().Coverage());
        Assert.False(result.GetProperty("completeAllClasses").GetBoolean());
        var classes=result.GetProperty("classes").EnumerateArray().ToArray();Assert.Equal(9,classes.Length);
        Assert.All(classes,c=>{Assert.Equal(3,c.GetProperty("trees").GetArrayLength());Assert.Equal(3,c.GetProperty("treeCoverage").GetArrayLength());Assert.False(c.GetProperty("allTreeCompletion").GetBoolean());Assert.Equal(JsonValueKind.Array,c.GetProperty("missingCode").ValueKind);});
        var paladin=classes.Single(c=>c.GetProperty("classKey").GetString()=="paladin");Assert.Equal("missing",paladin.GetProperty("engine").GetString());
    }
}
