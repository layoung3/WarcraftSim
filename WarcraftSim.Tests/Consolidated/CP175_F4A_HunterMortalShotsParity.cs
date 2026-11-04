using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F4AHunterMortalShotsParity
{
    private static Dictionary<string,int> Ranks(string fixture)
    {
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null)
        {
            var file=Path.Combine(dir.FullName,"docs","acceptance","cp175",fixture);
            if(File.Exists(file))
            {
                using var data=JsonDocument.Parse(File.ReadAllText(file));
                return data.RootElement.GetProperty("talents")
                    .GetProperty("ranks").Deserialize<Dictionary<string,int>>()!;
            }
            dir=dir.Parent;
        }
        throw new InvalidOperationException("Missing certified F3D4B fixture.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OffTreeMortalShotsIncreasesRealOwnerRangedCriticalDamageButNotPetDamage(bool bm)
    {
        var ranks=Ranks(bm?"cp175-f3d4b-beast-mastery-sniper-scatter.json":
            "cp175-f3d4b-survival-sniper-scatter.json");
        Assert.Equal(5,ranks["mortal-shots"]);
        var full=Run(bm,ranks);
        ranks["mortal-shots"]=0;
        var removed=Run(bm,ranks);
        Assert.NotEmpty(full.OwnerCrits);
        Assert.Equal(full.OwnerCrits.Length,removed.OwnerCrits.Length);
        Assert.All(full.OwnerCrits.Zip(removed.OwnerCrits),pair=>
            Assert.InRange(Math.Abs(pair.First*2m/2.30m-pair.Second),0m,.000001m));
        Assert.Equal(removed.PetDamage,full.PetDamage);
        var replay=Run(bm,ranks);
        Assert.Equal(removed.OwnerCrits,replay.OwnerCrits);
    }

    private static (decimal[] OwnerCrits,decimal PetDamage) Run(
        bool bm,Dictionary<string,int> ranks)
    {
        if(bm)
        {
            var request=new BeastMasteryHunterSimRequest {
                DurationSeconds=23m,Seed=1754401,PetEnabled=false,
                CaptureTimeline=true,TargetMaximumHealth=10000000m,
                UseRapidFire=false,UseAimedShot=false,UseMultiShot=false,
                UseSerpentSting=false,UseMendPet=false,UseSummonHawk=false,
                CustomPriority=["sniper-shot"],
                Tuning=new ForeverBeastMasteryHunterTuning {
                    HunterCriticalPercent=100m,HunterHitPercent=100m,TargetArmor=0m
                },
                Talents=new ForeverBeastMasteryHunterTalents {
                    Ranks=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase)
                }
            };
            var output=new BeastMasteryHunterSimRunner().Run(request);
            return (Crits(output.Run.Timeline),output.PetDamage);
        }
        else
        {
            var request=new SurvivalHunterSimRequest {
                DurationSeconds=23m,Seed=1754401,HasPet=false,
                CombatMode="ranged",AdditionalTargets=0,CaptureTimeline=true,
                TargetMaximumHealth=10000000m,UseRapidFire=false,
                UseAimedShot=false,UseFireTraps=false,UseFrostTraps=false,
                CustomPriority=["sniper-shot"],
                Tuning=new ForeverSurvivalHunterTuning {
                    CriticalChancePercent=100m,HitChancePercent=100m,
                    TargetArmor=0m
                },
                Talents=new ForeverSurvivalHunterTalents {
                    Ranks=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase)
                }
            };
            var output=new SurvivalHunterSimRunner().Run(request);
            return (Crits(output.Run.Timeline),output.PetDamage);
        }
    }

    private static decimal[] Crits(IEnumerable<CombatEvent> events)=>
        events.Where(e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="sniper-shot" &&
            e.IsCritical && e.Amount is >0m)
            .Select(e=>e.Amount!.Value).ToArray();
}
