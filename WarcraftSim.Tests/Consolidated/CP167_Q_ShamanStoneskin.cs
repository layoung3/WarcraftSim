using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167QShamanStoneskin
{
    [Fact]
    public void StoneskinAndStrengthOfEarthCannotOccupyEarthSlotTogether()
    {
        var req=ElementalPreRaidProfileCatalog.Setup("throughput","raid-sustain");
        req.CaptureTimeline=true;req.DurationSeconds=11m;
        req.UseStoneskinTotem=true;req.UseCallOfElements=true;
        req.IncomingBossHitDamage=100m;req.IncomingBossSwingSeconds=4m;
        req.UseSearingTotem=false;req.UseManaSpringTotem=false;
        req.UseLightningShield=false;req.UseLightningOverload=false;
        req.UseFlameShock=false;req.UseLavaBurst=false;req.UseChainLightning=false;
        req.UseElementalMastery=false;
        var run=new ElementalShamanSimRunner().Run(req);
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.EffectKey=="stoneskin-aura");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.EffectKey=="strength-of-earth-aura");
        Assert.Contains(run.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.EffectKey=="stoneskin-aura");
        Assert.True(run.Run.CustomMetrics.GetValueOrDefault("shaman-totem-replacements")>=1m);
        var incoming=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.EncounterEventKey=="elemental-shield-test").OrderBy(e=>e.TimeSeconds).ToList();
        Assert.True(incoming.Count>=2);
        Assert.Equal(70m,incoming[0].Amount);
        Assert.Equal(100m,incoming[1].Amount);
    }

    [Fact]
    public void StoneskinHasVerifiedCurrentRankAndNoPhantomMana()
    {
        var spell=ForeverShamanTotemCalls.Stoneskin();
        Assert.Equal(210m,spell.ResourceCosts.Single().Amount);
        Assert.Equal(300m,spell.Effects.Single().DurationSeconds);
        Assert.Equal("shaman-stoneskin-totem",spell.Effects.Single().AuraKey);
    }
}
