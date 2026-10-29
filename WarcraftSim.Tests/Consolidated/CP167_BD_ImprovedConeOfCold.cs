using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BdImprovedConeOfCold
{
    private static MageSimRequest Frost(int rank)
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.Talents.Ranks["improved-cone-of-cold"]=rank;
        r.DurationSeconds=2m;
        r.CaptureTimeline=true;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.CustomPriority=["cone-of-cold"];
        r.Character.BaseStats.Values["spell-power"]=0m;
        return r;
    }

    [Theory]
    [InlineData(0,1)]
    [InlineData(1,1.12)]
    [InlineData(2,1.23)]
    [InlineData(3,1.35)]
    public void TalentRankActuallyChangesSingleTargetConeDamage(
        int rank,double expectedMultiplier)
    {
        var req=Frost(rank);
        req.Talents.Validate("frost");
        var book=ForeverMageDpsSpellbook.Create(req.Talents,req.Tuning,0m);
        var effect=Assert.Single(book,x=>x.Key=="cone-of-cold").Effects.Single();
        Assert.Equal((decimal)expectedMultiplier,effect.DamageMultiplier);
        var result=new MageSimRunner().Run(req);
        var damage=Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="cone-of-cold" && e.Amount is >0m);
        Assert.Equal(340m*(decimal)expectedMultiplier,damage.Amount);
        Assert.InRange(Math.Abs(result.DamageDone-result.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void InvalidRankOrInsufficientFrostPointsRejected()
    {
        var t=Frost(4);
        Assert.Throws<ArgumentException>(()=>t.Talents.Validate("frost"));
        t=Frost(1);
        t.Talents.FrostPointsSpent=20;
        Assert.Throws<ArgumentException>(()=>t.Talents.Validate("frost"));
    }
}
