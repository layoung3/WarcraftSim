using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BiMage70685CombatOutcome
{
    private static MageSimRequest ArcaneFrostBlast(decimal hit)
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.BaseSpellHitPercent=hit;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.Talents.Ranks["arcane-concentration"]=0;
        r.Talents.Ranks["arcane-meditation"]=0;
        r.UseMissileBarrage=true;
        r.Tuning.MissileBarrageOtherChancePercent=100m;
        r.DurationSeconds=1.8m;
        r.CustomPriority=["frost-blast"];
        return r;
    }

    [Fact]
    public void FrostBlastMissCannotApplyChillOrTriggerMissileBarrage()
    {
        var missed=new MageSimRunner().Run(ArcaneFrostBlast(0m));
        Assert.Equal(0,missed.MissileBarrageProcs);
        Assert.DoesNotContain(missed.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied && e.AbilityKey=="frost-blast");
        var landed=new MageSimRunner().Run(ArcaneFrostBlast(100m));
        Assert.Equal(1,landed.MissileBarrageProcs);
        Assert.Contains(landed.Run.Timeline,e=>
            e.Type==CombatEventType.AuraApplied && e.AbilityKey=="frost-blast" &&
            e.TargetActorKey=="target" && e.EffectKey=="frost-blast-chill");
        var frostBlast=Assert.Single(ForeverMageDpsSpellbook.Create(
            ArcaneFrostBlast(100m).Talents,new ForeverMageTuning(),0m),
            a=>a.Key=="frost-blast");
        Assert.Equal(90m,Assert.Single(frostBlast.ResourceCosts).Amount);
        Assert.Equal(1.5m,frostBlast.CastTimeSeconds);
        Assert.Equal(9m,frostBlast.Effects.Single(e=>e.Key=="frost-blast-chill").DurationSeconds);
    }

    [Theory]
    [InlineData(0,9)]
    [InlineData(1,9.99)]
    [InlineData(3,11.97)]
    public void PermafrostExtendsOnlyLandedFrostBlastChillDuration(
        int rank,double duration)
    {
        var t=ForeverMageTalentBuild.Default("frost");
        t.Ranks["permafrost"]=rank;
        t.Validate("frost");
        var b=Assert.Single(ForeverMageDpsSpellbook.Create(
            t,new ForeverMageTuning(),0m),s=>s.Key=="frost-blast");
        Assert.Equal((decimal)duration,
            b.Effects.Single(e=>e.Key=="frost-blast-chill").DurationSeconds);
    }

    [Fact]
    public void MissileBarrageExpiresFifteenSecondsAfterItsLandedProc()
    {
        MageSimRequest Setup(decimal duration)
        {
            var r=ArcaneFrostBlast(100m);
            r.DurationSeconds=duration;
            r.StartingManaPercent=3m; // enough for one 90 Mana Frost Blast
            r.Tuning.ManaRegenPerSecond=0m;
            r.Character.BaseStats.Values["mp5"]=0m;
            r.Character.BaseStats.Values["spirit"]=0m;
            return r;
        }
        var early=new MageSimRunner().Run(Setup(3m));
        Assert.Equal(1,early.MissileBarrageProcs);
        Assert.Equal(1m,early.Run.CustomMetrics["mage-missile-barrage-active-at-end"]);
        var expired=new MageSimRunner().Run(Setup(20m));
        Assert.Equal(1,expired.MissileBarrageProcs);
        Assert.Equal(0m,expired.Run.CustomMetrics["mage-missile-barrage-active-at-end"]);
        Assert.Single(expired.Run.Timeline,e=>e.Type==CombatEventType.AbilityCastCompleted &&
            e.AbilityKey=="frost-blast");
    }

    [Fact]
    public void AccelerantDoesNotChangeFrostBlastCastTime()
    {
        var t=ForeverMageTalentBuild.Default("arcane");
        t.Ranks["accelerant"]=5;
        t.Validate("arcane");
        var book=ForeverMageDpsSpellbook.Create(t,new ForeverMageTuning(),0m);
        Assert.Equal(1.5m,Assert.Single(book,s=>s.Key=="frost-blast").CastTimeSeconds);
        Assert.Equal(3m,Assert.Single(book,s=>s.Key=="arcane-wrath").CastTimeSeconds);
    }

    [Fact]
    public void ConflictingFrostBlastNumericExportsStayExplicitlyTunable()
    {
        // These are alternative test inputs, NOT a claim about build 70685:
        // build 70532 reported 73-81 while later exports report 80-88.
        // 70630's reported 27.1% coefficient conflicts with other snapshots.
        var t=ForeverMageTalentBuild.Default("frost");
        var v70532=new ForeverMageTuning {
            FrostBlastDamage=77m,FrostBlastSpellPowerCoefficient=.343m
        };
        var v70630=new ForeverMageTuning {
            FrostBlastDamage=84m,FrostBlastSpellPowerCoefficient=.271m
        };
        decimal Impact(ForeverMageTuning tune) =>
            ForeverMageDpsSpellbook.Create(t,tune,200m)
                .Single(x=>x.Key=="frost-blast").Effects
                .Single(x=>x.Key=="frost-blast-impact").MinimumValue;
        Assert.Equal(77m+200m*.343m,Impact(v70532));
        Assert.Equal(84m+200m*.271m,Impact(v70630));
        Assert.NotEqual(Impact(v70532),Impact(v70630));
        var bad=MageSimRequest.CreatePreset("frost");
        bad.Tuning.FrostBlastSpellPowerCoefficient=-.01m;
        Assert.Throws<ArgumentException>(()=>new MageSimRunner().Run(bad));
    }

    [Fact]
    public void OctoberMissileBarrageUsesActualHalfSecondAcceleratedMissileTicks()
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.Talents.Ranks["arcane-concentration"]=0;
        r.Talents.Ranks["arcane-meditation"]=0;
        r.UseMissileBarrage=true;
        r.Tuning.MissileBarrageArcaneMissileChancePercentPerRank=50m;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.DurationSeconds=11m;
        r.CustomPriority=["arcane-missiles"];
        var run=new MageSimRunner().Run(r);
        Assert.True(run.MissileBarrageProcs>0);
        var starts=run.Run.Timeline.Where(e=>e.Type==CombatEventType.AbilityChannelStarted &&
            e.AbilityKey=="arcane-missiles").OrderBy(e=>e.TimeSeconds).ToArray();
        Assert.True(starts.Length>=2);
        var ticks=run.Run.Timeline.Where(e=>e.Type==CombatEventType.Damage &&
            e.AbilityKey=="arcane-missiles" && e.IsChannelTick).ToArray();
        Assert.True(ticks.Length>=5);
        // A buffed channel ticks every 0.5s, not twice at each 1s boundary.
        Assert.Contains(ticks.Zip(ticks.Skip(1)),pair=>
            pair.Second.TimeSeconds-pair.First.TimeSeconds==.5m);
        Assert.InRange(Math.Abs(run.DamageDone-run.DamageByAbility.Values.Sum()),0m,.000001m);
    }

    private static MageSimRequest EligibleSpell(string key,int rank)
    {
        var r=MageSimRequest.CreatePreset("fire");
        r.Talents.FirePointsSpent=35;
        r.Talents.ArcanePointsSpent=8;
        r.Talents.FrostPointsSpent=8;
        r.Talents.Ranks["critical-mass"]=0;
        r.Talents.Ranks["combustion"]=0;
        r.Talents.Ranks["incineration"]=rank;
        if(key=="arcane-blast")r.Talents.Ranks["arcane-blast"]=1;
        if(key=="ice-lance")r.Talents.Ranks["ice-lance"]=1;
        r.SpellCritPercent=0m;
        r.BaseSpellHitPercent=100m;
        r.UseIgnite=false;r.UseHeatingUp=false;
        r.UseEvocation=false;r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.DurationSeconds=9m;
        r.CustomPriority=[key];
        return r;
    }

    [Theory]
    [InlineData("fire-blast")]
    [InlineData("scorch")]
    [InlineData("ice-lance")]
    [InlineData("arcane-blast")]
    public void FiveIncinerationRanksIncreaseActualCritsOfAllFourEligibleSpells(string key)
    {
        int baseline=0,talented=0;
        for(int seed=1;seed<=100;seed++)
        {
            var plain=EligibleSpell(key,0);plain.Seed=seed;
            var strong=EligibleSpell(key,5);strong.Seed=seed;
            baseline+=new MageSimRunner().Run(plain).Run.Timeline.Count(e=>
                e.Type==CombatEventType.Damage && e.AbilityKey==key &&
                !e.IsPeriodic && !e.IsChannelTick && e.IsCritical);
            talented+=new MageSimRunner().Run(strong).Run.Timeline.Count(e=>
                e.Type==CombatEventType.Damage && e.AbilityKey==key &&
                !e.IsPeriodic && !e.IsChannelTick && e.IsCritical);
        }
        Assert.True(talented>baseline,
            $"{key}: 70685 five-rank Incineration produced {talented} crits vs {baseline}.");
    }

    [Fact]
    public void ThreeCriticalMassRanksIncreaseActualFireballCritChance()
    {
        int baseline=0,talented=0;
        for(int seed=1;seed<=100;seed++)
        {
            var no=EligibleSpell("fireball",0);no.Seed=seed;
            var yes=EligibleSpell("fireball",0);yes.Seed=seed;
            yes.Talents.Ranks["critical-mass"]=3;
            baseline+=new MageSimRunner().Run(no).Run.Timeline.Count(e=>
                e.Type==CombatEventType.Damage && e.AbilityKey=="fireball" &&
                !e.IsPeriodic && e.IsCritical);
            talented+=new MageSimRunner().Run(yes).Run.Timeline.Count(e=>
                e.Type==CombatEventType.Damage && e.AbilityKey=="fireball" &&
                !e.IsPeriodic && e.IsCritical);
        }
        Assert.True(talented>baseline);
    }
}
