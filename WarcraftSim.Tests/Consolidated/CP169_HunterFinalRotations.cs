using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169HunterFinalRotationIntegration
{
    [Fact]
    public void BeastMasteryCustomOwnerAndPetPrioritiesBothExecuteWithoutDoubleCounting()
    {
        var req=new BeastMasteryHunterSimRequest {
            PetFamily="raptor",DurationSeconds=35m,CaptureTimeline=true,
            TargetMaximumHealth=10000000m,
            CustomPriority=["serpent-sting","arcane-shot","aimed-shot"],
            PetAbilityPriority=["pet-savage-rend","pet-claw","pet-bite"],
            UseRapidFire=false,UseSummonHawk=false
        };
        var run=new BeastMasteryHunterSimRunner().Run(req);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="arcane-shot" &&
            e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.SourceActorKey=="hunter-pet" && e.AbilityKey=="pet-savage-rend");
        Assert.True(run.HunterDamage>0m);
        Assert.True(run.PetDamage>0m);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
        var round=JsonSerializer.Deserialize<BeastMasteryHunterSimRequest>(
            JsonSerializer.Serialize(req));
        Assert.NotNull(round);
        Assert.Equal(req.CustomPriority,round.CustomPriority);
        Assert.Equal(req.PetAbilityPriority,round.PetAbilityPriority);
    }

    [Fact]
    public void InvalidOwnerActionAndRepeatsAreRejectedButDefaultStillWorks()
    {
        var req=new BeastMasteryHunterSimRequest {
            DurationSeconds=15m,UseRapidFire=false
        };
        var ordinary=new BeastMasteryHunterSimRunner().Run(req);
        Assert.True(ordinary.DamageDone>0m);
        req.CustomPriority=["unknown-shot"];
        Assert.Throws<ArgumentException>(()=>new BeastMasteryHunterSimRunner().Run(req));
        req.CustomPriority=["aimed-shot","aimed-shot"];
        Assert.Throws<ArgumentException>(()=>new BeastMasteryHunterSimRunner().Run(req));
    }
}
