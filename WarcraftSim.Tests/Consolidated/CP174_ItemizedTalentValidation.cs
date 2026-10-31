using System;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Mage;
using System.Threading;
using System.Threading.Tasks;
using WarcraftSim.Api.Controllers;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp174ItemizedTalentValidationTests
{
    private static IActionResult Validate(string cls,string spec,object request)=>
        new ForeverSoloTalentController().ValidateItemized(cls,spec,
            JsonSerializer.SerializeToElement(request));

    [Fact]
    public void RealRankedTierOneAllocationHasNoSyntheticPoints()
    {
        var response=Assert.IsType<OkObjectResult>(Validate("mage","arcane",
            new { ranks=new Dictionary<string,int> {["improved-channeling"]=5},
                  arcanePointsSpent=5,firePointsSpent=0,frostPointsSpent=0 }));
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        Assert.Equal(5,doc.RootElement.GetProperty("pointsSpent").GetInt32());
        Assert.Equal(46,doc.RootElement.GetProperty("remaining").GetInt32());
        Assert.Equal(5,doc.RootElement.GetProperty("treePoints").GetProperty("arcane").GetInt32());
    }

    [Fact]
    public void SpoofedAggregateCannotUnlockDeepTalentOrHideUnspentRanks()
    {
        Assert.IsType<BadRequestObjectResult>(Validate("mage","fire",
            new { ranks=new Dictionary<string,int> {["heating-up"]=1}, firePointsSpent=41 }));
        Assert.IsType<BadRequestObjectResult>(Validate("mage","arcane",
            new { ranks=new Dictionary<string,int> {["improved-channeling"]=5},arcanePointsSpent=41 }));
    }

    [Fact]
    public void HistoricalUnmodeledTalentCountsWithoutGrantingCombat()
    {
        var response=Assert.IsType<OkObjectResult>(Validate("mage","arcane",
            new { ranks=new Dictionary<string,int> {["wand-specialization"]=2} }));
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        Assert.Equal(2,doc.RootElement.GetProperty("pointsSpent").GetInt32());
        var missing=Assert.Single(doc.RootElement.GetProperty("unmodeledSelections").EnumerateArray());
        Assert.Equal("wand-specialization",missing.GetProperty("key").GetString());
        Assert.IsType<BadRequestObjectResult>(Validate("mage","arcane",
            new { ranks=new Dictionary<string,int> {["arcane-focus"]=5} }));
    }

    [Fact]
    public void UnrecognizedTalentAndRankOverflowFailClosed()
    {
        Assert.IsType<BadRequestObjectResult>(Validate("warlock","affliction",
            new { ranks=new Dictionary<string,int> {["invented-rank"]=1} }));
        Assert.IsType<BadRequestObjectResult>(Validate("mage","arcane",
            new { ranks=new Dictionary<string,int> {["improved-channeling"]=6} }));
    }

    [Fact]
    public async Task UnifiedEndpointRejectsSpoofedAggregatesInStrictItemizedMode()
    {
        var mage=MageSimRequest.CreatePreset("arcane");
        mage.Character.Race="human";mage.UseProvisionalLevel60Baseline=true;
        mage.Talents=new ForeverMageTalentBuild{
            ArcanePointsSpent=41,Ranks=new(StringComparer.OrdinalIgnoreCase){
                ["improved-channeling"]=5
            }
        };
        var request=new ForeverLevel60SimulationRequest{
            ClassKey="mage",Specialization="arcane",ItemizedTalentMode=true,Mage=mage
        };
        var result=await new ForeverLevel60SimulationController().Simulate(request,CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
        mage.Talents.ArcanePointsSpent=5;
        mage.DurationSeconds=8m;mage.TargetMaximumHealth=1000000m;
        var accepted=await new ForeverLevel60SimulationController().Simulate(request,CancellationToken.None);
        Assert.IsType<OkObjectResult>(accepted);
    }

    [Fact]
    public void ReportMetadataDisclosesRulesetSourcesAndNoInventedLocalCommit()
    {
        var ok=Assert.IsType<OkObjectResult>(new ForeverLevel60SimulationController().ReportMetadata());
        using var doc=JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.Equal("wow-forever",doc.RootElement.GetProperty("ruleset").GetString());
        Assert.Equal("warcraftsim.solo-dps-report",doc.RootElement.GetProperty("reportSchema").GetString());
        Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("sourceCommit").GetString()));
    }
}
