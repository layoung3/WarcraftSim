using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167BmMage70685LevelSixtyRankData
{
    [Fact]
    public void CurrentClientRankDamageAndManaDefaultsAreNotPreHotfixPlaceholders()
    {
        var t=ForeverMageTalentBuild.Default("fire");
        var spells=ForeverMageDpsSpellbook.Create(t,new ForeverMageTuning(),0m);
        void AssertSpell(string key,decimal mana,decimal expectedDirect)
        {
            var a=Assert.Single(spells,x=>x.Key==key);
            Assert.Equal(mana,Assert.Single(a.ResourceCosts,x=>x.ResourceKey=="mana").Amount);
            Assert.Equal(expectedDirect,a.Effects[0].MinimumValue);
        }
        AssertSpell("frostbolt",290m,475m); // 70685 rank 11, spell 25304
        AssertSpell("fire-blast",340m,438m); // 70685 rank 7, spell 10199
        AssertSpell("frostfire-bolt",370m,292m); // 70685 rank 3, spell 1237313
        AssertSpell("arcane-missiles",655m,209m); // 70685 rank 8, spell 25345
        AssertSpell("scorch",150m,178m); // 70685 rank 7, spell 10207
        var frostfire=Assert.Single(spells,x=>x.Key=="frostfire-bolt");
        var dot=Assert.Single(frostfire.Effects,x=>x.EffectType==
            WarcraftSim.Core.Abilities.AbilityEffectTypes.PeriodicDamage);
        Assert.Equal(9m,dot.DurationSeconds);
        Assert.Equal(3m,dot.TickIntervalSeconds);
        Assert.Equal(19m,dot.MinimumValue);
        Assert.Equal("frostfire-impact",dot.DependsOnEffectKey);
    }

    [Fact]
    public void RankThreeFrostfireActuallyDealsFiftySevenPeriodicDamageAfterLandedImpact()
    {
        MageSimRequest Request(decimal hit)
        {
            var r=MageSimRequest.CreatePreset("fire");
            r.BaseSpellHitPercent=hit;
            r.SpellCritPercent=0m;
            r.Talents.Ranks["critical-mass"]=0;
            r.Talents.Ranks["combustion"]=0;
            r.Talents.Ranks["fire-power"]=0;
            r.Talents.Ranks["ignite"]=0;
            r.UseIgnite=false;
            r.UseHeatingUp=false;
            r.UseEvocation=false;
            r.UseMajorManaPotion=false;
            r.CombatItems.ManaRubyCharges=0;
            r.Tuning.ManaRegenPerSecond=0m;
            // One 370-Mana cast, avoiding the deliberate 9s DoT
            // refresh of back-to-back Frostfire casts.
            r.StartingManaPercent=15m;
            r.Character.BaseStats.Values["spell-power"]=0m;
            r.DurationSeconds=13m;
            r.CustomPriority=["frostfire-bolt"];
            return r;
        }
        var landed=new MageSimRunner().Run(Request(100m));
        var hits=landed.Run.Timeline.Where(x=>x.Type==CombatEventType.Damage &&
            x.AbilityKey=="frostfire-bolt" && x.Amount is >0m).ToArray();
        Assert.True(hits.Length>=4);
        var modeled=ForeverMageDpsSpellbook.Create(
            Request(100m).Talents,Request(100m).Tuning,landed.SpellPower)
            .Single(x=>x.Key=="frostfire-bolt");
        var direct=modeled.Effects.Single(x=>x.Key=="frostfire-impact");
        Assert.Equal(direct.MinimumValue*direct.DamageMultiplier,
            hits.First(x=>!x.IsPeriodic).Amount);
        var dot=modeled.Effects.Single(x=>x.Key=="frostfire-tick");
        Assert.Equal(3,hits.Count(x=>x.IsPeriodic));
        Assert.Equal(3m*dot.MinimumValue*dot.DamageMultiplier,
            hits.Where(x=>x.IsPeriodic).Sum(x=>x.Amount??0m));
        var miss=new MageSimRunner().Run(Request(0m));
        Assert.DoesNotContain(miss.Run.Timeline,x=>x.Type==CombatEventType.Damage &&
            x.AbilityKey=="frostfire-bolt" && x.Amount is >0m);
        Assert.InRange(Math.Abs(landed.DamageDone-landed.DamageByAbility.Values.Sum()),
            0m,.000001m);
    }

    [Fact]
    public void RankEightArcaneMissilesFiveTicksAndCurrentManaCostActuallyExecute()
    {
        var r=MageSimRequest.CreatePreset("arcane");
        r.Talents.Ranks["arcane-concentration"]=0;
        r.Talents.Ranks["arcane-meditation"]=0;
        r.UseMissileBarrage=false;
        r.BaseSpellHitPercent=100m;
        r.SpellCritPercent=0m;
        r.UseEvocation=false;
        r.UseMajorManaPotion=false;
        r.CombatItems.ManaRubyCharges=0;
        r.DurationSeconds=6m;
        r.CustomPriority=["arcane-missiles"];
        var run=new MageSimRunner().Run(r);
        var hits=run.Run.Timeline.Where(x=>x.Type==CombatEventType.Damage &&
            x.AbilityKey=="arcane-missiles" && x.IsChannelTick &&
            x.Amount is >0m).ToArray();
        Assert.Equal(5,hits.Length);
        var modeled=ForeverMageDpsSpellbook.Create(
            r.Talents,r.Tuning,run.SpellPower).Single(x=>x.Key=="arcane-missiles");
        var tick=Assert.Single(modeled.Effects);
        Assert.Equal(209m,r.Tuning.ArcaneMissilesTick);
        Assert.Equal(tick.MinimumValue*tick.DamageMultiplier,hits[0].Amount);
        Assert.Contains(run.Run.Timeline,x=>x.Type==CombatEventType.ResourceChanged &&
            x.AbilityKey=="arcane-missiles" && x.ResourceKey=="mana" &&
            x.Amount==-655m);
    }
}
