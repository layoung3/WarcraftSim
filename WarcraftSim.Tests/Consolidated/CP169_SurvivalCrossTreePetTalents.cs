using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp169SurvivalCrossTreePetTalents
{
    private static ForeverBeastMasteryHunterTalents Talents(bool wrath) => new() {
        Ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase) {
            ["endurance-training"]=5,
            ["deadly-aspects"]=5,
            ["unleashed-fury"]=5,
            ["ferocity"]=5,
            ["bestial-swiftness"]=1,
            ["intimidation"]=1,
            ["bestial-discipline"]=2,
            ["frenzy"]=5,
            ["improved-revive-pet"]=1,
            ["bestial-wrath"]=wrath?1:0
        }
    };

    private static SurvivalHunterSimRequest Request(bool wrath,decimal crit=0m)
    {
        var r=new SurvivalHunterSimRequest{
            HasPet=true,PetFamily="bear",CombatMode="melee",
            AdditionalTargets=0,DurationSeconds=35m,
            PetTalents=Talents(wrath),
            UseRapidFire=false,UseFireTraps=false,UseFrostTraps=false,
            ScriptedAvoidanceEverySeconds=null,CaptureTimeline=true,
            TargetMaximumHealth=10000000m
        };
        r.Tuning.HitChancePercent=100m;
        r.Tuning.CriticalChancePercent=crit;
        r.Tuning.TargetDodgePercent=0m;
        r.Tuning.TargetParryPercent=0m;
        r.PetTuning.PetHitPercent=100m;
        r.PetTuning.PetAvoidancePercent=0m;
        return r;
    }

    [Fact]
    public void BestialWrathIsActuallyCastAndIncreasesPetDamageNotOwnerDamage()
    {
        var off=new SurvivalHunterSimRunner().Run(Request(false));
        var on=new SurvivalHunterSimRunner().Run(Request(true));
        Assert.Contains(on.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.SourceActorKey=="hunter" && e.AbilityKey=="bestial-wrath");
        Assert.Contains(on.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="bestial-wrath" && e.TargetActorKey=="hunter-pet");
        // Extra owner casts alter RNG consumption by later pet attacks.
        // Compare effective pet DPS across the full deterministic encounter,
        // and independently require the actual Bestial Wrath pet aura above.
        Assert.True(on.PetDamage>off.PetDamage);
        Assert.InRange(Math.Abs(on.DamageDone-on.HunterDamage-on.PetDamage),
            0m,.000001m);
    }

    [Fact]
    public void FrenzyAndIntimidationProduceRealPetBuffAndThreatEvents()
    {
        var req=Request(true,100m);
        var run=new SurvivalHunterSimRunner().Run(req);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="pet-frenzy" && e.TargetActorKey=="hunter-pet");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.ThreatChanged &&
            e.AbilityKey=="intimidation" && e.SourceActorKey=="hunter-pet" &&
            e.TargetActorKey=="target" && e.Amount>0m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.SourceActorKey=="hunter-pet" && e.IsCritical && e.Amount>0m);
        Assert.True(run.PetThreat>run.PetDamage);
    }

    [Theory]
    [InlineData(1,10)]
    [InlineData(2,5)]
    public void SpiritBondRanksHealAfterScriptedPetInjury(int rank,int firstTick)
    {
        var req=new SurvivalHunterSimRequest{
            HasPet=true,PetFamily="bear",CombatMode="melee",
            AdditionalTargets=0,DurationSeconds=24m,
            UseRapidFire=false,UseFireTraps=false,UseFrostTraps=false,
            ScriptedAvoidanceEverySeconds=null,UseRevivePet=false,
            PetIncomingHitDamage=400m,PetIncomingHitEverySeconds=4m,
            CaptureTimeline=true,TargetMaximumHealth=10000000m,
            PetTalents=new ForeverBeastMasteryHunterTalents{
                Ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase){
                    ["endurance-training"]=5,["deadly-aspects"]=5,
                    ["unleashed-fury"]=5,["ferocity"]=5,
                    ["bestial-swiftness"]=1,["intimidation"]=1,
                    ["spirit-bond"]=rank
                }
            }
        };
        req.PetTuning.PetHitPercent=100m;
        var sim=new SurvivalHunterSimRunner().Run(req);
        var healing=sim.Run.Timeline.Where(e=>
            e.Type==CombatEventType.Healing &&
            e.AbilityKey=="spirit-bond" && e.TargetActorKey=="hunter-pet" &&
            e.Amount>0m).ToArray();
        Assert.NotEmpty(healing);
        Assert.Equal((decimal)firstTick,healing[0].TimeSeconds);
        Assert.InRange(healing[0].Amount!.Value,0.01m,100m);
    }
}
