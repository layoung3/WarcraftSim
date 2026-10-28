using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167AiMagicAbsorption
{
    private static MageSimRequest Setup(int rank)
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.Talents.Ranks["magic-absorption"]=rank;
        r.DurationSeconds=5m;
        r.StartingManaPercent=50m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.IncomingDamageAtSeconds=1m;
        r.IncomingDamageAmount=500m;
        r.IncomingDamageSchool="frost";
        r.IncomingDamageFullyResisted=true;
        r.CustomPriority=["fireball"];
        return r;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void FullResistRestoresCurrentTalentRankFractionOfMaximumMana(int rank)
    {
        var result=new MageSimRunner().Run(Setup(rank));
        var refund=Assert.Single(result.Run.Timeline,e=>
            e.Type==CombatEventType.ResourceChanged &&
            e.AbilityKey=="magic-absorption");
        Assert.Equal(result.MaximumMana*.01m*rank,refund.Amount);
        Assert.Equal(1m,result.Run.CustomMetrics["mage-magic-absorption-refunds"]);
        Assert.Equal(3000m,result.EndingHealth);
    }

    [Fact]
    public void NoTalentCannotProduceRefundAndResistDoesNotDamageHealth()
    {
        var result=new MageSimRunner().Run(Setup(0));
        Assert.DoesNotContain(result.Run.Timeline,e=>e.AbilityKey=="magic-absorption");
        Assert.Equal(3000m,result.EndingHealth);
    }

    [Fact]
    public void AbsorptionRejectsFormerClassicFiveRankAllocation()
    {
        var req=Setup(3);
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(req));
    }

    [Fact]
    public void RealDamageOrPhysicalFullResistCannotTriggerMagicAbsorption()
    {
        var hit=Setup(2);
        hit.IncomingDamageFullyResisted=false;
        Assert.DoesNotContain(new MageSimRunner().Run(hit).Run.Timeline,
            e=>e.AbilityKey=="magic-absorption");
        var physical=Setup(2);
        physical.IncomingDamageSchool="physical";
        Assert.DoesNotContain(new MageSimRunner().Run(physical).Run.Timeline,
            e=>e.AbilityKey=="magic-absorption");
    }
}
