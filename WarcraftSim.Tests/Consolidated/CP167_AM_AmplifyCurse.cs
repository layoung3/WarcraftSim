using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AmAmplifyCurse
{
    private static WarlockSimRequest Setup(bool amplify)
    {
        var r=WarlockSimRequest.CreatePreset("affliction");
        r.EnablePet=false;
        r.PrePullSacrificedDemon=null;
        r.UseLifeTap=false;
        r.CaptureTimeline=true;
        r.DurationSeconds=13m;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        if(amplify)r.Talents.Ranks["amplify-curse"]=1;
        r.CustomPriority=amplify?
            ["amplify-curse","bane-of-agony","shadow-bolt"]:
            ["bane-of-agony","shadow-bolt"];
        return r;
    }

    [Fact]
    public void AmplifyCurseIsTalentGatedAndHasThreeMinuteCooldown()
    {
        var off=ForeverWarlockDpsSpellbook.Create("affliction",
            ForeverWarlockTalentBuild.Default("affliction"),new ForeverWarlockTuning(),0m,0m);
        Assert.DoesNotContain(off,s=>s.Key=="amplify-curse");
        var with=Setup(true);
        var on=ForeverWarlockDpsSpellbook.Create("affliction",
            with.Talents,new ForeverWarlockTuning(),0m,0m);
        var amp=Assert.Single(on,s=>s.Key=="amplify-curse");
        Assert.Equal(180m,amp.CooldownSeconds);
        Assert.Empty(amp.ResourceCosts);
        Assert.Equal(30m,Assert.Single(amp.Effects).DurationSeconds);
        Assert.Throws<ArgumentException>(()=>new WarlockSimRunner().Run(
            new WarlockSimRequest{Specialization="affliction",
                Talents=ForeverWarlockTalentBuild.Default("affliction"),
                CustomPriority=["amplify-curse"]}));
    }

    [Fact]
    public void AmplifyCurseAmplifiesBanePeriodicDamageAndConsumesBuff()
    {
        var baseRun=new WarlockSimRunner().Run(Setup(false));
        var empowered=new WarlockSimRunner().Run(Setup(true));
        decimal Tick(WarlockSimResponse r)=>r.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="bane-of-agony" &&
            e.IsPeriodic && e.Amount is >0m).Amount!.Value;
        Assert.Equal(Tick(baseRun)*1.5m,Tick(empowered));
        Assert.Equal(1m,empowered.Run.CustomMetrics["warlock-amplified-banes"]);
        Assert.Contains(empowered.Run.Timeline,e=>
            e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="amplify-curse");
        Assert.InRange(Math.Abs(empowered.DamageDone-empowered.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }
}
