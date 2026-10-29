using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BhMage70685Hotfix
{
    [Fact]
    public void LiveClientHotfixPositionsAndRankCapsAreRepresented()
    {
        Assert.Equal((1,2),ForeverMageTalentBuild.HotfixPositions["improved-channeling"]);
        Assert.Equal((1,3),ForeverMageTalentBuild.HotfixPositions["arcane-resilience"]);
        Assert.Equal((2,4),ForeverMageTalentBuild.HotfixPositions["missile-barrage"]);
        Assert.Equal((3,4),ForeverMageTalentBuild.HotfixPositions["improved-fire-blast"]);
        var a=ForeverMageTalentBuild.Default("arcane");
        Assert.False(a.Ranks.ContainsKey("arcane-focus"));
        Assert.Equal(5,a.Rank("improved-channeling"));
        a.Ranks["arcane-focus"]=5;
        a.Validate("arcane");
        Assert.False(a.Ranks.ContainsKey("arcane-focus"));
        Assert.Equal(0,a.Rank("arcane-focus"));
        a.Ranks["arcane-resilience"]=2;
        a.Validate("arcane");
        a.Ranks["arcane-resilience"]=3;
        Assert.Throws<ArgumentException>(()=>a.Validate("arcane"));
    }

    [Fact]
    public void MissileBarrageNewSecondRowCannotBePurchasedBeforeFiveArcanePoints()
    {
        var t=new ForeverMageTalentBuild {
            ArcanePointsSpent=5,
            Ranks=new(StringComparer.OrdinalIgnoreCase){["missile-barrage"]=1}
        };
        Assert.Throws<ArgumentException>(()=>t.Validate("arcane"));
        t.ArcanePointsSpent=6;
        t.Validate("arcane");
    }

    [Fact]
    public void ImprovedFireBlastNewThirdRowRequiresTenEarlierFirePoints()
    {
        var t=new ForeverMageTalentBuild{
            FirePointsSpent=10,
            Ranks=new(StringComparer.OrdinalIgnoreCase){["improved-fire-blast"]=1}
        };
        Assert.Throws<ArgumentException>(()=>t.Validate("fire"));
        t.FirePointsSpent=11;
        t.Validate("fire");
    }

    [Fact]
    public void LegacyTalentKeysMigrateWithoutRestoringArcaneFocusOrLosingRanks()
    {
        var legacy=ForeverMageTalentBuild.Default("arcane");
        legacy.Ranks["improved-fireball"]=2;
        legacy.Ranks["elemental-precision"]=3;
        legacy.Ranks["arcane-focus"]=5;
        legacy.FrostPointsSpent=3;
        legacy.FirePointsSpent=7;
        // 41 + 7 + 3 remains within the level-60 51-point budget.
        var restored=JsonSerializer.Deserialize<ForeverMageTalentBuild>(
            JsonSerializer.Serialize(legacy))!;
        restored.Validate("arcane");
        Assert.Equal(2,restored.Rank("accelerant"));
        Assert.Equal(2,restored.Rank("improved-fireball"));
        Assert.Equal(3,restored.Rank("precision"));
        Assert.Equal(3,restored.Rank("elemental-precision"));
        Assert.Equal(0,restored.Rank("arcane-focus"));
        Assert.DoesNotContain("arcane-focus",restored.Ranks.Keys);
        Assert.DoesNotContain("improved-fireball",restored.Ranks.Keys);
        Assert.DoesNotContain("elemental-precision",restored.Ranks.Keys);
    }

    [Theory]
    [InlineData(0,3.5,3,3.5)]
    [InlineData(2,3.3,2.8,3.3)]
    [InlineData(5,3,2.5,3)]
    public void AccelerantAppliesExactCastTimeReductionsToAllThreeSpells(
        int rank,double fireball,double frostfire,double wrath)
    {
        var t=ForeverMageTalentBuild.Default("arcane");
        t.Ranks["accelerant"]=rank;
        t.Validate("arcane");
        var spells=ForeverMageDpsSpellbook.Create(t,new ForeverMageTuning(),0m);
        Assert.Equal((decimal)fireball,Assert.Single(spells,x=>x.Key=="fireball").CastTimeSeconds);
        Assert.Equal((decimal)frostfire,Assert.Single(spells,x=>x.Key=="frostfire-bolt").CastTimeSeconds);
        Assert.Equal((decimal)wrath,Assert.Single(spells,x=>x.Key=="arcane-wrath").CastTimeSeconds);
    }

    [Fact]
    public void FireCriticalMassCapThreeAndIncinerationCapFiveAreEnforced()
    {
        var t=ForeverMageTalentBuild.Default("fire");
        t.Ranks["critical-mass"]=3;
        t.Ranks["incineration"]=5;
        t.Validate("fire");
        t.Ranks["critical-mass"]=4;
        Assert.Throws<ArgumentException>(()=>t.Validate("fire"));
        t.Ranks["critical-mass"]=3;
        t.Ranks["incineration"]=6;
        Assert.Throws<ArgumentException>(()=>t.Validate("fire"));
        t.Ranks["incineration"]=5;
        t.Validate("fire");
        t.Ranks["critical-mass"]=2;
        Assert.Throws<ArgumentException>(()=>t.Validate("fire")); // Combustion requires 3/3.
    }

    private static MageSimRequest Setup(string spec,string spell,int precision,int incineration=0)
    {
        var r=MageSimRequest.CreatePreset(spec);
        r.Talents.Ranks["precision"]=precision;
        r.Talents.Ranks["incineration"]=incineration;
        if(spec=="arcane"){
            r.Talents.ArcanePointsSpent=41;
            r.Talents.FirePointsSpent=7;
            r.Talents.FrostPointsSpent=3;
        } else if(spec=="fire") {
            r.Talents.ArcanePointsSpent=7;
            r.Talents.FirePointsSpent=41;
            r.Talents.FrostPointsSpent=3;
        }
        r.Talents.Ranks["critical-mass"]=0;
        r.Talents.Ranks["combustion"]=0;
        r.Talents.Ranks["arcane-concentration"]=0;
        r.Talents.Ranks["arcane-meditation"]=0;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.UseMissileBarrage=false;
        r.UseHeatingUp=false;
        r.UseIgnite=false;
        r.BaseSpellHitPercent=0m;
        r.SpellCritPercent=0m;
        r.TargetMaximumHealth=1000000m;
        r.DurationSeconds=4m;
        r.CustomPriority=[spell];
        return r;
    }

    [Theory]
    [InlineData("arcane","arcane-blast")]
    [InlineData("arcane","frost-blast")]
    [InlineData("fire","fireball")]
    public void PrecisionAddsHitChanceAcrossArcaneFrostAndFire(string spec,string key)
    {
        var plainHits=0;
        var talentedHits=0;
        for(var seed=1;seed<=110;seed++)
        {
            var plain=Setup(spec,key,0);plain.Seed=seed;
            var good=Setup(spec,key,3);good.Seed=seed;
            plainHits+=new MageSimRunner().Run(plain).Run.Timeline.Count(e=>
                e.Type==CombatEventType.Damage && e.AbilityKey==key && e.Amount is >0m && !e.IsPeriodic);
            talentedHits+=new MageSimRunner().Run(good).Run.Timeline.Count(e=>
                e.Type==CombatEventType.Damage && e.AbilityKey==key && e.Amount is >0m && !e.IsPeriodic);
        }
        Assert.Equal(0,plainHits);
        Assert.True(talentedHits>0,$"Precision did not improve {key} hit outcomes.");
    }

    [Fact]
    public void LegacyArcaneFocusRankCannotGrantAnyArcaneHitChance()
    {
        var r=Setup("arcane","arcane-blast",0);
        r.Talents.Ranks["arcane-focus"]=5;
        r.Talents.Validate("arcane");
        for(var seed=1;seed<=15;seed++){
            r.Seed=seed;
            Assert.DoesNotContain(new MageSimRunner().Run(r).Run.Timeline,e=>
                e.Type==CombatEventType.Damage && e.AbilityKey=="arcane-blast" && e.Amount>0m);
        }
    }
}
