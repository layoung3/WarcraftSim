using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BkMageFrostfireProcEligibility
{
    private static MageSimRequest Request(bool fireDot, int rank)
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.DurationSeconds=13m;
        r.CaptureTimeline=true;
        r.Talents.Ranks["fingers-of-frost"]=rank;
        r.UseFingersOfFrost=true;
        r.Tuning.FingersOfFrostChancePercentPerRank=100m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseWintersChill=false;
        r.Tuning.ManaRegenPerSecond=0m;
        r.Character.BaseStats.Values["mp5"]=0m;
        r.Character.BaseStats.Values["spirit"]=0m;
        r.CustomPriority=[fireDot?"frostfire-bolt":"frostbolt"];
        return r;
    }

    [Fact]
    public void FrostfirePeriodicTicksNeverGrantExtraFingersProcs()
    {
        var r=Request(true,2);
        // The direct hit can trigger Fingers, but the periodic Fire
        // component cannot count as another Chill or refresh its charges.
        var run=new MageSimRunner().Run(r);
        var directHits=run.Run.Timeline.Count(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="frostfire-bolt" && !e.IsPeriodic && !e.IsChannelTick &&
            e.Amount is >0m);
        var dotTicks=run.Run.Timeline.Count(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="frostfire-bolt" && e.IsPeriodic && e.Amount is >0m);
        Assert.True(directHits>0);
        Assert.True(dotTicks>0);
        Assert.Equal(directHits,run.FingersOfFrostProcs);
        Assert.Equal(2m*directHits,run.Run.CustomMetrics["mage-fingers-charges-granted"]);
        Assert.Equal((decimal)directHits,run.Run.CustomMetrics["mage-fingers-chill-triggers"]);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void FrostboltDirectHitsRetainFingersEligibility()
    {
        var run=new MageSimRunner().Run(Request(false,1));
        var hits=run.Run.Timeline.Count(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="frostbolt" && !e.IsPeriodic && e.Amount is >0m);
        Assert.True(hits>0);
        Assert.Equal(hits,run.FingersOfFrostProcs);
    }

    [Fact]
    public void UntalentedFingersCannotProcFromAnyPeriodicSpell()
    {
        var run=new MageSimRunner().Run(Request(true,0));
        Assert.Equal(0,run.FingersOfFrostProcs);
        Assert.Equal(0m,run.Run.CustomMetrics["mage-fingers-charges-granted"]);
    }
}
