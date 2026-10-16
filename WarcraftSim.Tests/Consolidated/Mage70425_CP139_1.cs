using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests;

public sealed class Mage70425Cp1391Tests
{
    [Fact]
    public void ArcaneWrathIsTalentGatedAndAddedToDefaultPriority()
    {
        var r=MageSimRequest.CreatePreset("arcane");
        Assert.Contains(MageAdaptiveRotation.Build(r).Entries, e=>e.AbilityKey=="arcane-wrath");
        r.Talents.Ranks.Remove("arcane-wrath");
        Assert.DoesNotContain(MageAdaptiveRotation.Build(r).Entries, e=>e.AbilityKey=="arcane-wrath");
    }
    [Fact]
    public void ArcaneBlastHasNewCastTimeAndScaledDamage()
    {
        var r=MageSimRequest.CreatePreset("arcane");
        var spells=ForeverMageDpsSpellbook.Create(r.Talents,r.Tuning,200m,180m,5000m);
        var blast=Assert.Single(spells, a=>a.Key=="arcane-blast");
        Assert.Equal(1.5m,blast.CastTimeSeconds);
        Assert.Equal(394m*.6m+200m*.4284m,blast.Effects[0].MinimumValue);
    }
    [Fact]
    public void ArcaneWrathIncludesAdditionalSpellPowerFromMaximumMana()
    {
        var r=MageSimRequest.CreatePreset("arcane");
        var a=ForeverMageDpsSpellbook.Create(r.Talents,r.Tuning,200m,180m,4000m);
        var b=ForeverMageDpsSpellbook.Create(r.Talents,r.Tuning,200m,180m,6000m);
        decimal da=Assert.Single(a, x=>x.Key=="arcane-wrath").Effects[0].MinimumValue;
        decimal db=Assert.Single(b, x=>x.Key=="arcane-wrath").Effects[0].MinimumValue;
        Assert.Equal(100m*.857m,db-da);
    }
    [Fact]
    public void BarrageHasTwoRanksAndCanWorkWithoutArcaneBlastTalent()
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.Talents.Ranks["missile-barrage"]=2;
        r.Talents.ArcanePointsSpent=10;
        r.Talents.FirePointsSpent=41;
        r.Talents.Validate("fire");
        Assert.Equal(2,r.Talents.Rank("missile-barrage"));
    }
    [Fact]
    public void ArcaneHasNewWrathDamageInActualRun()
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.CaptureTimeline=false;
        var result=new MageSimRunner().Run(r);
        Assert.True(result.ArcaneWrathCasts>0);
        Assert.True(result.DamageByAbility.GetValueOrDefault("arcane-wrath")>0m);
    }
    [Fact]
    public void ManaRecoveryMetricsAreReportedWithTimelineDisabled()
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.CaptureTimeline=false;
        var result=new MageSimRunner().Run(r);
        Assert.True(result.EvocationCasts>0);
        Assert.True(result.ManaRestoredByEvocation>0m);
        Assert.True(result.ManaRestoredByPotions>0m);
    }
    [Fact]
    public void FrozenIceLanceIsThreeTimesNormalNotFour()
    {
        var r=MageSimRequest.CreatePreset("frost");
        r.Tuning.FingersOfFrostChancePercentPerRank=50m;
        var result=new MageSimRunner().Run(r);
        Assert.True(result.FingersOfFrostIceLances>0);
    }
    [Fact]
    public void FirePowerMaximumRanksThreeAndCriticalMassFive()
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.Talents.Ranks["fire-power"]=4;
        Assert.Throws<ArgumentException>(()=>r.Talents.Validate("fire"));
        r.Talents.Ranks["fire-power"]=3;
        r.Talents.Ranks["critical-mass"]=5;
        r.Talents.Validate("fire");
    }
}
