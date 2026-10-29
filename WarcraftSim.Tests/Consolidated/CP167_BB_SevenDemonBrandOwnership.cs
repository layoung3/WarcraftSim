using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BbSevenDemonBrandOwnership
{
    [Theory]
    [InlineData("imp","demonic-brand-fire")]
    [InlineData("voidwalker","demonic-brand-shadow")]
    [InlineData("succubus","demonic-brand-shadow")]
    [InlineData("incubus","demonic-brand-shadow")]
    [InlineData("felhunter","demonic-brand-shadow")]
    [InlineData("infernal","demonic-brand-fire")]
    [InlineData("doomguard","demonic-brand-shadow")]
    public void AllSevenDemonFamiliesApplyBrandProcWithOwnerSchoolAndActorAttribution(
        string family,string expectedProc)
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.PetType=family;
        r.PrePullSacrificedDemon=null;
        r.AllowTemporaryDemon=true;
        r.AllowDoomguardRitual=true;
        r.UseLifeTap=false;
        r.UseSearingPain=true;
        r.CustomPriority=["searing-pain"];
        r.DemonTuning.DemonSpellHitPercent=100m;
        r.PetMeleeHitPercent=100m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.TargetMaximumHealth=1000000m;
        r.DurationSeconds=19m;
        r.CaptureTimeline=true;
        var sim=new WarlockSimRunner().Run(r);
        Assert.Contains(sim.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="searing-pain" && e.SourceActorKey=="warlock" &&
            e.Amount is >0m);
        var proc=Assert.Single(sim.Run.Timeline.Where(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey==expectedProc &&
            e.SourceActorKey=="warlock-pet").Take(1));
        Assert.Equal(expectedProc=="demonic-brand-fire"?"fire":"shadow",proc.SchoolKey);
        Assert.Equal("target",proc.TargetActorKey);
        Assert.True(proc.Amount>0m);
        Assert.InRange(Math.Abs(sim.DamageDone-sim.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void WithoutBrandTalentNoPetAttackCanCreateBonusDamage()
    {
        var r=WarlockSimRequest.CreatePreset("demonology");
        r.Talents.Ranks["demonic-brand"]=0;
        r.PetType="imp";
        r.PrePullSacrificedDemon=null;
        r.CustomPriority=["searing-pain"];
        r.BaseSpellHitPercent=100m;
        r.DemonTuning.DemonSpellHitPercent=100m;
        r.DurationSeconds=14m;
        r.CaptureTimeline=true;
        var sim=new WarlockSimRunner().Run(r);
        Assert.DoesNotContain(sim.Run.Timeline,e=>
            e.AbilityKey is ("demonic-brand-fire" or "demonic-brand-shadow"));
    }
}
