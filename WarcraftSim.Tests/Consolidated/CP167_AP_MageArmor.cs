using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167ApMageArmor
{
    private static MageSimRequest Setup(int arcaneShielding,bool armor)
    {
        var req=MageSimRequest.CreatePreset("arcane");
        req.Talents.Ranks["arcane-shielding"]=arcaneShielding;
        req.DurationSeconds=10m;
        req.BaseSpellHitPercent=100m;
        req.SpellCritPercent=0m;
        req.StartingManaPercent=70m;
        req.UseEvocation=false;
        req.UseMajorManaPotion=false;
        req.CombatItems.ManaRubyCharges=0;
        req.UseMageArmor=armor;
        req.CustomPriority=armor?["mage-armor","fireball"]:["fireball"];
        req.CaptureTimeline=true;
        return req;
    }

    [Theory]
    [InlineData(0,15)]
    [InlineData(1,18.75)]
    [InlineData(2,22.5)]
    public void MageArmorGrantsEffectiveSchoolResistancesAndArcaneShieldingRankModifier(
        int rank,double resistance)
    {
        var request=Setup(rank,true);
        var result=new MageSimRunner().Run(request);
        Assert.Equal((decimal)resistance,result.Run.CustomMetrics["mage-armor-arcane-resistance"]);
        Assert.Single(result.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="mage-armor");
        Assert.Contains(result.Run.Timeline,e=>e.Type==CombatEventType.AuraApplied &&
            e.AbilityKey=="mage-armor");
    }

    [Fact]
    public void MageArmorRetainsHalfSpiritRegenerationInsideFiveSecondRule()
    {
        var noArmor=new MageSimRunner().Run(Setup(0,false));
        var armored=new MageSimRunner().Run(Setup(0,true));
        Assert.True(armored.Run.CustomMetrics["mage-mana-regen-rate-end"] >
            noArmor.Run.CustomMetrics["mage-mana-regen-rate-end"]);
        var spell=ForeverMageDpsSpellbook.Create(Setup(0,true).Talents,
            new ForeverMageTuning(),0m);
        Assert.Equal(490m,Assert.Single(spell,s=>s.Key=="mage-armor").ResourceCosts.Single().Amount);
    }

    [Fact]
    public void ArcaneShieldingCapRejectsThirdRank()
    {
        var r=Setup(3,true);
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(r));
    }
}
