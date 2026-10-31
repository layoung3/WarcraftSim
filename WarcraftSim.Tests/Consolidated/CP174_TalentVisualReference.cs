using System;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp174TalentVisualReference
{
    [Theory]
    [InlineData("mage","arcane")]
    [InlineData("mage","fire")]
    [InlineData("mage","frost")]
    [InlineData("warlock","affliction")]
    [InlineData("warlock","demonology")]
    [InlineData("warlock","destruction")]
    [InlineData("hunter","marksmanship")]
    [InlineData("hunter","survival")]
    [InlineData("hunter","beast-mastery")]
    public void EverySupportedTreeRetainsEngineOwnedCapsAndSourceStatus(string cls,string spec)
    {
        var action=new ForeverSoloTalentController().Catalog(cls,spec);
        var result=Assert.IsType<OkObjectResult>(action);
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var entries=json.RootElement.GetProperty("talents").EnumerateArray().ToArray();
        Assert.NotEmpty(entries);
        Assert.All(entries,node=>
        {
            Assert.InRange(node.GetProperty("maxRank").GetInt32(),1,5);
            if(node.GetProperty("row").ValueKind==JsonValueKind.Number)
            {
                Assert.InRange(node.GetProperty("row").GetInt32(),1,7);
                Assert.InRange(node.GetProperty("column").GetInt32(),1,4);
                Assert.NotEqual("unmatched-engine-talent",
                    node.GetProperty("visualSourceStatus").GetString());
            }
        });
    }

    [Fact]
    public void Mage70685HotfixOverridesOlderTreeCoordinate()
    {
        var result=Assert.IsType<OkObjectResult>(
            new ForeverSoloTalentController().Catalog("mage","arcane"));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var missile=Assert.Single(json.RootElement.GetProperty("talents").EnumerateArray(),
            x=>x.GetProperty("key").GetString()=="missile-barrage");
        Assert.Equal(2,missile.GetProperty("row").GetInt32());
        Assert.Equal(4,missile.GetProperty("column").GetInt32());
        Assert.Contains("hotfix",missile.GetProperty("visualSourceStatus").GetString()!,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(json.RootElement.GetProperty("talents").EnumerateArray(),
            x=>x.GetProperty("key").GetString()=="arcane-focus");
    }

    [Fact]
    public void HistoricalReferenceNeverApprovesMismatchedRankCap()
    {
        Assert.Null(ForeverTalentVisualReferenceCatalog.Match("mage","fire","ignite",999));
        Assert.Null(ForeverTalentVisualReferenceCatalog.Match("mage","arcane","not-a-talent",5));
        Assert.NotNull(ForeverTalentVisualReferenceCatalog.Match("mage","fire","ignite",5));
        Assert.Equal(40,ForeverTalentVisualReferenceCatalog.SourceCommit.Length);
    }
}
