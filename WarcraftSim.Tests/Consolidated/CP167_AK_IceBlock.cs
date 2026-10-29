using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AkIceBlock
{
    private static MageSimRequest Setup(bool talented=true)
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.DurationSeconds=14m;
        r.IncomingDamageAtSeconds=1m;
        r.IncomingDamageAmount=400m;
        r.IncomingDamageSchool="physical";
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CaptureTimeline=true;
        if(talented)r.Talents.Ranks["ice-block"]=1;
        r.CustomPriority=["ice-block","frostbolt"];
        return r;
    }

    [Fact]
    public void IceBlockRankOneCostsFifteenManaAndIsTalentGated()
    {
        var t=ForeverMageTalentBuild.Default("frost");
        Assert.DoesNotContain(ForeverMageDpsSpellbook.Create(t,new ForeverMageTuning(),0m),x=>x.Key=="ice-block");
        t.Ranks["ice-block"]=1;
        var ice=Assert.Single(ForeverMageDpsSpellbook.Create(t,new ForeverMageTuning(),0m),x=>x.Key=="ice-block");
        Assert.Equal(15m,Assert.Single(ice.ResourceCosts).Amount);
        Assert.Equal(300m,ice.CooldownSeconds);
        Assert.Equal(10m,Assert.Single(ice.Effects).DurationSeconds);
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(Setup(false)));
    }

    [Fact]
    public void IceBlockPreventsIncomingDamageAndCastingDuringTenSecondImmunity()
    {
        var run=new MageSimRunner().Run(Setup());
        Assert.Equal(3000m,run.EndingHealth);
        Assert.Contains(run.Run.Timeline,e=>e.EffectKey=="ice-block-immunity" &&
            e.Amount==0m && e.ResultKey=="immune");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="ice-block");
        Assert.DoesNotContain(run.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="frostbolt" && e.TimeSeconds<10m);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="frostbolt" && e.TimeSeconds>=10m);
    }

    [Fact]
    public void IceBlockRequiresFifteenEarlierFrostPoints()
    {
        var t=ForeverMageTalentBuild.Default("frost");
        t.Ranks["ice-block"]=1;
        t.FrostPointsSpent=15;
        Assert.Throws<ArgumentException>(()=>t.Validate("frost"));
    }
}
