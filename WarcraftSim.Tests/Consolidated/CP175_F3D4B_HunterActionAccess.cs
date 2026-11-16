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

    [Fact]
    public void MarksmanshipOffTreeSummonHawkCreatesTwoIndependentActorsAndConsumesMana()
    {
        var (ranks,mm,sv,bm)=Fixture("cp175-f3d4b-marksmanship-summon-hawk.json");
        Assert.Equal((35,0,16),(mm,sv,bm));
        Assert.Equal(51,ranks.Values.Sum());
        var choice=JsonSerializer.SerializeToElement(new{
            ranks,marksmanshipPointsSpent=mm,survivalPointsSpent=sv,
            beastMasteryPointsSpent=bm
        },new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var api=new ForeverSoloTalentController();
        Assert.IsType<OkObjectResult>(api.Validate("hunter","marksmanship",choice));
        Assert.IsType<OkObjectResult>(api.ValidateItemized("hunter","marksmanship",choice));
        MarksmanshipHunterSimRequest Build(bool learned) {
            var selected=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase);
            if(!learned)selected["summon-hawk"]=0;
            return new MarksmanshipHunterSimRequest {
                DurationSeconds=65m,Seed=1757503,HasPet=true,PetFamily="wolf",
                CaptureTimeline=true,TargetMaximumHealth=10000000m,
                UseRapidFire=false,
                CustomPriority=["summon-hawk","arcane-shot"],
                Talents=new ForeverMarksmanshipHunterTalents {
                    Ranks=selected,MarksmanshipPointsSpent=mm,
                    BeastMasteryPointsSpent=bm,SurvivalPointsSpent=sv
                }
            };
        }
        var original=Build(true);
        var result=new MarksmanshipHunterSimRunner().Run(original);
        var replay=new MarksmanshipHunterSimRunner().Run(original);
        Assert.True(result.HawkDamage>0m);
        Assert.True(result.PetDamage>0m);
        Assert.Equal(result.DamageDone,
            result.HunterDamage+result.PetDamage+result.HawkDamage);
        Assert.Contains(result.Run.Timeline,e=>
            e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="summon-hawk" && e.ResourceKey=="mana" && e.Amount<0m);
        Assert.Contains(result.Run.Timeline,e=>
            e.Type==CombatEventType.Damage &&
            e.SourceActorKey is "hawk-1" or "hawk-2" && e.Amount>0m);
        Assert.True(result.Run.CustomMetrics["hunter-hawk-summons"]>0m);
        Assert.Equal(result.HawkDamage,replay.HawkDamage);
        Assert.Equal(result.DamageDone,replay.DamageDone);
        Assert.Throws<ArgumentException>(()=>new MarksmanshipHunterSimRunner().Run(Build(false)));
    }

    [Fact]
    public void MarksmanshipSurvivalMeleeLearnsStriderAndRequiresRealCounterattackOpportunity()
    {
        var (ranks,mm,sv,bm)=Fixture("cp175-f3d4b-marksmanship-survival-melee.json");
        Assert.Equal((29,22,0),(mm,sv,bm));
        Assert.Equal(51,ranks.Values.Sum());
        var api=new ForeverSoloTalentController();
        var selection=JsonSerializer.SerializeToElement(new {
            ranks,marksmanshipPointsSpent=mm,survivalPointsSpent=sv,beastMasteryPointsSpent=bm
        },new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsType<OkObjectResult>(api.ValidateItemized("hunter","marksmanship",selection));
        Assert.IsType<OkObjectResult>(api.Validate("hunter","marksmanship",selection));
        MarksmanshipHunterSimRequest Build(bool melee,decimal? parry)
        {
            return new MarksmanshipHunterSimRequest{
                DurationSeconds=65m,Seed=1757504,UseMeleeAuto=melee,
                ScriptedParryEverySeconds=parry,HasPet=false,
                CaptureTimeline=true,TargetMaximumHealth=10000000m,
                UseRapidFire=false,UseAimedShot=false,UseSerpentSting=false,
                UseArcaneShot=false,UseSniperShot=false,UseMultiShot=false,
                CustomPriority=["counterattack","strider-kick"],
                Talents=new ForeverMarksmanshipHunterTalents {
                    Ranks=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase),
                    MarksmanshipPointsSpent=mm,SurvivalPointsSpent=sv,
                    BeastMasteryPointsSpent=bm
                }
            };
        }
        var result=new MarksmanshipHunterSimRunner().Run(Build(true,8m));
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="strider-kick");
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="strider-kick" && e.Amount>0m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="strider-kick" && e.TargetActorKey=="hunter");
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="strider-kick" && e.TargetActorKey=="hunter");
        Assert.Contains(result.Run.Timeline,e=>
            e.Type==CombatEventType.ReactiveOpportunityGranted &&
            e.AbilityKey=="counterattack");
        Assert.Contains(result.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="counterattack");
        var noParry=new MarksmanshipHunterSimRunner().Run(Build(true,null));
        Assert.DoesNotContain(noParry.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="counterattack");
        var noMelee=new MarksmanshipHunterSimRunner().Run(Build(false,8m));
        Assert.DoesNotContain(noMelee.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey is ("counterattack" or "strider-kick"));
        var replay=new MarksmanshipHunterSimRunner().Run(Build(true,8m));
        Assert.Equal(result.DamageDone,replay.DamageDone);
        Assert.Equal(result.Run.Timeline.Select(e=>
            (e.TimeSeconds,e.Type,e.AbilityKey,e.Amount)).ToArray(),
            replay.Run.Timeline.Select(e=>
            (e.TimeSeconds,e.Type,e.AbilityKey,e.Amount)).ToArray());
    }

    [Fact]
    public void BeastMasteryStartingPresetExecutesSurvivalCounterattackAndStriderWithActivePet()
    {
        var (ranks,mm,sv,bm)=Fixture("cp175-f3d4b-beast-mastery-survival-melee.json");
        Assert.Equal((0,22,29),(mm,sv,bm));
        Assert.Equal(51,ranks.Values.Sum());
        var selection=JsonSerializer.SerializeToElement(new {
            ranks,marksmanshipPointsSpent=mm,survivalPointsSpent=sv,
            beastMasteryPointsSpent=bm
        },new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var api=new ForeverSoloTalentController();
        Assert.IsType<OkObjectResult>(api.ValidateItemized("hunter","beast-mastery",selection));
        Assert.IsType<OkObjectResult>(api.Validate("hunter","beast-mastery",selection));
        BeastMasteryHunterSimRequest Build(decimal? parry)
            => new(){
                DurationSeconds=65m,Seed=1757505,PetEnabled=true,
                PetFamily="wolf",UseMeleeAuto=true,ScriptedParryEverySeconds=parry,
                CaptureTimeline=true,TargetMaximumHealth=10000000m,
                UseRapidFire=false,UseAimedShot=false,UseSerpentSting=false,
                UseMultiShot=false,UseSummonHawk=false,
                CustomPriority=["counterattack","strider-kick"],
                Talents=new ForeverBeastMasteryHunterTalents {
                    Ranks=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase)
                }
            };
        var result=new BeastMasteryHunterSimRunner().Run(Build(8m));
        var replay=new BeastMasteryHunterSimRunner().Run(Build(8m));
        var withoutParry=new BeastMasteryHunterSimRunner().Run(Build(null));
        Assert.True(result.PetDamage>0m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="strider-kick" && e.Amount>0m);
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="counterattack");
        Assert.DoesNotContain(withoutParry.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastCompleted && e.AbilityKey=="counterattack");
        Assert.Equal(result.DamageDone,replay.DamageDone);
        Assert.Equal(result.PetDamage,replay.PetDamage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrossPresetDeterrenceRequiresLearnedRankAndExecutesItsNativeDefensiveAura(bool beast)
    {
        var file=beast?"cp175-f3d4b-beast-mastery-survival-melee.json":
            "cp175-f3d4b-marksmanship-survival-melee.json";
        var (ranks,mm,sv,bm)=Fixture(file);
        var cloned=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase);
        if(beast)
        {
            BeastMasteryHunterSimRequest Build(Dictionary<string,int> chosen)=>new(){
                DurationSeconds=35m,Seed=1757510,PetEnabled=true,
                UseMeleeAuto=true,PetFamily="wolf",
                CaptureTimeline=true,TargetMaximumHealth=10000000m,
                UseRapidFire=false,UseAimedShot=false,UseMultiShot=false,
                UseSummonHawk=false,
                CustomPriority=["deterrence","strider-kick"],
                Talents=new ForeverBeastMasteryHunterTalents{Ranks=chosen}
            };
            var run=new BeastMasteryHunterSimRunner().Run(Build(cloned));
            Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
                e.AbilityKey=="deterrence" && e.TargetActorKey=="hunter");
            cloned.Remove("counterattack");
            cloned.Remove("deterrence");
            Assert.Throws<ArgumentException>(()=>new BeastMasteryHunterSimRunner().Run(Build(cloned)));
        }
        else
        {
            MarksmanshipHunterSimRequest Build(Dictionary<string,int> chosen)=>new(){
                DurationSeconds=35m,Seed=1757510,HasPet=false,
                UseMeleeAuto=true,CaptureTimeline=true,
                TargetMaximumHealth=10000000m,UseRapidFire=false,
                CustomPriority=["deterrence","strider-kick"],
                Talents=new ForeverMarksmanshipHunterTalents{
                    Ranks=chosen,MarksmanshipPointsSpent=mm,
                    SurvivalPointsSpent=sv,BeastMasteryPointsSpent=bm
                }
            };
            var run=new MarksmanshipHunterSimRunner().Run(Build(cloned));
            Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
                e.AbilityKey=="deterrence" && e.TargetActorKey=="hunter");
            cloned.Remove("counterattack");
            cloned.Remove("deterrence");
            Assert.Throws<ArgumentException>(()=>new MarksmanshipHunterSimRunner().Run(Build(cloned)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OffTreeTrueshotAuraUsesRealOwnerStatsRatherThanOnlySelectedRanks(bool beast)
    {
        var file=beast?"cp175-f3d4b-beast-mastery-sniper-scatter.json":
            "cp175-f3d4b-survival-sniper-scatter.json";
        var (ranks,_,_,_)=Fixture(file);
        Assert.Equal(1,ranks["trueshot-aura"]);
        if(beast)
        {
            BeastMasteryHunterSimRequest Build(int rank) {
                var selected=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase);
                selected["trueshot-aura"]=rank;
                // Sniper Shot requires learned Trueshot Aura; removing the
                // parent must also remove the dependent active talent.
                if(rank==0)selected["sniper-shot"]=0;
                return new BeastMasteryHunterSimRequest{
                    Seed=1757516,DurationSeconds=40m,PetEnabled=true,
                    PetFamily="cat",UseSummonHawk=false,UseRapidFire=false,
                    TargetMaximumHealth=10000000m,CaptureTimeline=true,
                    Talents=new ForeverBeastMasteryHunterTalents{Ranks=selected}
                };
            }
            var selected=new BeastMasteryHunterSimRunner().Run(Build(1));
            var removed=new BeastMasteryHunterSimRunner().Run(Build(0));
            Assert.Equal(1m,selected.Run.CustomMetrics["beast-offtree-trueshot-applications"]);
            Assert.Equal(0m,removed.Run.CustomMetrics["beast-offtree-trueshot-applications"]);
            Assert.Contains(selected.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
                e.AbilityKey=="trueshot-aura" && e.TargetActorKey=="hunter");
        }
        else
        {
            SurvivalHunterSimRequest Build(int rank) {
                var selected=new Dictionary<string,int>(ranks,StringComparer.OrdinalIgnoreCase);
                selected["trueshot-aura"]=rank;
                if(rank==0)selected["sniper-shot"]=0;
                return new SurvivalHunterSimRequest{
                    Seed=1757516,DurationSeconds=40m,CombatMode="ranged",
                    HasPet=true,PetFamily="cat",AdditionalTargets=0,
                    UseFireTraps=false,UseFrostTraps=false,
                    UseRapidFire=false,TargetMaximumHealth=10000000m,
                    CaptureTimeline=true,
                    Talents=new ForeverSurvivalHunterTalents{Ranks=selected}
                };
            }
            var selected=new SurvivalHunterSimRunner().Run(Build(1));
            var removed=new SurvivalHunterSimRunner().Run(Build(0));
            Assert.Equal(1m,selected.Run.CustomMetrics["survival-offtree-trueshot-applications"]);
            Assert.Equal(0m,removed.Run.CustomMetrics["survival-offtree-trueshot-applications"]);
            Assert.Contains(selected.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
                e.AbilityKey=="trueshot-aura" && e.TargetActorKey=="hunter");
        }
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
