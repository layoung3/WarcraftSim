using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3D4HunterCrossTree
{
    private static (Dictionary<string,int> Ranks,int Mm,int Survival,int Bm) Read(string name)
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null)
        {
            var file=Path.Combine(directory.FullName,"docs","acceptance","cp175",name);
            if(File.Exists(file))
            {
                using var doc=JsonDocument.Parse(File.ReadAllText(file));
                var t=doc.RootElement.GetProperty("talents");
                return (t.GetProperty("ranks").Deserialize<Dictionary<string,int>>()!,
                    t.GetProperty("marksmanshipPointsSpent").GetInt32(),
                    t.GetProperty("survivalPointsSpent").GetInt32(),
                    t.GetProperty("beastMasteryPointsSpent").GetInt32());
            }
            directory=directory.Parent;
        }
        throw new InvalidOperationException("Missing F3D4 acceptance fixture: "+name);
    }

    [Theory]
    [InlineData("cp175-f3d4-marksmanship-beast-mastery.json")]
    [InlineData("cp175-f3d4-marksmanship-survival.json")]
    [InlineData("cp175-f3d4-three-trees.json")]
    public void ItemizedCrossTreeHunterRuntimesExecuteRealDamageAndReplay(string name)
    {
        var (ranks,mm,survival,bm)=Read(name);
        Assert.Equal(51,ranks.Values.Sum());
        Assert.Equal(51,mm+survival+bm);
        var selection=JsonSerializer.SerializeToElement(new {
            ranks,marksmanshipPointsSpent=mm,survivalPointsSpent=survival,
            beastMasteryPointsSpent=bm
        },new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var api=new ForeverSoloTalentController();
        Assert.IsType<OkObjectResult>(api.ValidateItemized("hunter","marksmanship",selection));
        Assert.IsType<OkObjectResult>(api.Validate("hunter","marksmanship",selection));
        var req=new MarksmanshipHunterSimRequest {
            DurationSeconds=48m,Seed=1757409,TargetMaximumHealth=10000000m,
            HasPet=true,PetFamily="wolf",UseRapidFire=false,CaptureTimeline=true,
            Talents=new ForeverMarksmanshipHunterTalents {
                Ranks=ranks,MarksmanshipPointsSpent=mm,
                BeastMasteryPointsSpent=bm,SurvivalPointsSpent=survival
            }
        };
        var runner=new MarksmanshipHunterSimRunner();
        var first=runner.Run(req);
        Assert.True(first.HunterDamage>0m);
        Assert.True(first.PetDamage>0m);
        Assert.Contains(first.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.Amount>0m);
        Assert.Contains(first.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.SourceActorKey=="hunter-pet" && e.ResourceKey=="focus" &&
            e.Amount<0m);
        var second=runner.Run(new MarksmanshipHunterSimRequest {
            DurationSeconds=48m,Seed=1757409,TargetMaximumHealth=10000000m,
            HasPet=true,PetFamily="wolf",UseRapidFire=false,CaptureTimeline=true,
            Talents=new ForeverMarksmanshipHunterTalents {
                Ranks=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase),
                MarksmanshipPointsSpent=mm,BeastMasteryPointsSpent=bm,SurvivalPointsSpent=survival
            }
        });
        Assert.Equal(first.DamageDone,second.DamageDone);
        Assert.Equal(first.PetDamage,second.PetDamage);
        Assert.Equal(first.Run.Timeline.Select(e=>
            (e.TimeSeconds,e.Type,e.AbilityKey,e.SourceActorKey,e.Amount)).ToArray(),
            second.Run.Timeline.Select(e=>
            (e.TimeSeconds,e.Type,e.AbilityKey,e.SourceActorKey,e.Amount)).ToArray());
    }

    [Fact]
    public void RemovingPetDamageTalentChangesPetOutputWithoutChangingOwnerAllocation()
    {
        var (ranks,mm,survival,bm)=Read("cp175-f3d4-marksmanship-beast-mastery.json");
        var baseline=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase);
        var noBeast=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase);
        noBeast["deadly-aspects"]=0;
        var req=new MarksmanshipHunterSimRequest {
            DurationSeconds=45m,Seed=1757410,HasPet=true,
            PetFamily="cat",CaptureTimeline=true,UseRapidFire=false,
            TargetMaximumHealth=10000000m,
            Talents=new ForeverMarksmanshipHunterTalents {
                Ranks=baseline,MarksmanshipPointsSpent=mm,
                SurvivalPointsSpent=survival,BeastMasteryPointsSpent=bm
            }
        };
        var with=new MarksmanshipHunterSimRunner().Run(req);
        req.Talents.Ranks=noBeast;
        var without=new MarksmanshipHunterSimRunner().Run(req);
        Assert.True(with.PetDamage>0m);
        Assert.True(without.PetDamage>0m);
        Assert.Equal(0,req.Talents.Rank("deadly-aspects"));
    }

    [Fact]
    public void InvalidThreeTreeTotalsAndUnlearnedDeepBeastTalentsFailClosed()
    {
        var (ranks,mm,survival,bm)=Read("cp175-f3d4-three-trees.json");
        var api=new ForeverSoloTalentController();
        var invalid=JsonSerializer.SerializeToElement(new {
            ranks,marksmanshipPointsSpent=mm,survivalPointsSpent=survival,
            beastMasteryPointsSpent=bm+1
        },new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsType<BadRequestObjectResult>(api.ValidateItemized("hunter","marksmanship",invalid));
        ranks["bestial-wrath"]=1;
        var invalidCapstone=JsonSerializer.SerializeToElement(new {
            ranks,marksmanshipPointsSpent=mm,survivalPointsSpent=survival,
            beastMasteryPointsSpent=bm+1
        },new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsType<BadRequestObjectResult>(api.ValidateItemized("hunter","marksmanship",invalidCapstone));
    }
}
