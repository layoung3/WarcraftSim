using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175RogueUiContractsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value, Web);

    [Fact]
    public void CuratedPartialPresetIsStrictAndHasNoFillers()
    {
        var controller = new ForeverSoloTalentController();
        var preset = Assert.IsType<ForeverItemizedPresetCatalog.Preset>(Assert.IsType<OkObjectResult>(controller.DefaultItemized("rogue", "assassination")).Value);
        Assert.Equal(38, preset.TotalRankedPoints);
        Assert.Equal(13, preset.UnspentPoints);
        Assert.Equal(12, preset.Ranks.Count);
        Assert.Equal("source-backed-partial-historical-provisional-38-ranks", preset.AllocationStatus);
        Assert.IsType<OkObjectResult>(controller.ValidateItemized("rogue", "assassination", Json(new { ranks = preset.Ranks })));
        Assert.Empty(Json(Assert.IsType<OkObjectResult>(controller.ItemizedCatalog("rogue", "assassination")).Value).GetProperty("unmodeled").EnumerateArray());
        Assert.Equal(new[] { "combat" }, ForeverClassSimulationCapabilities.Get("rogue", "combat").Specializations);
        Assert.Equal(31, Assert.IsType<ForeverItemizedPresetCatalog.Preset>(Assert.IsType<OkObjectResult>(controller.DefaultItemized("rogue", "combat")).Value).TotalRankedPoints);
        foreach (var spec in new[] { "subtlety" })
        {
            Assert.Throws<ArgumentException>(() => ForeverClassSimulationCapabilities.Get("rogue", spec));
            Assert.IsType<BadRequestObjectResult>(controller.DefaultItemized("rogue", spec));
        }
    }

    [Fact]
    public void CapabilityOnlyAdvertisesRestrictedAssassination()
    {
        var capability = ForeverClassSimulationCapabilities.Get("rogue", "assassination");
        Assert.Equal(new[] { "assassination" }, capability.Specializations);
        Assert.True(capability.ExecutesThroughCurrentUnifiedEndpoint);
        Assert.True(capability.HasRotationPreview);
        Assert.False(capability.HasExecutableCustomRotation);
        Assert.False(capability.HasSharedCombatItemProcessor);
        Assert.False(capability.HasSeededBatchResults);
        Assert.False(capability.ExecutesArbitraryCrossTreeModeledTalents);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void ExactPreviewAndFixedPriorityReuseRunnerWithoutMutatingInput(bool provisional)
    {
        var input = RogueSimRequest.CreatePreset("assassination");
        input.Character.Race = "human";
        input.UseProvisionalLevel60Baseline = provisional;
        input.StartingEnergyPercent = 25;
        var before = JsonSerializer.Serialize(input, Web);
        var expected = RogueSimRunner.Preview(input);
        var actual = Assert.IsType<RoguePrecombatPreview>(Assert.IsType<OkObjectResult>(new ForeverCharacterController().PreviewRogue(input)).Value);
        Assert.Equal(Json(expected).GetRawText(), Json(actual).GetRawText());
        Assert.Equal(actual.MaximumEnergy / 4, actual.StartingEnergy);
        var envelope = new ForeverLevel60SimulationRequest { ClassKey = "rogue", Specialization = "assassination", Rogue = input };
        var rotation = Json(Assert.IsType<OkObjectResult>(new ForeverSoloRotationController().Preview(envelope)).Value);
        Assert.Equal("fixed-configured", rotation.GetProperty("mode").GetString());
        Assert.Equal(Json(expected.Rotation.Entries.Select(x => new { x.AbilityKey, x.IsEnabled, x.Priority, x.Conditions })).GetRawText(), rotation.GetProperty("entries").GetRawText());
        Assert.Equal(before, JsonSerializer.Serialize(input, Web));
        envelope.Specialization = "combat";
        Assert.IsType<BadRequestObjectResult>(new ForeverSoloRotationController().Preview(envelope));
        Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewRogue(null));
        Assert.Equal(41, RogueSimRequest.CreatePreset("assassination").Talents.AssassinationPointsSpent);
    }
    [Fact]
    public void ObservedHealthAndEnergyRemainSeparateFromTargetHealth()
    {
        var input = RogueSimRequest.CreatePreset("assassination");
        input.Character.Race = "human";
        input.Level60Baseline = new()
        {
            Race = "human", ClassKey = "rogue", Strength = 100m, Agility = 100m,
            Stamina = 100m, Intellect = 100m, Spirit = 100m,
            ObservedUnbuffedMeleeAttackPower = 700m,
            ObservedUnbuffedMaximumHealth = 3000m,
            ClientBuild = "synthetic", SourceReference = "controlled-preview-endpoint-test",
            RecordedAtUtc = "2026-11-10T00:00:00Z"
        };
        input.RogueMaximumHealth = 4800;
        input.TargetMaximumHealth = 987654;
        input.StartingEnergyPercent = 25;
        input.Talents.Ranks["vigor"] = 2;
        input.Character.Equipment.Items.Add(new() { SlotKey = "chest", ItemKey = "manual",
            Stats = new() { Values = new() { ["stamina"] = 3m, ["health"] = 5m } } });
        var preview = Assert.IsType<RoguePrecombatPreview>(Assert.IsType<OkObjectResult>(new ForeverCharacterController().PreviewRogue(input)).Value);
        Assert.Equal(3035m, preview.MaximumHealth);
        Assert.Equal(110m, preview.MaximumEnergy);
        Assert.Equal(27.5m, preview.StartingEnergy);
        Assert.Equal(987654m, input.TargetMaximumHealth);
        input.Level60Baseline.ObservedUnbuffedMaximumHealth = null;
        preview = Assert.IsType<RoguePrecombatPreview>(Assert.IsType<OkObjectResult>(new ForeverCharacterController().PreviewRogue(input)).Value);
        Assert.Equal(4835m, preview.MaximumHealth);
    }

    [Theory]
    [InlineData("rogue", "combat", "combat", 60)]
    [InlineData("mage", "assassination", "assassination", 60)]
    [InlineData("rogue", "assassination", "combat", 60)]
    [InlineData("rogue", "assassination", "assassination", 59)]
    public void PreviewRejectsMismatchingIdentities(string cls, string spec, string characterSpec, int level)
    {
        var input = RogueSimRequest.CreatePreset("assassination");
        input.Character.ClassKey = cls;
        input.Specialization = spec;
        input.Character.SpecializationKey = characterSpec;
        input.Character.Level = level;
        Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewRogue(input));
        Assert.IsType<BadRequestObjectResult>(new ForeverSoloRotationController().Preview(new()
            { ClassKey = "rogue", Specialization = "assassination", Rogue = input }));
    }

    [Theory]
    [InlineData("energy")][InlineData("poison")][InlineData("charges")][InlineData("armor")]
    public void PreviewPreservesRunnerValidation(string field)
    {
        var input = RogueSimRequest.CreatePreset("assassination");
        switch (field)
        {
            case "energy": input.StartingEnergyPercent = 101; break;
            case "poison": input.MainHandPoison = "invented"; break;
            case "charges": input.PoisonChargesPerWeapon = -1; break;
            case "armor": input.TargetArmor = -1; break;
        }
        Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewRogue(input));
        Assert.IsType<BadRequestObjectResult>(new ForeverSoloRotationController().Preview(new()
            { ClassKey = "rogue", Specialization = "assassination", Rogue = input }));
    }

}
