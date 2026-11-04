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

public sealed class Cp175F3D4BHunterActionAccess
{
    private static (Dictionary<string,int> Ranks,int Mm,int Sv,int Bm) Fixture(string name)
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null)
        {
            var file=Path.Combine(dir.FullName,"docs","acceptance","cp175",name);
            if(File.Exists(file))
            {
                using var data=JsonDocument.Parse(File.ReadAllText(file));
                var tree=data.RootElement.GetProperty("talents");
                return (tree.GetProperty("ranks").Deserialize<Dictionary<string,int>>()!,
                    tree.GetProperty("marksmanshipPointsSpent").GetInt32(),
                    tree.GetProperty("survivalPointsSpent").GetInt32(),
                    tree.GetProperty("beastMasteryPointsSpent").GetInt32());
            }
            dir=dir.Parent;
        }
        throw new InvalidOperationException("Missing cross-tree browser fixture "+name);
    }

    [Theory]
    [InlineData("survival","cp175-f3d4b-survival-sniper-scatter.json")]
    [InlineData("beast-mastery","cp175-f3d4b-beast-mastery-sniper-scatter.json")]
    public void LearnedRangedCapstoneAndScatterExecuteUnderOtherStartingPresets(
        string spec,string filename)
    {
        var (ranks,mm,sv,bm)=Fixture(filename);
        Assert.Equal(51,ranks.Values.Sum());
        var payload=JsonSerializer.SerializeToElement(new{
            ranks,marksmanshipPointsSpent=mm,survivalPointsSpent=sv,
            beastMasteryPointsSpent=bm},new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var validator=new ForeverSoloTalentController();
        Assert.IsType<OkObjectResult>(validator.ValidateItemized("hunter",spec,payload));
        Assert.IsType<OkObjectResult>(validator.Validate("hunter",spec,payload));
        var first=Run(spec,ranks,777133);
        var second=Run(spec,ranks,777133);
        Assert.True(first.Damage>0m);
        Assert.Contains(first.Events,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="scatter-shot" && e.SourceActorKey=="hunter");
        Assert.Contains(first.Events,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="sniper-shot" && e.SourceActorKey=="hunter");
        Assert.Contains(first.Events,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="scatter-shot" && e.ResourceKey=="mana" && e.Amount<0m);
        Assert.Contains(first.Events,e=>e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="sniper-shot" && e.ResourceKey=="mana" && e.Amount<0m);
        Assert.Equal(first.Damage,second.Damage);
        Assert.Equal(first.Events.Select(e=>(e.TimeSeconds,e.Type,e.AbilityKey,e.Amount)).ToArray(),
            second.Events.Select(e=>(e.TimeSeconds,e.Type,e.AbilityKey,e.Amount)).ToArray());
    }

    private static (decimal Damage,IReadOnlyList<CombatEvent> Events) Run(
        string spec,Dictionary<string,int> ranks,int seed)
    {
        var priority=new List<string>{"scatter-shot","sniper-shot","arcane-shot"};
        if(spec=="survival")
        {
            var r=new SurvivalHunterSimRequest{
                DurationSeconds=90m,Seed=seed,CombatMode="ranged",
                HasPet=true,PetFamily="cat",AdditionalTargets=0,
                CaptureTimeline=true,UseFireTraps=false,UseFrostTraps=false,
                UseRapidFire=false,UseAimedShot=false,UseMultiShot=false,
                CustomPriority=priority,TargetMaximumHealth=10000000m,
                Talents=new ForeverSurvivalHunterTalents {
                    Ranks=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase)
                }
            };
            var value=new SurvivalHunterSimRunner().Run(r);
            return (value.DamageDone,value.Run.Timeline);
        }
        var b=new BeastMasteryHunterSimRequest{
            DurationSeconds=90m,Seed=seed,PetEnabled=true,
            PetFamily="cat",UseRapidFire=false,UseAimedShot=false,
            UseMultiShot=false,UseSerpentSting=false,UseSummonHawk=false,
            CaptureTimeline=true,CustomPriority=priority,
            TargetMaximumHealth=10000000m,
            Talents=new ForeverBeastMasteryHunterTalents {
                Ranks=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase)
            }
        };
        var result=new BeastMasteryHunterSimRunner().Run(b);
        return (result.DamageDone,result.Run.Timeline);
    }

    [Theory]
    [InlineData("survival","cp175-f3d4b-survival-sniper-scatter.json")]
    [InlineData("beast-mastery","cp175-f3d4b-beast-mastery-sniper-scatter.json")]
    public void UnlearnedOffTreeShotsCannotExecuteThroughStalePriority(
        string spec,string filename)
    {
        var (ranks,_,_,_)=Fixture(filename);
        ranks.Remove("scatter-shot");
        Assert.Throws<ArgumentException>(()=>Run(spec,ranks,777134));
        ranks["scatter-shot"]=1;
        ranks.Remove("sniper-shot");
        Assert.Throws<ArgumentException>(()=>Run(spec,ranks,777134));
    }
}
