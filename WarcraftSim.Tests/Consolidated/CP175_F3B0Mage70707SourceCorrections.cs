using System;
using System.Linq;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Mage;
using Xunit;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// Provisional client 1.60.1.70707 Mage rank, prerequisite, and
/// actual shared-engine Arcane mechanics (wowfdb.com/talents/mage/).
/// </summary>
public sealed class Cp175F3B0Mage70707SourceCorrections
{
    [Fact]
    public void AllFiftyThreeMageTalentDefinitionsHaveConservativeCoverageStatus()
    {
        var rows=Mage70707TalentCoverageCatalog.Talents;
        Assert.Equal(53,rows.Count);
        Assert.Equal(18,rows.Count(x=>x.Tree=="arcane"));
        Assert.Equal(17,rows.Count(x=>x.Tree=="fire"));
        Assert.Equal(18,rows.Count(x=>x.Tree=="frost"));
        Assert.Equal(16,rows.Count(x=>x.SimulationStatus=="fully-modeled"));
        Assert.Equal(27,rows.Count(x=>x.SimulationStatus=="partially-modeled"));
        Assert.Equal(10,rows.Count(x=>x.SimulationStatus=="not-modeled"));
        Assert.Equal(53,rows.Select(x=>x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach(var modeled in ForeverMageTalentBuild.SupportedTalentMetadata)
        {
            var item=Assert.Single(rows,x=>x.Key==modeled.Key);
            Assert.Equal(modeled.Tree,item.Tree);
            Assert.Equal(modeled.MaxRank,item.MaxRank);
            Assert.NotEqual("not-modeled",item.SimulationStatus);
        }
    }

    [Fact]
    public void ArcaneResilienceSupportsThreeRanksAndRejectsFour()
    {
        var talents = new ForeverMageTalentBuild {
            ArcanePointsSpent = 3,
            Ranks = new(StringComparer.OrdinalIgnoreCase) { ["arcane-resilience"] = 3 }
        };
        talents.Validate("fire"); // Cross-tree behavior is not spec-gated.
        Assert.Equal(3, ForeverMageTalentBuild.SupportedTalentMetadata
            .Single(x => x.Key == "arcane-resilience").MaxRank);
        talents.Ranks["arcane-resilience"] = 4;
        Assert.Throws<ArgumentException>(() => talents.Validate("fire"));
    }

    [Fact]
    public void ArcaneMeditationRequiresFifteenEarlierPointsAndFiveConcentrationRanks()
    {
        var talents = new ForeverMageTalentBuild {
            ArcanePointsSpent = 20,
            Ranks = new(StringComparer.OrdinalIgnoreCase) {
                ["arcane-concentration"] = 4, ["arcane-meditation"] = 1
            }
        };
        Assert.Throws<ArgumentException>(() => talents.Validate("frost"));
        talents.Ranks["arcane-concentration"] = 5;
        talents.Validate("frost");
        talents.ArcanePointsSpent = 15;
        Assert.Throws<ArgumentException>(() => talents.Validate("frost"));
        talents.ArcanePointsSpent = 16;
        talents.Validate("frost");
    }

    private static MageSimRequest ArmorBuild(int rank)
    {
        var request = MageSimRequest.CreatePreset("arcane");
        request.DurationSeconds = 4m;
        request.Talents.Ranks["arcane-resilience"] = rank;
        request.UseEvocation = false;
        request.UseMajorManaPotion = false;
        request.CombatItems.ManaRubyCharges = 0;
        request.UseMissileBarrage = false;
        request.CustomPriority = ["fireball"];
        return request;
    }

    [Fact]
    public void ArcaneResilienceContributesRankScaledArmorToTheRealMageActor()
    {
        var none = new MageSimRunner().Run(ArmorBuild(0));
        var one = new MageSimRunner().Run(ArmorBuild(1));
        var three = new MageSimRunner().Run(ArmorBuild(3));
        const string metric = "mage-arcane-resilience-bonus-armor";
        Assert.Equal(0m, none.Run.CustomMetrics[metric]);
        Assert.True(one.Run.CustomMetrics[metric] > 0m);
        Assert.Equal(3m * one.Run.CustomMetrics[metric], three.Run.CustomMetrics[metric]);
    }

    [Fact]
    public void PublicTalentCatalogRequiresFiveConcentrationRanks()
    {
        var action = new WarcraftSim.Api.Controllers.ForeverSoloTalentController()
            .Catalog("mage", "arcane");
        var json = System.Text.Json.JsonSerializer.Serialize(
            ((Microsoft.AspNetCore.Mvc.OkObjectResult)action).Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var talents = doc.RootElement.GetProperty("talents").EnumerateArray();
        var meditation = talents.Single(x => x.GetProperty("key").GetString() == "arcane-meditation");
        Assert.Equal(5, meditation.GetProperty("enginePrerequisiteRank").GetInt32());
    }

    [Fact]
    public void ArcaneResiliencePrecombatPreviewMatchesCombatAndDoesNotDoubleApply()
    {
        static decimal Preview(int rank)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(new {
                character=new {
                    name="Preview Mage",rulesetKey="wow-forever",classKey="mage",
                    specializationKey="arcane",level=60,
                    baseStats=new { values=new { intellect=200m,armor=50m } },
                    talents=new { selections=new[] {
                        new { talentKey="arcane-resilience",rank } }.Where(x=>x.rank>0).ToArray() }
                }, modifiers=new { buffs=Array.Empty<string>(),
                    consumables=Array.Empty<string>(),targetDebuffs=Array.Empty<string>() }
            });
            var request=System.Text.Json.JsonSerializer.Deserialize<
                WarcraftSim.Api.Controllers.ForeverCharacterResolutionRequest>(json,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
            var response=new WarcraftSim.Api.Controllers.ForeverCharacterController().Resolve(request);
            if(response is Microsoft.AspNetCore.Mvc.BadRequestObjectResult bad)
                throw new Xunit.Sdk.XunitException("Arcane Resilience preview rejected: "+
                    System.Text.Json.JsonSerializer.Serialize(bad.Value));
            var ok=Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(response);
            using var doc=System.Text.Json.JsonDocument.Parse(
                System.Text.Json.JsonSerializer.Serialize(ok.Value,
                    new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
            return doc.RootElement.GetProperty("mageArcaneResilienceBonusArmor").GetDecimal();
        }
        Assert.Equal(0m,Preview(0));
        Assert.Equal(50m,Preview(1));
        Assert.Equal(150m,Preview(3));
    }

    [Fact]
    public void ArcaneBlastFiveCastManaAndDamageFollowAdditiveStacks()
    {
        var request=MageSimRequest.CreatePreset("arcane");
        request.DurationSeconds=9m;
        request.BaseSpellHitPercent=100m;
        request.SpellCritPercent=0m;
        request.Talents.Ranks["arcane-concentration"]=0;
        request.Talents.Ranks["arcane-meditation"]=0;
        request.Talents.Ranks["arcane-mind"]=0;
        request.Talents.Ranks["arcane-instability"]=0;
        request.Talents.Ranks["arcane-impact"]=0;
        request.Character.BaseStats.Values["intellect"]=200m;
        request.Character.BaseStats.Values["spell-power"]=0m;
        request.Character.BaseStats.Values["spirit"]=0m;
        request.Character.BaseStats.Values["mp5"]=0m;
        request.Tuning.ManaRegenPerSecond=0m;
        request.UseEvocation=false;
        request.UseMissileBarrage=false;
        request.UseMajorManaPotion=false;
        request.CombatItems.ManaRubyCharges=0;
        request.CustomPriority=["arcane-blast"];
        var result=new MageSimRunner().Run(request);
        var paid=result.Run.Timeline.Where(e=>
            e.Type==WarcraftSim.Core.Simulation.Engine.CombatEventType.ResourceChanged &&
            e.AbilityKey=="arcane-blast" && e.ResourceKey=="mana" && e.Amount<0m)
            .OrderBy(e=>e.TimeSeconds).ToArray();
        Assert.True(paid.Length>=5,$"Expected five real Blast payments, got {paid.Length}");
        for(var i=0;i<5;i++)
            Assert.Equal(-1376m*.15m*(1m+i),paid[i].Amount);
        var damage=result.Run.Timeline.Where(e=>
            e.Type==WarcraftSim.Core.Simulation.Engine.CombatEventType.Damage &&
            e.AbilityKey=="arcane-blast" && e.Amount>0m && !e.IsPeriodic)
            .OrderBy(e=>e.TimeSeconds).ToArray();
        Assert.True(damage.Length>=5,$"Expected five real Blast hits, got {damage.Length}");
        for(var i=1;i<5;i++)
            Assert.InRange(Math.Abs(damage[i].Amount!.Value-
                (damage[0].Amount!.Value+i*200m*.12m*.4284m)),0m,.0001m);
        Assert.Equal(4m,result.Run.CustomMetrics["mage-arcane-blast-stacks-at-end"]);
    }

    [Fact]
    public void ArcaneBlastStacksExpireEighteenSecondsAfterFirstLandedCast()
    {
        MageSimRequest Setup(decimal seconds)
        {
            var r=MageSimRequest.CreatePreset("arcane");
            r.DurationSeconds=seconds;
            r.CustomPriority=["arcane-blast"];
            r.Talents.Ranks["arcane-concentration"]=0;
            r.Talents.Ranks["arcane-meditation"]=0;
            r.UseEvocation=false;
            r.UseMissileBarrage=false;
            r.CombatItems.ManaRubyCharges=0;
            r.Tuning.ManaRegenPerSecond=0m;
            r.Character.BaseStats.Values["spirit"]=0m;
            r.Character.BaseStats.Values["mp5"]=0m;
            r.StartingManaPercent=6m; // one cast; subsequent casts cannot be funded
            return r;
        }
        var before=new MageSimRunner().Run(Setup(19m));
        var after=new MageSimRunner().Run(Setup(21m));
        Assert.Equal(1m,before.Run.CustomMetrics["mage-arcane-blast-stacks-at-end"]);
        Assert.Equal(0m,after.Run.CustomMetrics["mage-arcane-blast-stacks-at-end"]);
    }

    [Fact]
    public void ArcaneBlastHas70707ProvisionalOneHundredPercentStackedManaSurcharge()
    {
        var request = MageSimRequest.CreatePreset("arcane");
        Assert.Equal(100m, request.Tuning.ArcaneBlastExtraManaPerStackPercent);
        var spell = Assert.Single(ForeverMageDpsSpellbook.Create(
            request.Talents, request.Tuning, 0m), s => s.Key == "arcane-blast");
        Assert.Equal(1376m * .15m, spell.ResourceCosts.Single().Amount);
    }
}
