using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169HunterOffTreePetTalents
{
    private static ForeverBeastMasteryHunterTalents OffTree(int focusedFire)=>
        new() {
            Ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){
                ["endurance-training"]=5,
                ["focused-fire"]=focusedFire
            }
        };

    [Theory]
    [InlineData("marksmanship")]
    [InlineData("survival")]
    public void SourceBackedFocusedFireBoostsBothOwnerAndPetOnlyWhilePetIsActive(
        string spec)
    {
        if(spec=="marksmanship")
        {
            MarksmanshipHunterSimRequest Req(int rank)
            {
                var req=new MarksmanshipHunterSimRequest{
                    HasPet=true,PetFamily="bear",DurationSeconds=26m,
                    UseRapidFire=false,TargetMaximumHealth=10000000m,
                    CaptureTimeline=true,PetTalents=OffTree(rank)
                };
                req.Tuning.HitChancePercent=100m;
                req.Tuning.CriticalChancePercent=0m;
                req.PetTuning.PetHitPercent=100m;
                req.PetTuning.PetAvoidancePercent=0m;
                return req;
            }
            var zero=new MarksmanshipHunterSimRunner().Run(Req(0));
            var two=new MarksmanshipHunterSimRunner().Run(Req(2));
            Assert.InRange(Math.Abs(zero.HunterDamage*1.02m-two.HunterDamage),0m,.0001m);
            Assert.InRange(Math.Abs(zero.PetDamage*1.02m-two.PetDamage),0m,.0001m);
        }
        else
        {
            SurvivalHunterSimRequest Req(int rank)
            {
                var req=new SurvivalHunterSimRequest{
                    HasPet=true,PetFamily="bear",CombatMode="melee",
                    AdditionalTargets=0,DurationSeconds=26m,UseRapidFire=false,
                    UseFireTraps=false,UseFrostTraps=false,
                    ScriptedAvoidanceEverySeconds=null,TargetMaximumHealth=10000000m,
                    CaptureTimeline=true,PetTalents=OffTree(rank)
                };
                req.Tuning.HitChancePercent=100m;
                req.Tuning.CriticalChancePercent=0m;
                req.Tuning.TargetDodgePercent=0m;
                req.Tuning.TargetParryPercent=0m;
                req.PetTuning.PetHitPercent=100m;
                req.PetTuning.PetAvoidancePercent=0m;
                return req;
            }
            var zero=new SurvivalHunterSimRunner().Run(Req(0));
            var two=new SurvivalHunterSimRunner().Run(Req(2));
            Assert.InRange(Math.Abs(zero.HunterDamage*1.02m-two.HunterDamage),0m,.0001m);
            Assert.InRange(Math.Abs(zero.PetDamage*1.02m-two.PetDamage),0m,.0001m);
        }
    }

    [Fact]
    public void EnduranceTrainingAndImprovedRevivePetUseSharedActorAndCostRules()
    {
        var build=OffTree(0);
        build.Ranks["improved-revive-pet"]=2;
        build.Validate();
        var ctx=new SimulationContext(new SimulationRunOptions{DurationSeconds=30m});
        var hunter=new SimulationActorState{
            Key="hunter",TeamKey="raid",Level=60,MaximumHealth=3900m
        };
        hunter.InitializeHealth(hunter.MaximumHealth);
        ctx.AddActor(hunter);
        var tune=new ForeverBeastMasteryHunterTuning();
        var family=ForeverHunterPetFamilies.Get("bear");
        var normal=HunterCompanionComposition.Create(ctx,hunter,family,tune,0);
        // Re-register in a new context to compare otherwise identical actors.
        var otherCtx=new SimulationContext(new SimulationRunOptions{DurationSeconds=30m});
        var otherHunter=new SimulationActorState{
            Key="hunter",TeamKey="raid",Level=60,MaximumHealth=3900m
        };
        otherHunter.InitializeHealth(otherHunter.MaximumHealth);
        otherCtx.AddActor(otherHunter);
        var trained=HunterCompanionComposition.Create(otherCtx,otherHunter,family,tune,5);
        Assert.Equal(normal.MaximumHealth*1.15m,trained.MaximumHealth);
        Assert.Equal(normal.Stats.Get("armor")*1.15m,trained.Stats.Get("armor"));
        var revive=ForeverBeastMasteryHunterSpellbook.Hunter(build,tune)
            .Single(x=>x.Key=="revive-pet");
        Assert.Equal(4m,revive.CastTimeSeconds);
        Assert.Equal(300m,revive.ResourceCosts.Single().Amount);
    }

    [Fact]
    public void OverspendingOffTreeRanksIsRejectedInsteadOfCreatingFreeBuffs()
    {
        var mm=new MarksmanshipHunterSimRequest{HasPet=true,DurationSeconds=8m};
        mm.PetTalents=OffTree(2);
        mm.PetTalents.Ranks["improved-revive-pet"]=2;
        mm.PetTalents.Ranks["pathfinding"]=2; // 11 > 10 MM off-tree points
        Assert.Throws<ArgumentException>(()=>new MarksmanshipHunterSimRunner().Run(mm));
        var survival=new SurvivalHunterSimRequest{HasPet=true,DurationSeconds=8m};
        survival.Talents.Ranks["improved-tracking"]=5;
        survival.PetTalents=OffTree(2);
        survival.PetTalents.Ranks["pathfinding"]=2;
        // This is valid unless remaining Survival talent allocations exceed 42.
        new SurvivalHunterSimRunner().Run(survival);
    }
}
