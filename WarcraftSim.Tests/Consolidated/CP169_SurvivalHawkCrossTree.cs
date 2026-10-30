using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169SurvivalOffTreeHawks
{
    private static ForeverBeastMasteryHunterTalents HawkTalents()=>
        new() {
            Ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase) {
                ["endurance-training"]=5,
                ["focused-fire"]=2,
                ["pathfinding"]=2,
                ["improved-revive-pet"]=2,
                ["unleashed-fury"]=5,
                ["summon-hawk"]=1
            }
        };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SurvivalSummonsTwoIndependentHawksAlongsideOptionalPermanentPet(bool pet)
    {
        var talents=HawkTalents();
        talents.Validate();
        var req=new SurvivalHunterSimRequest {
            HasPet=pet,PetFamily="cat",CombatMode="melee",
            PetTalents=talents,AdditionalTargets=0,
            DurationSeconds=33m,TargetMaximumHealth=10000000m,
            UseFireTraps=false,UseFrostTraps=false,
            UseRapidFire=false,ScriptedAvoidanceEverySeconds=null,
            CaptureTimeline=true
        };
        req.Tuning.HitChancePercent=100m;
        req.Tuning.TargetDodgePercent=0m;
        req.Tuning.TargetParryPercent=0m;
        req.PetTuning.PetHitPercent=100m;
        var run=new SurvivalHunterSimRunner().Run(req);
        Assert.True(run.HunterDamage>0m);
        Assert.True(run.HawkDamage>0m);
        Assert.True(run.HawkSummons>=2);
        Assert.Equal(pet,run.PetDamage>0m);
        Assert.True(run.HawkThreat>0m);
        Assert.InRange(Math.Abs(run.DamageDone-run.HunterDamage-
            run.PetDamage-run.HawkDamage),0m,.000001m);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),
            0m,.000001m);
        foreach(var name in new[]{"hawk-1","hawk-2"})
            Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
                e.SourceActorKey==name && e.Amount>0m &&
                e.TargetActorKey=="target");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="melee-auto");
        var json=JsonSerializer.Serialize(run);
        Assert.Contains("HawkDamage",json);
    }

    [Fact]
    public void HawkSummonSharedCooldownDoesNotGrantFreeArcaneShots()
    {
        var req=new SurvivalHunterSimRequest {
            HasPet=false,CombatMode="ranged",
            PetTalents=HawkTalents(),AdditionalTargets=0,
            DurationSeconds=20m,TargetMaximumHealth=10000000m,
            UseFireTraps=false,UseFrostTraps=false,UseRapidFire=false,
            ScriptedAvoidanceEverySeconds=null,CaptureTimeline=true
        };
        req.Tuning.HitChancePercent=100m;
        var run=new SurvivalHunterSimRunner().Run(req);
        var starts=run.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityCastStarted &&
            e.SourceActorKey=="hunter" &&
            e.AbilityKey is ("summon-hawk" or "arcane-shot"))
            .OrderBy(e=>e.TimeSeconds).ToArray();
        Assert.Contains(starts,e=>e.AbilityKey=="summon-hawk");
        for(var i=1;i<starts.Length;i++)
            Assert.True(starts[i].TimeSeconds-starts[i-1].TimeSeconds>=6m);
    }
}
