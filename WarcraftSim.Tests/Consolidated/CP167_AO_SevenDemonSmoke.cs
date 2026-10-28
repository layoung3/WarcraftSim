using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AoSevenDemonSmoke
{
    [Theory]
    [InlineData("imp","demon-firebolt")]
    [InlineData("voidwalker","demon-torment")]
    [InlineData("succubus","demon-lash-of-pain")]
    [InlineData("incubus","demon-lash-of-pain")]
    [InlineData("felhunter","demon-shadow-bite")]
    [InlineData("infernal","demon-immolation")]
    [InlineData("doomguard","demon-doom-bolt")]
    public void SevenDemonFamiliesExecuteOwnAbilityAndReportCompanionUptime(
        string family,string spell)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType=family;
        r.EnablePet=true;
        r.PrePullSacrificedDemon=null;
        r.AllowTemporaryDemon=true;
        r.AllowDoomguardRitual=true;
        r.DemonTuning.DemonSpellHitPercent=100m;
        r.BaseSpellHitPercent=100m;
        r.UseLifeTap=false;
        r.DurationSeconds=22m;
        r.TargetMaximumHealth=1000000m;
        r.CaptureTimeline=true;
        var result=new WarlockSimRunner().Run(r);
        Assert.Equal(family,result.DemonType);
        Assert.True(result.DemonUptimeSeconds>0m);
        Assert.True(result.DemonSpecialCasts>0);
        Assert.Contains(result.Run.Timeline,e=>e.SourceActorKey=="warlock-pet" &&
            e.AbilityKey==spell &&
            (e.Type==CombatEventType.AbilityCastCompleted ||
             e.Type==CombatEventType.AbilityChannelStarted ||
             e.Type==CombatEventType.Damage));
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Theory]
    [InlineData("infernal")]
    [InlineData("doomguard")]
    public void TemporaryDemonsDespawnAtConfiguredExpiration(string family)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType=family;
        r.PrePullSacrificedDemon=null;
        r.AllowTemporaryDemon=true;
        r.AllowDoomguardRitual=true;
        r.DemonTuning.TemporaryDemonDurationSeconds=8m;
        r.DurationSeconds=14m;
        r.TargetMaximumHealth=1000000m;
        r.CaptureTimeline=true;
        var run=new WarlockSimRunner().Run(r);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.Description!=null && e.Description.Contains("demon-despawn"));
        Assert.True(run.DemonUptimeSeconds<=8m);
    }
}
