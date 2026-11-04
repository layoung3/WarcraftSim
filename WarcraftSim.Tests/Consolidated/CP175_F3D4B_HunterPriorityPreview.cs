using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3D4BHunterPriorityPreview
{
    private static Dictionary<string,int> Ranks(string name)
    {
        var d=new DirectoryInfo(AppContext.BaseDirectory);
        while(d is not null)
        {
            var file=Path.Combine(d.FullName,"docs","acceptance","cp175",name);
            if(File.Exists(file))
            {
                using var json=JsonDocument.Parse(File.ReadAllText(file));
                return json.RootElement.GetProperty("talents").GetProperty("ranks")
                    .Deserialize<Dictionary<string,int>>()!;
            }
            d=d.Parent;
        }
        throw new InvalidOperationException("Missing F3D4B browser build "+name);
    }

    [Theory]
    [InlineData("marksmanship","cp175-f3d4b-marksmanship-survival-melee.json",
        "counterattack","strider-kick")]
    [InlineData("survival","cp175-f3d4b-survival-sniper-scatter.json",
        "sniper-shot","scatter-shot")]
    [InlineData("beast-mastery","cp175-f3d4b-beast-mastery-sniper-scatter.json",
        "sniper-shot","scatter-shot")]
    public void PreviewUsesActualSelectedRunnerAndIncludesLegalOffTreeActions(
        string spec,string file,string first,string second)
    {
        var ranks=Ranks(file);
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ForeverLevel60SimulationRequest request=new(){
            ClassKey="hunter",Specialization=spec
        };
        if(spec=="marksmanship")
            request.Marksmanship=new MarksmanshipHunterSimRequest {
                HasPet=false,UseMeleeAuto=true,ScriptedParryEverySeconds=8m,
                CustomPriority=[first,second],
                Talents=new ForeverMarksmanshipHunterTalents{
                    Ranks=ranks,MarksmanshipPointsSpent=29,SurvivalPointsSpent=22,
                    BeastMasteryPointsSpent=0
                }
            };
        else if(spec=="survival")
            request.Survival=new SurvivalHunterSimRequest {
                CombatMode="ranged",HasPet=true,CustomPriority=[first,second],
                Talents=new ForeverSurvivalHunterTalents{Ranks=ranks}
            };
        else
            request.BeastMastery=new BeastMasteryHunterSimRequest{
                PetEnabled=true,CustomPriority=[first,second],
                Talents=new ForeverBeastMasteryHunterTalents{Ranks=ranks}
            };
        var response=Assert.IsType<OkObjectResult>(
            new ForeverSoloRotationController().Preview(request));
        using var json=JsonDocument.Parse(JsonSerializer.Serialize(response.Value,options));
        var entries=json.RootElement.GetProperty("entries").EnumerateArray()
            .Select(e=>e.GetProperty("abilityKey").GetString()).ToArray();
        Assert.Contains(first,entries);
        Assert.Contains(second,entries);
        Assert.Equal("custom",json.RootElement.GetProperty("mode").GetString());
        Assert.True(json.RootElement.GetProperty("supported").GetBoolean());
    }

    [Fact]
    public void PreviewRejectsStaleUnlearnedCrossTreeAction()
    {
        var ranks=Ranks("cp175-f3d4b-survival-sniper-scatter.json");
        ranks.Remove("sniper-shot");
        var request=new ForeverLevel60SimulationRequest {
            ClassKey="hunter",Specialization="survival",
            Survival=new SurvivalHunterSimRequest{
                CombatMode="ranged",CustomPriority=["sniper-shot"],
                Talents=new ForeverSurvivalHunterTalents{Ranks=ranks}
            }
        };
        Assert.IsType<BadRequestObjectResult>(
            new ForeverSoloRotationController().Preview(request));
    }
}
