using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175CombatRogueUiCatalogTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value, Web);
    private static ForeverItemizedPresetCatalog.Preset Preset() =>
        Assert.IsType<ForeverItemizedPresetCatalog.Preset>(Assert.IsType<OkObjectResult>(
            new ForeverSoloTalentController().DefaultItemized("rogue", "combat")).Value);

    [Fact]
    public void DefaultIsExactlyElevenHistoricalCapsAndThirtyOnePoints()
    {
        var p = Preset();
        Assert.Equal("warcraftsim.cp174-itemized-preset", p.Schema);
        Assert.Equal(1, p.Version); Assert.Equal(60, p.Level);
        Assert.Equal("rogue", p.ClassKey); Assert.Equal("combat", p.Specialization);
        Assert.Equal("source-backed-partial-historical-provisional-31-ranks", p.AllocationStatus);
        Assert.Equal(31, p.TotalRankedPoints); Assert.Equal(20, p.UnspentPoints);
        var nodes = ForeverHistoricalCasterTalentCatalog.ForSpecialization("rogue", "combat");
        Assert.Equal(11, p.Ranks.Count);
        Assert.Equal(nodes.ToDictionary(x => x.Key, x => x.MaxRank).OrderBy(x => x.Key), p.Ranks.OrderBy(x => x.Key));
        Assert.All(nodes, x => Assert.Equal("combat", x.Tree));
        Assert.Equal(3, p.Ranks["precision"]);
        Assert.Equal(5, ForeverRogueTalentBuild.Default("combat").Rank("precision"));
        Assert.Equal(5, ForeverRogueTalentBuild.SupportedTalentMetadata.Single(x => x.Key == "precision").MaxRank);
    }

    [Fact]
    public void ProvenanceDistinguishesDecodedReceiptAndInactiveProvisionalMechanics()
    {
        var source = Preset().Provenance;
        Assert.Equal("1cc5ae1e9abdbf941b7326d2d90421822dce31ba", source.GetProperty("sourceCommit").GetString());
        Assert.Equal("https://github.com/gunba/wow-forever-sim/blob/1cc5ae1e9abdbf941b7326d2d90421822dce31ba/ui/core/talents/trees/rogue.json", Assert.Single(source.GetProperty("sources").EnumerateArray()).GetString());
        Assert.Equal("9776311caefba3f7100b3220ffeaaccfc33dd6fb280c18f21172e85c5e984e09", source.GetProperty("localDecodedReceiptSha256").GetString());
        Assert.Contains("decoded", source.GetProperty("acquisition").GetString());
        Assert.Contains("not raw upstream bytes or verified Git blob", source.GetProperty("hashMeaning").GetString());
        Assert.Contains("local strict validation convention", source.GetProperty("parentRankConvention").GetString());
        var statement = source.GetProperty("statement").GetString()!;
        foreach (var text in new[] { "0/31/0", "20 unspent", "not optimized", "not current-client", "Lightning Reflexes", "Puncturing Wounds", "Endurance", "30/60", "10/20", "Blade Flurry", "off-hand", "provisional", "full-parent" })
            Assert.Contains(text, statement);
    }

    [Theory]
    [InlineData(31)] [InlineData(30)] [InlineData(0)]
    public void PresetAndLegalUnderallocationsKeepStrictValidation(int points)
    {
        var ranks = new Dictionary<string, int>(Preset().Ranks);
        if (points == 30) ranks.Remove("adrenaline-rush");
        if (points == 0) ranks.Clear();
        var response = Json(Assert.IsType<OkObjectResult>(new ForeverSoloTalentController().ValidateItemized("rogue", "combat", Json(new {
            ranks, assassinationPointsSpent = 0, combatPointsSpent = points, subtletyPointsSpent = 0,
            totalPoints = points, unspentPoints = 51 - points
        }))).Value);
        Assert.Equal(points, response.GetProperty("pointsSpent").GetInt32());
        Assert.Equal(51 - points, response.GetProperty("remaining").GetInt32());
    }

    [Fact]
    public void CombatCapabilityIsIsolatedAndDoesNotAdvertiseGenericRotation()
    {
        var legacy = ForeverClassSimulationCapabilities.Get("rogue");
        Assert.Same(legacy, ForeverClassSimulationCapabilities.Get("rogue", "assassination"));
        Assert.Equal(new[] { "assassination" }, legacy.Specializations);
        Assert.True(legacy.HasRotationPreview);
        var combat = ForeverClassSimulationCapabilities.Get("rogue", "combat");
        Assert.NotSame(legacy, combat);
        Assert.Same(combat, ForeverClassSimulationCapabilities.Get("ROGUE", "COMBAT"));
        Assert.Contains(combat, ForeverClassSimulationCapabilities.SpecializationOverrides);
        Assert.DoesNotContain(combat, ForeverClassSimulationCapabilities.All);
        Assert.Equal(new[] { "combat" }, combat.Specializations);
        Assert.True(combat.ExecutesThroughCurrentUnifiedEndpoint); Assert.True(combat.HasClassTalentValidation);
        Assert.False(combat.HasRotationPreview); Assert.False(combat.HasExecutableCustomRotation);
        Assert.False(combat.ExecutesArbitraryCrossTreeModeledTalents); Assert.False(combat.HasSharedCombatItemProcessor);
        Assert.False(combat.HasSeededBatchResults);
        Assert.Contains("sword-weapon-budgets", combat.EncounterControls);
        Assert.DoesNotContain("dagger-weapon-budgets", combat.EncounterControls);
        var notes = string.Join(" ", combat.KnownRestrictions);
        foreach (var text in new[] { "0/31/0", "20 unspent", "character/preview-rogue", "flat", "generic rotation", "30/60", "10/20", "100", "Precision", "provisional" }) Assert.Contains(text, notes);
        Assert.Throws<ArgumentException>(() => ForeverClassSimulationCapabilities.Get("rogue", "subtlety"));
        var note = Json(Assert.IsType<OkObjectResult>(new ForeverSoloTalentController().ItemizedCatalog("rogue", "combat")).Value).GetProperty("note").GetString()!;
        Assert.Contains("31 allocated", note); Assert.Contains("20 unspent", note);
        Assert.DoesNotContain("no unmodeled fillers or curated default", note);
        Assert.Equal(38, ForeverItemizedPresetCatalog.Load("rogue", "assassination").TotalRankedPoints);
    }
}
