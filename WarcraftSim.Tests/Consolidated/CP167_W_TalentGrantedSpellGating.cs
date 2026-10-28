using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Warlock;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp167WTalentGrantedSpellGating
{
    [Theory]
    [InlineData("arcane","arcane-blast","arcane-wrath")]
    [InlineData("fire","pyroblast","combustion")]
    [InlineData("frost","ice-lance","cold-snap")]
    public void MageOnlyReceivesTheTalentSpellsKnownToTheChosenBuild(
        string specialization,string learnedOne,string learnedTwo)
    {
        var talents=ForeverMageTalentBuild.Default(specialization);
        var book=ForeverMageDpsSpellbook.Create(talents,new ForeverMageTuning(),0m);
        Assert.Contains(book,a=>a.Key==learnedOne);
        Assert.Contains(book,a=>a.Key==learnedTwo);
        foreach(var key in new[] {"arcane-blast","arcane-wrath","pyroblast","ice-lance"})
            if(talents.Rank(key)==0)
                Assert.DoesNotContain(book,a=>a.Key==key);
    }

    [Fact]
    public void ClearingMageTalentRemovesGrantedSpellFromExecutableSpellbook()
    {
        var talents=ForeverMageTalentBuild.Default("fire");
        talents.Ranks.Remove("pyroblast");
        // Heating Up requires Pyroblast, so disable its dependent talent too.
        talents.Ranks.Remove("heating-up");
        talents.Validate("fire");
        var spells=ForeverMageDpsSpellbook.Create(talents,new ForeverMageTuning(),0m);
        Assert.DoesNotContain(spells,a=>a.Key=="pyroblast");
        Assert.Contains(spells,a=>a.Key=="fireball");
    }

    [Fact]
    public void ConflagrateIsTalentGrantedNotUniversalDestructionSpell()
    {
        var talents=ForeverWarlockTalentBuild.Default("destruction");
        Assert.Equal(1,talents.Rank("conflagrate"));
        var equipped=ForeverWarlockDpsSpellbook.Create("destruction",talents,
            new ForeverWarlockTuning(),150m,180m);
        Assert.Contains(equipped,a=>a.Key=="conflagrate");
        talents.Ranks.Remove("conflagrate");
        talents.Validate(60,"destruction");
        var missing=ForeverWarlockDpsSpellbook.Create("destruction",talents,
            new ForeverWarlockTuning(),150m,180m);
        Assert.DoesNotContain(missing,a=>a.Key=="conflagrate");
        var req=WarlockSimRequest.CreatePreset("destruction");
        req.Talents=talents;
        req.CustomPriority=["conflagrate"];
        Assert.Throws<ArgumentException>(()=>WarlockAdaptiveRotation.Build(req));
    }

    [Fact]
    public void ConflagrateTalentEnforcesCurrentRankAndTier()
    {
        var talents=ForeverWarlockTalentBuild.Default("destruction");
        talents.Ranks["conflagrate"]=2;
        Assert.Throws<ArgumentException>(()=>talents.Validate(60,"destruction"));
        talents.Ranks["conflagrate"]=1;
        talents.DestructionPointsSpent=15;
        talents.AfflictionPointsSpent=10;
        Assert.Throws<ArgumentException>(()=>talents.Validate(60,"destruction"));
    }

    [Theory]
    [InlineData("arcane")]
    [InlineData("fire")]
    [InlineData("frost")]
    public void AllExistingMageDefaultPresetsRemainRunnable(string spec)
    {
        var req=MageSimRequest.CreatePreset(spec);
        req.DurationSeconds=12m;
        var result=new MageSimRunner().Run(req);
        Assert.True(result.DamageDone>0m);
    }
}
