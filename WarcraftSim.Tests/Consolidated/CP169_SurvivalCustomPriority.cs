using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169SurvivalCustomOwnerPriority
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MeleeFirstCustomPriorityKeepsOwnerAndOptionalPetIndependent(bool pet)
    {
        var req=new SurvivalHunterSimRequest {
            HasPet=pet,PetFamily="raptor",CombatMode="melee",
            AdditionalTargets=0,DurationSeconds=32m,
            UseFireTraps=false,UseFrostTraps=false,
            ScriptedAvoidanceEverySeconds=null,UseRapidFire=false,
            TargetMaximumHealth=10000000m,CaptureTimeline=true,
            CustomPriority=["lacerate","raptor-strike","mongoose-bite"]
        };
        req.Tuning.HitChancePercent=100m;
        req.Tuning.TargetDodgePercent=0m;
        req.Tuning.TargetParryPercent=0m;
        req.PetTuning.PetHitPercent=100m;
        var run=new SurvivalHunterSimRunner().Run(req);
        Assert.True(run.MeleeDamage>0m);
        Assert.Contains(run.Run.Timeline,e=>e.AbilityKey=="lacerate" &&
            e.SourceActorKey=="hunter" && e.Type==CombatEventType.AbilityCastCompleted);
        Assert.Contains(run.Run.Timeline,e=>e.AbilityKey=="raptor-strike" &&
            e.SourceActorKey=="hunter" && e.Type==CombatEventType.Damage &&
            e.Amount>0m);
        Assert.Equal(pet,run.PetDamage>0m);
        Assert.InRange(Math.Abs(run.DamageDone-run.HunterDamage-run.PetDamage),0m,.000001m);
        var copy=JsonSerializer.Deserialize<SurvivalHunterSimRequest>(JsonSerializer.Serialize(req));
        Assert.NotNull(copy);
        Assert.Equal(req.CustomPriority,copy.CustomPriority);
    }

    [Fact]
    public void InvalidSurvivalAbilityAndDuplicateCustomPrioritiesAreRejected()
    {
        var req=new SurvivalHunterSimRequest {
            AdditionalTargets=0,CombatMode="melee",DurationSeconds=8m
        };
        req.CustomPriority=["not-a-hunter-ability"];
        Assert.Throws<ArgumentException>(()=>new SurvivalHunterSimRunner().Run(req));
        req.CustomPriority=["lacerate","lacerate"];
        Assert.Throws<ArgumentException>(()=>new SurvivalHunterSimRunner().Run(req));
    }
}
