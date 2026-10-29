using System;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp168AfflictionMultiTargetRotation
{
    private static WarlockSimRequest Setup(int enemies, bool spread)
    {
        var request=WarlockSimRequest.CreatePreset("affliction");
        request.AdditionalTargets=enemies-1;
        request.SpreadAfflictionDots=spread;
        request.EnablePet=false;
        request.PrePullSacrificedDemon=null;
        request.UseLifeTap=false;
        request.UseImmolate=false;
        request.UseWrack=false;
        request.UseBaneOfAgony=true;
        request.UseSiphonLife=true;
        request.BaseSpellHitPercent=100m;
        request.SpellCritPercent=0m;
        request.DurationSeconds=27m;
        request.TargetMaximumHealth=1000000m;
        request.CaptureTimeline=true;
        request.CustomPriority=["corruption","bane-of-agony","siphon-life","shadow-bolt"];
        return request;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LandedDotsApplyToEveryConfiguredEnemyAndDealIndependentDamage(int enemies)
    {
        var request=Setup(enemies,true);
        var run=new WarlockSimRunner().Run(request);
        for(var i=0;i<enemies;i++)
        {
            var key=i==0?"target":$"target-{i}";
            foreach(var spell in new[]{"corruption","bane-of-agony","siphon-life"})
            {
                Assert.Contains(run.Run.Timeline,e=>
                    e.Type==CombatEventType.AuraApplied &&
                    e.AbilityKey==spell && e.SourceActorKey=="warlock" &&
                    e.TargetActorKey==key);
                Assert.Contains(run.Run.Timeline,e=>
                    e.Type==CombatEventType.Damage && e.AbilityKey==spell &&
                    e.TargetActorKey==key && e.Amount>0m);
            }
        }
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void DefaultSingleTargetBehaviorRemainsUnchangedWithAdditionalEnemies()
    {
        var request=Setup(3,false);
        var run=new WarlockSimRunner().Run(request);
        Assert.DoesNotContain(run.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="corruption" && e.TargetActorKey!="target");
    }

    [Fact]
    public void ThreeTargetRotationUsesSharedFixedTargetSelectorAndSerializes()
    {
        var request=Setup(3,true);
        var profile=WarlockAdaptiveRotation.Build(request);
        foreach(var key in new[]{"corruption","bane-of-agony","siphon-life"})
            foreach(var target in new[]{"target-1","target-2"})
                Assert.Contains(profile.Entries,e=>e.AbilityKey==key &&
                    e.Target.Mode==RotationTargetSelectionModes.Fixed &&
                    e.Target.ActorKey==target &&
                    e.Conditions.Any(c=>c.ConditionType==RotationConditionTypes.TargetAuraMissing));
        var copy=JsonSerializer.Deserialize<WarlockSimRequest>(JsonSerializer.Serialize(request));
        Assert.NotNull(copy);
        Assert.True(copy.SpreadAfflictionDots);
        Assert.Equal(2,copy.AdditionalTargets);
    }

    [Fact]
    public void DefaultAfflictionRotationAlsoMaintainsThreeIndependentTargetDots()
    {
        var request=Setup(3,true);
        request.CustomPriority.Clear();
        request.UseWrack=false;
        request.UseLifeTap=false;
        var run=new WarlockSimRunner().Run(request);
        foreach(var target in new[]{"target","target-1","target-2"})
        {
            Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
                e.AbilityKey=="corruption" && e.TargetActorKey==target);
            Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
                e.AbilityKey=="bane-of-agony" && e.TargetActorKey==target);
        }
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void ForcedMissesNeverAttachSecondaryTargetDots()
    {
        var request=Setup(3,true);
        request.BaseSpellHitPercent=0m;
        // Suppression adds talent-derived spell hit even when the base roll
        // is zero; disable it to create a genuine deterministic miss fixture.
        request.Talents.Ranks["suppression"]=0;
        var run=new WarlockSimRunner().Run(request);
        Assert.DoesNotContain(run.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey is ("corruption" or "bane-of-agony" or "siphon-life") &&
            e.SourceActorKey=="warlock");
    }
}
