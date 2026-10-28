using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167YMageFrostboltRanks
{
    [Theory]
    [InlineData(0,"frostbolt",3.0)]
    [InlineData(1,"frostbolt",2.9)]
    [InlineData(3,"frostbolt",2.7)]
    [InlineData(5,"frostbolt",2.5)]
    [InlineData(0,"frost-blast",1.5)]
    [InlineData(1,"frost-blast",1.4)]
    [InlineData(3,"frost-blast",1.2)]
    [InlineData(5,"frost-blast",1.0)]
    public void CurrentImprovedFrostboltRanksAffectBothForeverSpells(int rank,
        string ability,double expectedSeconds)
    {
        var t=ForeverMageTalentBuild.Default("frost");
        t.Ranks["improved-frostbolt"]=rank;
        t.Validate("frost");
        var book=ForeverMageDpsSpellbook.Create(t,new ForeverMageTuning(),0m);
        Assert.Equal((decimal)expectedSeconds,
            Assert.Single(book,s=>s.Key==ability).CastTimeSeconds);
    }

    [Fact]
    public void FrostboltRunsFasterWithRealMaxRankAndDefaultFrostUsesIt()
    {
        MageSimRequest setup(int rank)
        {
            var req=MageSimRequest.CreatePreset("frost");
            req.Talents.Ranks["improved-frostbolt"]=rank;
            req.CustomPriority=["frostbolt"];
            req.BaseSpellHitPercent=100m;
            req.SpellCritPercent=0m;
            req.DurationSeconds=12m;
            req.UseEvocation=false;
            req.UseMajorManaPotion=false;
            req.CombatItems.ManaRubyCharges=0;
            return req;
        }
        Assert.Equal(5,ForeverMageTalentBuild.Default("frost").Rank("improved-frostbolt"));
        var slow=new MageSimRunner().Run(setup(0));
        var quick=new MageSimRunner().Run(setup(5));
        decimal firstCast(MageSimResponse r)
        {
            var started=r.Run.Timeline.First(e=>e.Type==CombatEventType.AbilityCastStarted &&
                e.AbilityKey=="frostbolt");
            var end=r.Run.Timeline.First(e=>e.Type==CombatEventType.AbilityCastCompleted &&
                e.AbilityKey=="frostbolt");
            return end.TimeSeconds-started.TimeSeconds;
        }
        Assert.Equal(3m,firstCast(slow));
        Assert.Equal(2.5m,firstCast(quick));
        Assert.True(quick.DamageDone>=slow.DamageDone);
    }

    [Fact]
    public void RankAboveFiveIsRejected()
    {
        var t=ForeverMageTalentBuild.Default("frost");
        t.Ranks["improved-frostbolt"]=6;
        Assert.Throws<ArgumentException>(()=>t.Validate("frost"));
    }
}
