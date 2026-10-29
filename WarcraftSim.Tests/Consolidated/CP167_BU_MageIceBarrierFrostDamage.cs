using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BuMageIceBarrierFrostDamage
{
    private static MageSimRequest Frost(bool barrier,decimal incoming=0m)
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.DurationSeconds=10m;
        r.CaptureTimeline=true;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseFingersOfFrost=false;
        r.UseWintersChill=false;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CustomPriority=barrier?["ice-barrier","frostbolt"]:["frostbolt"];
        if(barrier)r.Talents.Ranks["ice-barrier"]=1;
        if(incoming>0m)
        {
            r.IncomingDamageAtSeconds=1m;
            r.IncomingDamageAmount=incoming;
        }
        return r;
    }

    private static decimal FirstFrostbolt(MageSimResponse run) =>
        run.Run.Timeline.First(e=>
            e.Type==CombatEventType.Damage &&
            e.AbilityKey=="frostbolt" && e.Amount is >0m).Amount!.Value;

    [Fact]
    public void CurrentBarrierAddsSixPercentFrostDamageWhileShieldRemains()
    {
        var baseline=new MageSimRunner().Run(Frost(false));
        var intact=new MageSimRunner().Run(Frost(true));
        Assert.Equal(FirstFrostbolt(baseline)*1.06m,FirstFrostbolt(intact));
        Assert.Contains(intact.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="ice-barrier" &&
            e.EffectKey=="ice-barrier-frost-offense" && e.TargetActorKey=="mage");
        Assert.Contains(intact.Run.Timeline,e=>e.Type==CombatEventType.AbsorbApplied &&
            e.AbilityKey=="ice-barrier" && e.TargetActorKey=="mage");
    }

    [Fact]
    public void ShieldDepletionRemovesOnlyConditionalThreePercentButKeepsTimedBuff()
    {
        var baseline=new MageSimRunner().Run(Frost(false));
        var depleted=new MageSimRunner().Run(Frost(true,900m));
        Assert.Contains(depleted.Run.Timeline,e=>e.Type==CombatEventType.AbsorbRemoved &&
            e.AbilityKey=="ice-barrier");
        Assert.Equal(FirstFrostbolt(baseline)*1.03m,FirstFrostbolt(depleted));
        Assert.Contains(depleted.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.EffectKey=="ice-barrier-frost-offense");
        Assert.InRange(Math.Abs(depleted.DamageDone-depleted.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void UntalentedMageReceivesNoUnearnedBarrierOffense()
    {
        var r=Frost(false);
        Assert.DoesNotContain(new MageSimRunner().Run(r).Run.Timeline,
            e=>e.AbilityKey=="ice-barrier");
        r.CustomPriority=["ice-barrier","frostbolt"];
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(r));
    }
}
