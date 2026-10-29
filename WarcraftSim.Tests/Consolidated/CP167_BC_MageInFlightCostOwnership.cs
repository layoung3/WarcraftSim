using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BcMageInFlightCostOwnership
{
    private static MageSimRequest Setup(bool power)
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.DurationSeconds=18.5m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.Talents.Ranks["arcane-concentration"]=0;
        r.Talents.Ranks["arcane-meditation"]=0;
        r.Talents.Ranks["improved-fireball"]=0;
        r.CaptureTimeline=true;
        r.CustomPriority=power?["arcane-power","fireball"]:["fireball"];
        return r;
    }

    [Fact]
    public void ArcanePowerExpiringDuringFireballDoesNotRetroactivelyChangeItsManaCost()
    {
        var baseline=new MageSimRunner().Run(Setup(false));
        var powered=new MageSimRunner().Run(Setup(true));

        var preExpireStart=Assert.Single(powered.Run.Timeline,e=>
            e.Type==CombatEventType.AbilityCastStarted &&
            e.AbilityKey=="fireball" && e.TimeSeconds==14m);
        Assert.Equal(14m,preExpireStart.TimeSeconds);
        Assert.Contains(powered.Run.Timeline,e=>e.Type==CombatEventType.AuraRemoved &&
            e.AbilityKey=="arcane-power" && e.TimeSeconds==15m);
        var cost=Assert.Single(powered.Run.Timeline,e=>
            e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="fireball" && e.ResourceKey=="mana" &&
            e.TimeSeconds==17.5m && e.Amount is <0m);
        Assert.Equal(-Setup(true).Tuning.FireballMana*1.30m,cost.Amount);
        var baseHit=Assert.Single(baseline.Run.Timeline,e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="fireball" &&
            e.TimeSeconds==17.5m && e.Amount is >0m);
        var poweredHit=Assert.Single(powered.Run.Timeline,e=>
            e.Type==CombatEventType.Damage && e.AbilityKey=="fireball" &&
            e.TimeSeconds==17.5m && e.Amount is >0m);
        Assert.Equal(baseHit.Amount!.Value*1.30m,poweredHit.Amount);
        Assert.InRange(Math.Abs(powered.DamageDone-powered.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    [Fact]
    public void PowerEndingAfterHardcastRestoresUnaffectedNextSpellCost()
    {
        var req=Setup(true);
        req.DurationSeconds=24m;
        var run=new MageSimRunner().Run(req);
        var nextPaid=run.Run.Timeline.First(e=>
            e.Type==CombatEventType.ResourceChanged && e.AbilityKey=="fireball" &&
            e.ResourceKey=="mana" && e.TimeSeconds>17.5m &&
            e.Amount is <0m);
        Assert.Equal(-req.Tuning.FireballMana,nextPaid.Amount);
    }
}
