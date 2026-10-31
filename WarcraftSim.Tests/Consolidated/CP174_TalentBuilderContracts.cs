using System;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Warlock;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp174TalentBuilderContracts
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
    public void EverySupportedSpecPublishesEngineOwnedTalentKeys(string cls,string spec)
    {
        var endpoint=new ForeverSoloTalentController();
        var response=Assert.IsType<OkObjectResult>(endpoint.Catalog(cls,spec));
        using var document=JsonDocument.Parse(JsonSerializer.Serialize(response.Value));
        Assert.Equal(51,document.RootElement.GetProperty("maxPoints").GetInt32());
        var talents=document.RootElement.GetProperty("talents").EnumerateArray().ToArray();
        Assert.NotEmpty(talents);
        Assert.All(talents,x=>{
            Assert.False(string.IsNullOrWhiteSpace(x.GetProperty("key").GetString()));
            Assert.InRange(x.GetProperty("maxRank").GetInt32(),1,5);
        });
    }

    [Fact]
    public void BackendRejectsExcessiveRankAndUnknownSpec()
    {
        var api=new ForeverSoloTalentController();
        var mage=ForeverMageTalentBuild.Default("fire");
        mage.Ranks["ignite"]=6;
        var document=JsonSerializer.SerializeToElement(mage);
        Assert.IsType<BadRequestObjectResult>(api.Validate("mage","fire",document));
        Assert.IsType<BadRequestObjectResult>(api.Catalog("mage","shadow"));
    }

    [Fact]
    public void ExistingWarlockAndHunterValidationIsReused()
    {
        var api=new ForeverSoloTalentController();
        Assert.IsType<OkObjectResult>(api.Validate("warlock","demonology",
            JsonSerializer.SerializeToElement(ForeverWarlockTalentBuild.Default("demonology"))));
        var hunter=new ForeverSurvivalHunterTalents();
        hunter.Ranks["lacerating-strikes"]=1;
        Assert.IsType<BadRequestObjectResult>(api.Validate("hunter","survival",
            JsonSerializer.SerializeToElement(hunter)));
    }
}