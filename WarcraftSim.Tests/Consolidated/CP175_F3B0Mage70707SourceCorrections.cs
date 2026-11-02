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
    public void ArcaneBlastHas70707ProvisionalOneHundredPercentStackedManaSurcharge()
    {
        var request = MageSimRequest.CreatePreset("arcane");
        Assert.Equal(100m, request.Tuning.ArcaneBlastExtraManaPerStackPercent);
        var spell = Assert.Single(ForeverMageDpsSpellbook.Create(
            request.Talents, request.Tuning, 0m), s => s.Key == "arcane-blast");
        Assert.Equal(1376m * .15m, spell.ResourceCosts.Single().Amount);
    }
}
