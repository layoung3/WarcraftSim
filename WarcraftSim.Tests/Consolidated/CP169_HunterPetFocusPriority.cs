using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169HunterPetFocusReservation
{
    private static BeastMasteryHunterSimRequest Setup(string family,bool special)
    {
        return new BeastMasteryHunterSimRequest {
            PetFamily=family,PetEnabled=true,
            DurationSeconds=110m,TargetMaximumHealth=10000000m,
            UsePetSpecial=special,UsePetClaw=true,UsePetBite=true,
            UseAimedShot=false,UseMultiShot=false,UseArcaneShot=false,
            UseSerpentSting=false,UseSummonHawk=false,UseRapidFire=false,
            UseBestialWrath=false,UseIntimidation=false,UseMendPet=false,
            CaptureTimeline=true
        };
    }

    [Fact]
    public void RaptorSavesFocusForAdditionalSavageRendApplications()
    {
        var req=Setup("raptor",true);
        var run=new BeastMasteryHunterSimRunner().Run(req);
        var casts=run.Run.Timeline.Where(e=>
            e.Type==CombatEventType.AbilityCastCompleted &&
            e.SourceActorKey=="hunter-pet" && e.AbilityKey=="pet-savage-rend").ToArray();
        Assert.True(casts.Length>=2,
            $"Raptor special was starved after the first cast: {casts.Length}");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.AbilityKey=="pet-savage-rend" &&
            e.IsPeriodic && e.Amount>0m);
        Assert.True(run.PetSpecialDamage>0m);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void HigherFocusSpellReceivesSecondCastWhenCooldownAndFocusPermit()
    {
        var req=Setup("bat",true);
        req.DurationSeconds=145m;
        var run=new BeastMasteryHunterSimRunner().Run(req);
        var casts=run.Run.Timeline.Where(e=>
            e.Type==CombatEventType.AbilityCastCompleted &&
            e.SourceActorKey=="hunter-pet" && e.AbilityKey=="pet-sonic-blast").ToArray();
        Assert.True(casts.Length>=2,
            $"Bat's 80-Focus Sonic Blast never recast after its cooldown.");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.ResourceChanged &&
            e.SourceActorKey=="hunter-pet" && e.AbilityKey=="pet-sonic-blast" &&
            e.ResourceKey=="focus" && e.Amount==-80m);
    }

    [Fact]
    public void DisablingFamilySpecialKeepsOrdinaryClawAutocastUsable()
    {
        var req=Setup("raptor",false);
        var run=new BeastMasteryHunterSimRunner().Run(req);
        Assert.DoesNotContain(run.Run.Timeline,e=>
            e.AbilityKey=="pet-savage-rend" &&
            e.Type==CombatEventType.AbilityCastCompleted);
        Assert.Contains(run.Run.Timeline,e=>e.AbilityKey=="pet-claw" &&
            e.Type==CombatEventType.Damage && e.Amount>0m);
    }
}
