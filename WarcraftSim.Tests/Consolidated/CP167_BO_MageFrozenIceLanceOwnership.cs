using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BoMageFrozenIceLanceOwnership
{
    private static MageSimRequest Frost(int seed,bool seekProcs)
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.Seed=seed;
        r.DurationSeconds=24m;
        r.TargetMaximumHealth=1000000m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.Talents.Ranks["shatter"]=0;
        r.Talents.Ranks["winters-chill"]=0;
        r.UseWintersChill=false;
        r.Tuning.FingersOfFrostChancePercentPerRank=50m;
        r.Tuning.ManaRegenPerSecond=1000m;
        r.CaptureTimeline=true;
        if(!seekProcs)
        {
            r.UseFingersOfFrost=false;
            r.CustomPriority=["ice-lance"];
        }
        return r;
    }

    [Fact]
    public void FrozenIceLanceTripleDamageIsOwnedByOriginalExecution()
    {
        var baseline=new MageSimRunner().Run(Frost(1,false));
        var normal=baseline.Run.Timeline.First(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="ice-lance" && e.Amount is >0m && !e.IsCritical).Amount!.Value;
        var observed=false;
        for(var seed=1;seed<=30;seed++)
        {
            var run=new MageSimRunner().Run(Frost(seed,true));
            var lances=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
                e.AbilityKey=="ice-lance" && e.Amount is >0m && !e.IsCritical).ToArray();
            if(lances.Length==0)continue;
            observed=true;
            Assert.Equal(normal*3m,lances[0].Amount);
            Assert.True(lances[0].AbilityExecutionId.HasValue);
            Assert.True(run.FingersOfFrostIceLances>0);
            Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
            break;
        }
        Assert.True(observed,"No Frostbolt-earned Fingers of Frost proc produced an Ice Lance.");
    }
}
