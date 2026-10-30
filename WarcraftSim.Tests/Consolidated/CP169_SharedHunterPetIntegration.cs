using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169CrossSpecSharedPetIntegration
{
    private static MarksmanshipHunterSimRequest Mm(bool pet,string family="raptor")
    {
        var req=new MarksmanshipHunterSimRequest{
            HasPet=pet,PetFamily=family,DurationSeconds=42m,
            TargetMaximumHealth=10000000m,CaptureTimeline=true,
            UseRapidFire=false
        };
        req.Tuning.HitChancePercent=100m;
        req.PetTuning.PetHitPercent=100m;
        req.PetTuning.PetAvoidancePercent=0m;
        req.PetTuning.PetFocusPerSecond=10m;
        return req;
    }
    private static SurvivalHunterSimRequest Survival(bool pet,string family="wind-serpent")
    {
        var req=new SurvivalHunterSimRequest {
            HasPet=pet,PetFamily=family,CombatMode="melee",
            AdditionalTargets=0,DurationSeconds=42m,
            TargetMaximumHealth=10000000m,CaptureTimeline=true,
            UseRapidFire=false,UseFireTraps=false,UseFrostTraps=false,
            ScriptedAvoidanceEverySeconds=null,UseVolley=false
        };
        req.Tuning.HitChancePercent=100m;
        req.Tuning.TargetDodgePercent=0m;
        req.Tuning.TargetParryPercent=0m;
        req.PetTuning.PetHitPercent=100m;
        req.PetTuning.PetAvoidancePercent=0m;
        req.PetTuning.PetFocusPerSecond=10m;
        return req;
    }

    [Theory]
    [InlineData("raptor","pet-savage-rend")]
    [InlineData("wind-serpent","pet-lightning-breath")]
    [InlineData("bat","pet-sonic-blast")]
    public void MarksmanshipOwnerAndSelectedPetExecuteIndependentlyInOneSimulation(
        string family,string ability)
    {
        var req=Mm(true,family);
        var run=new MarksmanshipHunterSimRunner().Run(req);
        Assert.True(run.HunterDamage>0m);
        Assert.True(run.PetDamage>0m);
        Assert.Equal(run.HunterDamage+run.PetDamage,run.DamageDone);
        Assert.True(run.PetThreat>0m);
        Assert.InRange(run.PetFocus,0m,100m);
        Assert.True(run.PetUptimeSeconds>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="auto-shot" && e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.TargetActorKey=="target" &&
            e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.SourceActorKey=="hunter-pet" && e.AbilityKey==ability);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
        Assert.Equal(0m,run.Run.CustomMetrics["hunter-no-pet-lone-wolf"]);
        Assert.Contains("PetDamage",JsonSerializer.Serialize(run));
    }

    [Fact]
    public void NoPetMarksmanshipRetainsLoneWolfAndNeverCreatesPhantomCompanion()
    {
        var petless=new MarksmanshipHunterSimRunner().Run(Mm(false));
        var pet=new MarksmanshipHunterSimRunner().Run(Mm(true));
        Assert.Equal(0m,petless.PetDamage);
        Assert.Equal(0m,petless.PetThreat);
        Assert.False(petless.Run.Summary.ActorSummaries.ContainsKey("hunter-pet"));
        Assert.True(pet.Run.Summary.ActorSummaries.ContainsKey("hunter-pet"));
        Assert.Equal(1m,petless.Run.CustomMetrics["hunter-no-pet-lone-wolf"]);
        Assert.Equal(0m,pet.Run.CustomMetrics["hunter-no-pet-lone-wolf"]);
    }

    [Fact]
    public void SurvivalMeleeHunterAndPetShareTimeWithoutRequiringRangedMode()
    {
        var req=Survival(true);
        var run=new SurvivalHunterSimRunner().Run(req);
        Assert.True(run.MeleeDamage>0m);
        Assert.True(run.PetDamage>0m);
        Assert.Equal(run.HunterDamage+run.PetDamage,run.DamageDone);
        Assert.True(run.PetThreat>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter" &&
            e.AbilityKey is ("melee-auto" or "raptor-strike") &&
            e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.AbilityKey=="pet-melee" && e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.SourceActorKey=="hunter-pet" && e.AbilityKey=="pet-lightning-breath");
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void PetlessSurvivalPreservesOwnerOnlyMeleeCombatAndRequestSerialization()
    {
        var req=Survival(false);
        var run=new SurvivalHunterSimRunner().Run(req);
        Assert.True(run.MeleeDamage>0m);
        Assert.Equal(0m,run.PetDamage);
        Assert.Equal(run.HunterDamage,run.DamageDone);
        Assert.False(run.Run.Summary.ActorSummaries.ContainsKey("hunter-pet"));
        var withPet=Survival(true,"fox");
        var copy=JsonSerializer.Deserialize<SurvivalHunterSimRequest>(JsonSerializer.Serialize(withPet));
        Assert.NotNull(copy);
        Assert.True(copy.HasPet);
        Assert.Equal("fox",copy.PetFamily);
    }

    [Theory]
    [InlineData("marksmanship")]
    [InlineData("survival")]
    public void PetDeathStopsLaterCompanionDamageWithoutStoppingOwner(string spec)
    {
        if(spec=="marksmanship")
        {
            var req=Mm(true);
            req.PetIncomingHitDamage=100000m;
            req.PetIncomingHitEverySeconds=3m;
            var run=new MarksmanshipHunterSimRunner().Run(req);
            Assert.True(run.PetDied);
            var death=Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.ActorDied &&
                e.TargetActorKey=="hunter-pet");
            Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
                e.SourceActorKey=="hunter-pet" && e.TimeSeconds>death.TimeSeconds &&
                e.Amount>0m);
            Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
                e.SourceActorKey=="hunter" && e.TimeSeconds>death.TimeSeconds &&
                e.Amount>0m);
        }
        else
        {
            var req=Survival(true);
            req.PetIncomingHitDamage=100000m;
            var run=new SurvivalHunterSimRunner().Run(req);
            Assert.True(run.PetDied);
            var death=Assert.Single(run.Run.Timeline,e=>e.Type==CombatEventType.ActorDied &&
                e.TargetActorKey=="hunter-pet");
            Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
                e.SourceActorKey=="hunter-pet" && e.TimeSeconds>death.TimeSeconds &&
                e.Amount>0m);
            Assert.True(run.HunterDamage>0m);
        }
    }

    [Theory]
    [InlineData("marksmanship")]
    [InlineData("survival")]
    public void SharedRevivePetResumesActualCompanionDamage(string spec)
    {
        var mm=Mm(true);
        var surv=Survival(true);
        mm.UseRevivePet=true;mm.PetIncomingHitDamage=100000m;
        surv.UseRevivePet=true;surv.PetIncomingHitDamage=100000m;
        var events=spec=="marksmanship"?
            new MarksmanshipHunterSimRunner().Run(mm).Run.Timeline:
            new SurvivalHunterSimRunner().Run(surv).Run.Timeline;
        Assert.Contains(events,e=>e.Type==CombatEventType.ActorDied &&
            e.TargetActorKey=="hunter-pet");
        Assert.Contains(events,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="revive-pet" && e.SourceActorKey=="hunter");
        Assert.Contains(events,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.TimeSeconds>16m && e.Amount>0m);
    }
}
