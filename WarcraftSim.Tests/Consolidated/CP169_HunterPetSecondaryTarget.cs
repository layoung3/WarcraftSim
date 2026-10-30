using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169HunterPetIndependentTargeting
{
    [Theory]
    [InlineData("marksmanship")]
    [InlineData("survival")]
    public void PetCanAttackSecondaryEnemyWhileHunterKeepsPrimaryTarget(string spec)
    {
        SimulationRunResult run;
        decimal petDamage,petThreat,total,ownerDamage;
        if(spec=="marksmanship")
        {
            var request=new MarksmanshipHunterSimRequest{
                HasPet=true,PetFamily="wind-serpent",PetTargetKey="add-1",
                AdditionalTargets=2,DurationSeconds=25m,CaptureTimeline=true,
                UseRapidFire=false,TargetMaximumHealth=10000000m
            };
            request.Tuning.HitChancePercent=100m;
            request.PetTuning.PetHitPercent=100m;
            var result=new MarksmanshipHunterSimRunner().Run(request);
            run=result.Run;petDamage=result.PetDamage;petThreat=result.PetThreat;
            total=result.DamageDone;ownerDamage=result.HunterDamage;
            var copy=JsonSerializer.Deserialize<MarksmanshipHunterSimRequest>(
                JsonSerializer.Serialize(request));
            Assert.NotNull(copy);
            Assert.Equal("add-1",copy.PetTargetKey);
            request.PetTargetKey="add-9";
            Assert.Throws<ArgumentException>(()=>new MarksmanshipHunterSimRunner().Run(request));
        }
        else
        {
            var request=new SurvivalHunterSimRequest{
                HasPet=true,PetFamily="wind-serpent",PetTargetKey="add-1",
                CombatMode="melee",AdditionalTargets=2,DurationSeconds=25m,
                CaptureTimeline=true,UseRapidFire=false,UseFireTraps=false,
                UseFrostTraps=false,ScriptedAvoidanceEverySeconds=null,
                TargetMaximumHealth=10000000m
            };
            request.Tuning.HitChancePercent=100m;
            request.PetTuning.PetHitPercent=100m;
            var result=new SurvivalHunterSimRunner().Run(request);
            run=result.Run;petDamage=result.PetDamage;petThreat=result.PetThreat;
            total=result.DamageDone;ownerDamage=result.HunterDamage;
            var copy=JsonSerializer.Deserialize<SurvivalHunterSimRequest>(
                JsonSerializer.Serialize(request));
            Assert.NotNull(copy);
            Assert.Equal("add-1",copy.PetTargetKey);
            request.PetTargetKey="add-9";
            Assert.Throws<ArgumentException>(()=>new SurvivalHunterSimRunner().Run(request));
        }
        Assert.True(petDamage>0m);
        Assert.True(petThreat>0m);
        Assert.Equal(total,petDamage+ownerDamage);
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter" && e.TargetActorKey=="target" &&
            e.Amount>0m);
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.TargetActorKey=="add-1" &&
            e.Amount>0m);
        Assert.DoesNotContain(run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.TargetActorKey=="target" &&
            e.Amount>0m);
        Assert.Contains(run.Timeline,e=>e.Type==CombatEventType.ThreatChanged &&
            e.SourceActorKey=="hunter-pet" && e.TargetActorKey=="add-1" &&
            e.Amount>0m);
    }
}
