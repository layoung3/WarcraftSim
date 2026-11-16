using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3D2MixedHunterFrontendValidation
{
    private static JsonElement Selection(Dictionary<string,int> ranks,
        int mm=0,int survival=0,int bm=0) =>
        JsonSerializer.SerializeToElement(new {
            ranks,marksmanshipPointsSpent=mm,survivalPointsSpent=survival,
            beastMasteryPointsSpent=bm
        },new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public void ItemizedSurvivalBeastMasteryTransfersOnlySupportedPetTalentKeys()
    {
        // Use the actual JSON fixture's talent payload, not a synthetic
        // aggregate: the frontend must pass both routes on the same ranks.
        using var doc=JsonDocument.Parse(System.IO.File.ReadAllText(
            FindFixture("cp175-f3d2-survival-beast-mastery.json")));
        var source=doc.RootElement.GetProperty("talents");
        var api=new ForeverSoloTalentController();
        Assert.IsType<OkObjectResult>(api.Validate("hunter","survival",source));
        Assert.IsType<OkObjectResult>(api.ValidateItemized("hunter","survival",source));
        var ranks=source.GetProperty("ranks").Deserialize<Dictionary<string,int>>()!;
        Assert.Equal(51,ranks.Values.Sum());
        Assert.Equal(5,ranks["endurance-training"]);
        Assert.Equal(2,ranks["focused-fire"]);
    }

    [Fact]
    public void ItemizedMarksmanshipSurvivalTracksValidFirstTierOnly()
    {
        using var doc=JsonDocument.Parse(System.IO.File.ReadAllText(
            FindFixture("cp175-f3d2-marksmanship-survival.json")));
        var source=doc.RootElement.GetProperty("talents");
        var api=new ForeverSoloTalentController();
        Assert.IsType<OkObjectResult>(api.Validate("hunter","marksmanship",source));
        Assert.IsType<OkObjectResult>(api.ValidateItemized("hunter","marksmanship",source));
    }

    [Fact]
    public void UnsupportedOrUnbudgetedCrossTreeTalentsStillFailClosed()
    {
        var api=new ForeverSoloTalentController();
        var survival=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase) {
            ["improved-tracking"]=5,["lacerating-strikes"]=1,
            ["endurance-training"]=5,["focused-fire"]=2,
            ["bestial-wrath"]=1
        };
        Assert.IsType<BadRequestObjectResult>(api.Validate(
            "hunter","survival",Selection(survival,survival:6,bm:8)));
        var mm=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){
            ["lethal-attacks"]=5,["lacerating-strikes"]=1
        };
        Assert.IsType<BadRequestObjectResult>(api.Validate(
            "hunter","marksmanship",Selection(mm,mm:5,survival:1)));
        mm.Clear();
        mm["improved-tracking"]=5;
        Assert.IsType<BadRequestObjectResult>(api.Validate(
            "hunter","marksmanship",Selection(mm,mm:5,survival:0)));
    }

    private static string FindFixture(string fileName)
    {
        var cwd=new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while(cwd is not null)
        {
            var p=System.IO.Path.Combine(cwd.FullName,"docs","acceptance",
                "cp175",fileName);
            if(System.IO.File.Exists(p))return p;
            cwd=cwd.Parent;
        }
        throw new InvalidOperationException("Missing checked-in F3D2 fixture.");
    }
}
