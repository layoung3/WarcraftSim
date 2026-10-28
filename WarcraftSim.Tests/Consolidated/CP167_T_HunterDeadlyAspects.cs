using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167THunterDeadlyAspects
{
    private static BeastMasteryHunterSimRequest Beast(bool useMelee, bool enablePet=false)
    {
        var req=new BeastMasteryHunterSimRequest {
            DurationSeconds=180m, CaptureTimeline=true,
            PetEnabled=enablePet,UsePetSpecial=false,
            UseAspectOfTheBeast=true,UseAspectOfTheHawk=false,
            UseMeleeAuto=useMelee,UseSerpentSting=false,
            UseAimedShot=false,UseMultiShot=false,UseArcaneShot=false,
            UseSummonHawk=false,UseRapidFire=false,UseBestialWrath=false,
            UseIntimidation=false,UseMendPet=false,UseRevivePet=false
        };
        req.Tuning.HunterHitPercent=100m;
        req.Tuning.HunterCriticalPercent=0m;
        req.Tuning.TargetArmor=0m;
        return req;
    }

    [Fact]
    public void BeastModeDeadlyAspectsTriggersFromOwnerMeleeSwingAndReschedulesHaste()
    {
        var run=new BeastMasteryHunterSimRunner().Run(Beast(true));
        var buffs=run.Run.Timeline.Where(e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="deadly-aspects" && e.TargetActorKey=="hunter").ToList();
        Assert.NotEmpty(buffs);
        Assert.Equal(run.Run.CustomMetrics["beast-deadly-aspects-procs"],buffs.Count);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="melee-auto" && e.SourceActorKey=="hunter");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="deadly-aspects");
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void PetMeleeAloneCannotTriggerBeastDeadlyAspects()
    {
        var run=new BeastMasteryHunterSimRunner().Run(Beast(false,true));
        Assert.Equal(0m,run.Run.CustomMetrics["beast-deadly-aspects-procs"]);
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="deadly-aspects");
    }

    [Theory]
    [InlineData(0,0)]
    [InlineData(1,2)]
    [InlineData(2,4)]
    [InlineData(3,6)]
    [InlineData(4,8)]
    [InlineData(5,10)]
    public void CurrentRankProcChanceScalesAtTwoPercentPerTalentPoint(int rank,int expected)
    {
        Assert.Equal(expected,BeastMasteryHunterSimRunner.DeadlyAspectsChancePercent(rank));
    }

    [Fact]
    public void NoTalentDisablesBeastProcAndNewRequestFieldSerializes()
    {
        var req=Beast(true);
        req.Talents.Ranks["deadly-aspects"]=0;
        var run=new BeastMasteryHunterSimRunner().Run(req);
        Assert.Equal(0m,run.Run.CustomMetrics["beast-deadly-aspects-procs"]);
        var roundTrip=JsonSerializer.Deserialize<BeastMasteryHunterSimRequest>(
            JsonSerializer.Serialize(req));
        Assert.NotNull(roundTrip);
        Assert.True(roundTrip.UseMeleeAuto);
    }
}
