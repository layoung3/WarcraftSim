using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175F3D3BBeastMasteryNumerical
{
    private static BeastMasteryHunterSimRequest Controlled() => new() {
        PetFamily="bear",DurationSeconds=70m,Seed=1753330,
        CaptureTimeline=true,TargetMaximumHealth=10000000m,
        UsePetBite=false,UsePetClaw=false,UsePetSpecial=false,
        UseRapidFire=false,UseSummonHawk=false,UseMendPet=false,
        UseIntimidation=false,
        CustomPriority=["bestial-wrath"],
        Tuning=new ForeverBeastMasteryHunterTuning {
            PetHitPercent=100m,PetAvoidancePercent=0m,
            PetDamageMin=100m,PetDamageMax=100m,
            PetBaseAttackPower=0m,PetOwnerAttackPowerInheritance=0m,
            TargetArmor=0m,HunterCriticalPercent=0m,
            HunterMeleeCriticalPercent=0m,PetCriticalBonusPercent=0m
        }
    };

    [Fact]
    public void BestialWrathRaisesActualNoncriticalPetMeleeHitsByExactlyHalfAndThenExpires()
    {
        var req=Controlled();
        var run=new BeastMasteryHunterSimRunner().Run(req);
        var cast=Assert.Single(run.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastCompleted &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="bestial-wrath");
        Assert.Contains(run.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="bestial-wrath" && e.TargetActorKey=="hunter-pet");
        Assert.Contains(run.Run.Timeline,e=>
            e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="bestial-wrath" && e.TargetActorKey=="hunter-pet" &&
            e.TimeSeconds>=cast.TimeSeconds+18m);
        var melee=run.Run.Timeline.Where(e=>
            e.Type==CombatEventType.Damage && e.SourceActorKey=="hunter-pet" &&
            e.AbilityKey=="pet-melee" && e.Amount>0m && !e.IsCritical).ToArray();
        var during=melee.Where(e=>e.TimeSeconds>=cast.TimeSeconds &&
            e.TimeSeconds<cast.TimeSeconds+18m).ToArray();
        var after=melee.Where(e=>e.TimeSeconds>=cast.TimeSeconds+18m).ToArray();
        Assert.NotEmpty(during);
        Assert.NotEmpty(after);
        Assert.All(during,e=>Assert.Equal(after[0].Amount!.Value*1.5m,e.Amount!.Value));
        Assert.All(after,e=>Assert.Equal(after[0].Amount,e.Amount));
        var replay=new BeastMasteryHunterSimRunner().Run(req);
        Assert.Equal(run.PetDamage,replay.PetDamage);
        Assert.Equal(melee.Select(e=>(e.TimeSeconds,e.Amount)).ToArray(),
            replay.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
                e.SourceActorKey=="hunter-pet" && e.AbilityKey=="pet-melee" &&
                e.Amount>0m && !e.IsCritical)
                .Select(e=>(e.TimeSeconds,e.Amount)).ToArray());
    }

    [Fact]
    public void FrenzyRetimesActualPetMeleeAfterCritsAndRemovingTalentStopsProcs()
    {
        var with=Controlled();
        with.DurationSeconds=35m;
        with.UseBestialWrath=false;
        with.Tuning.HunterCriticalPercent=100m;
        with.Tuning.HunterMeleeCriticalPercent=100m;
        var without=Controlled();
        without.DurationSeconds=35m;
        without.UseBestialWrath=false;
        without.Tuning.HunterCriticalPercent=100m;
        without.Tuning.HunterMeleeCriticalPercent=100m;
        without.Talents.Ranks["frenzy"]=0;
        var active=new BeastMasteryHunterSimRunner().Run(with);
        var inactive=new BeastMasteryHunterSimRunner().Run(without);
        var haste=active.Run.Timeline.Where(e=>
            e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="pet-frenzy" && e.TargetActorKey=="hunter-pet").ToArray();
        Assert.NotEmpty(haste);
        Assert.DoesNotContain(inactive.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied && e.AbilityKey=="pet-frenzy");
        decimal[] Swings(BeastMasteryHunterSimResponse run) =>
            run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
                e.SourceActorKey=="hunter-pet" && e.AbilityKey=="pet-melee")
                .Select(e=>e.TimeSeconds).ToArray();
        var sped=Swings(active);
        var normal=Swings(inactive);
        Assert.True(sped.Length>5 && normal.Length>5);
        Assert.True(sped[4]<normal[4],
            "Frenzy must advance real melee timestamps, not just preview haste.");
        var replay=new BeastMasteryHunterSimRunner().Run(with);
        Assert.Equal(sped,Swings(replay));
        Assert.Equal(active.PetDamage,replay.PetDamage);
    }
}
