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
    public void RemovingOffTreeFocusedFireRemovesItsRealPetDamageMultiplier()
    {
        var (ranks,mm,survival,bm)=Read("cp175-f3d4-marksmanship-beast-mastery.json");
        // The legal 44 MM / 7 BM fixture selects Focused Fire with
        // five earlier-row Endurance ranks. Removing it must revoke damage.
        MarksmanshipHunterSimRequest Build(int focusedRank)
        {
            var selection=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase);
            selection["focused-fire"]=focusedRank;
            return new MarksmanshipHunterSimRequest {
                DurationSeconds=45m,Seed=1757410,HasPet=true,
                PetFamily="cat",CaptureTimeline=true,UseRapidFire=false,
                TargetMaximumHealth=10000000m,
                Talents=new ForeverMarksmanshipHunterTalents {
                    Ranks=selection,MarksmanshipPointsSpent=mm,
                    SurvivalPointsSpent=survival,BeastMasteryPointsSpent=bm
                }
            };
        }
        var selected=Build(2);
        var with=new MarksmanshipHunterSimRunner().Run(selected);
        var without=new MarksmanshipHunterSimRunner().Run(Build(0));
        Assert.True(with.PetDamage>0m);
        Assert.True(without.PetDamage>0m);
        Assert.InRange(Math.Abs(without.PetDamage*1.02m-with.PetDamage),0m,.000001m);
        Assert.Equal(2,selected.Talents.Ranks["focused-fire"]);
    }

    [Fact]
    public void SurvivalStartingThreeTreeBuildExecutesLearnedMeleeAndMarksmanshipCritWithPet()
    {
        var (ranks,mm,survival,bm)=Read("cp175-f3d4-survival-three-trees.json");
        Assert.Equal(51,ranks.Values.Sum());
        Assert.Equal((5,39,7),(mm,survival,bm));
        var payload=JsonSerializer.SerializeToElement(new {
            ranks,marksmanshipPointsSpent=mm,survivalPointsSpent=survival,
            beastMasteryPointsSpent=bm
        },new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var api=new ForeverSoloTalentController();
        Assert.IsType<OkObjectResult>(api.ValidateItemized("hunter","survival",payload));
        Assert.IsType<OkObjectResult>(api.Validate("hunter","survival",payload));
        SurvivalHunterSimRequest Build(int lethal)
        {
            var selected=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase);
            selected["lethal-attacks"]=lethal;
            return new SurvivalHunterSimRequest {
                DurationSeconds=65m,Seed=1757411,HasPet=true,
                CombatMode="melee",PetFamily="raptor",CaptureTimeline=true,
                TargetMaximumHealth=10000000m,
                UseRapidFire=false,UseFireTraps=false,UseFrostTraps=false,
                ScriptedAvoidanceEverySeconds=null,
                Talents=new ForeverSurvivalHunterTalents {Ranks=selected}
            };
        }
        var learned=Build(5);
        var first=new SurvivalHunterSimRunner().Run(learned);
        var replay=new SurvivalHunterSimRunner().Run(learned);
        var removed=new SurvivalHunterSimRunner().Run(Build(0));
        Assert.True(first.HunterDamage>0m);
        Assert.True(first.MeleeDamage>0m);
        Assert.True(first.PetDamage>0m);
        Assert.Equal(5m,first.Run.CustomMetrics["survival-offtree-lethal-attacks-crit-percent"]);
        Assert.Equal(0m,removed.Run.CustomMetrics["survival-offtree-lethal-attacks-crit-percent"]);
        Assert.Contains(first.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="raptor-strike" && e.Amount>0m);
        Assert.Contains(first.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.SourceActorKey=="hunter-pet" && e.ResourceKey=="focus" && e.Amount<0m);
        Assert.Equal(first.DamageDone,replay.DamageDone);
        Assert.Equal(first.PetDamage,replay.PetDamage);
        Assert.Equal(5,learned.Talents.Ranks["lethal-attacks"]);
    }

    [Fact]
    public void BeastMasteryStartingThreeTreeBuildTracksTargetsAndPreservesPetDamage()
    {
        var (ranks,mm,survival,bm)=Read("cp175-f3d4-beast-mastery-three-trees.json");
        Assert.Equal(51,ranks.Values.Sum());
        Assert.Equal((5,5,41),(mm,survival,bm));
        var selection=JsonSerializer.SerializeToElement(new {
            ranks,marksmanshipPointsSpent=mm,survivalPointsSpent=survival,
            beastMasteryPointsSpent=bm
        },new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var api=new ForeverSoloTalentController();
        Assert.IsType<OkObjectResult>(api.ValidateItemized("hunter","beast-mastery",selection));
        Assert.IsType<OkObjectResult>(api.Validate("hunter","beast-mastery",selection));
        BeastMasteryHunterSimRequest Build(int tracking)
        {
            var selected=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase);
            selected["improved-tracking"]=tracking;
            return new BeastMasteryHunterSimRequest {
                DurationSeconds=56m,Seed=1757412,PetEnabled=true,
                PetFamily="cat",UseRapidFire=false,UseSummonHawk=false,
                TargetMaximumHealth=10000000m,CaptureTimeline=true,
                TargetIsTracked=true,
                Talents=new ForeverBeastMasteryHunterTalents {Ranks=selected}
            };
        }
        var input=Build(5);
        var tracked=new BeastMasteryHunterSimRunner().Run(input);
        var replay=new BeastMasteryHunterSimRunner().Run(input);
        var untracked=new BeastMasteryHunterSimRunner().Run(Build(0));
        Assert.True(tracked.HunterDamage>0m);
        Assert.True(tracked.PetDamage>0m);
        Assert.InRange(Math.Abs(untracked.HunterDamage*1.05m-tracked.HunterDamage),0m,.000001m);
        Assert.Equal(untracked.PetDamage,tracked.PetDamage);
        Assert.Equal(tracked.DamageDone,replay.DamageDone);
        Assert.Equal(5,input.Talents.Ranks["improved-tracking"]);
        Assert.Contains(tracked.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.Amount>0m);
    }

    [Fact]
    public void SurvivalFocusedFireDoesNotBoostOwnerWhilePetIsAliveButNeverEngaged()
    {
        SurvivalHunterSimRequest Build(int rank) => new() {
            DurationSeconds=35m,Seed=1757413,HasPet=true,
            PetEngagementDelaySeconds=60m,CombatMode="ranged",
            TargetMaximumHealth=10000000m,CaptureTimeline=true,
            UseRapidFire=false,UseFireTraps=false,UseFrostTraps=false,
            ScriptedAvoidanceEverySeconds=null,
            PetTalents=new ForeverBeastMasteryHunterTalents {
                Ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){
                    ["endurance-training"]=5,["focused-fire"]=rank
                }
            }
        };
        var ranked=new SurvivalHunterSimRunner().Run(Build(2));
        var unranked=new SurvivalHunterSimRunner().Run(Build(0));
        Assert.True(ranked.HunterDamage>0m);
        Assert.Equal(unranked.HunterDamage,ranked.HunterDamage);
        Assert.Equal(0m,ranked.PetDamage);
        Assert.DoesNotContain(ranked.Run.Timeline,e=>
            e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.Amount>0m);
    }

    [Fact]
    public void OffTreeDeadlyAspectsUsesRealHawkAutoHitsAndExpiresInsteadOfPermanentHaste()
    {
        var (ranks,mm,survival,bm)=Read("cp175-f3d4-marksmanship-beast-mastery.json");
        // Spend five BM row-one ranks on Deadly Aspects instead of the
        // rank-two Focused Fire to exercise the real cross-tree proc.
        ranks["focused-fire"]=0;
        ranks["deadly-aspects"]=5;
        ranks["improved-arcane-shot"]=2;
        mm-=3;bm+=3;
        MarksmanshipHunterSimRequest Build(int rank)
        {
            var chosen=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase);
            chosen["deadly-aspects"]=rank;
            return new MarksmanshipHunterSimRequest {
                DurationSeconds=150m,Seed=1757414,HasPet=true,
                PetFamily="bear",UseAspectOfTheHawk=true,
                CaptureTimeline=true,TargetMaximumHealth=10000000m,
                UseRapidFire=false,UseAimedShot=false,UseSerpentSting=false,
                UseMultiShot=false,UseSniperShot=false,
                CustomPriority=["aspect-of-the-hawk","arcane-shot"],
                Tuning=new ForeverMarksmanshipHunterTuning{HitChancePercent=100m},
                Talents=new ForeverMarksmanshipHunterTalents {
                    Ranks=chosen,MarksmanshipPointsSpent=mm,
                    SurvivalPointsSpent=survival,BeastMasteryPointsSpent=bm
                }
            };
        }
        var selected=new MarksmanshipHunterSimRunner().Run(Build(5));
        var removed=new MarksmanshipHunterSimRunner().Run(Build(0));
        Assert.True(selected.Run.CustomMetrics["hunter-deadly-aspects-procs"]>0m);
        Assert.Equal(0m,removed.Run.CustomMetrics["hunter-deadly-aspects-procs"]);
        var applied=selected.Run.Timeline.First(e=>
            e.Type==CombatEventType.AuraApplied && e.AbilityKey=="deadly-aspects");
        Assert.Contains(selected.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="deadly-aspects" && e.TimeSeconds>=applied.TimeSeconds+12m);
        Assert.DoesNotContain(removed.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied && e.AbilityKey=="deadly-aspects");
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
