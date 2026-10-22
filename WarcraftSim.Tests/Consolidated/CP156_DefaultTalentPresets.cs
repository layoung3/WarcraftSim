using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Data.Forever.Druid;
using WarcraftSim.Data.Forever.Hunter;
using WarcraftSim.Data.Forever.Mage;
using WarcraftSim.Data.Forever.Priest;
using WarcraftSim.Data.Forever.Rogue;
using WarcraftSim.Data.Forever.Shaman;
using WarcraftSim.Data.Forever.Warlock;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>
/// Simulation Lab static talent data is validated against actual server-side
/// talent catalogs. Test builds are mechanic-coverage presets, not asserted BiS.
/// </summary>
public sealed class Cp156DefaultTalentPresetsTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static string TalentDirectory()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory);
             folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName, "WarcraftSim.Api", "wwwroot",
                "default-talents");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("Cannot locate WarcraftSim.Api/wwwroot/default-talents.");
    }

    private static JsonElement GetTalentNode(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(TalentDirectory(), name + ".json")));
        Assert.Equal(name, document.RootElement.GetProperty("id").GetString());
        var overrides = document.RootElement.GetProperty("overrides");
        var field = name == "priest-holy-discipline-preview" ? "priestTalents" : "talents";
        var talent = overrides.GetProperty(field);
        var ranks = talent.GetProperty("ranks");
        Assert.True(ranks.EnumerateObject().Any(x => x.Value.GetInt32() > 0),
            $"{name}: no modeled talents are enabled.");
        return talent.Clone();
    }

    private static T Parse<T>(JsonElement node) where T : class =>
        JsonSerializer.Deserialize<T>(node.GetRawText(), Options)
        ?? throw new InvalidOperationException("Talent JSON deserialized to null.");

    [Fact]
    public void EverySimulationLabSpecHasOneNonemptyTalentFile()
    {
        string[] expected = [
            "hunter-marksmanship", "hunter-survival", "hunter-beast-mastery",
            "druid-balance", "druid-feral-cat", "druid-feral-bear", "druid-restoration",
            "rogue-assassination", "rogue-combat", "rogue-subtlety",
            "mage-arcane", "mage-fire", "mage-frost",
            "warlock-affliction", "warlock-demonology", "warlock-destruction",
            "shaman-elemental", "shaman-enhancement", "shaman-restoration",
            "priest-shadow", "priest-holy-discipline-preview"
        ];
        Assert.Equal(expected.Length, Directory.GetFiles(TalentDirectory(), "*.json").Length);
        foreach (var name in expected) _ = GetTalentNode(name);
    }

    [Theory]
    [InlineData("hunter-marksmanship")]
    [InlineData("hunter-survival")]
    [InlineData("hunter-beast-mastery")]
    [InlineData("druid-balance")]
    [InlineData("druid-feral-cat")]
    [InlineData("druid-feral-bear")]
    [InlineData("druid-restoration")]
    [InlineData("rogue-assassination")]
    [InlineData("rogue-combat")]
    [InlineData("rogue-subtlety")]
    [InlineData("mage-arcane")]
    [InlineData("mage-fire")]
    [InlineData("mage-frost")]
    [InlineData("warlock-affliction")]
    [InlineData("warlock-demonology")]
    [InlineData("warlock-destruction")]
    [InlineData("shaman-elemental")]
    [InlineData("shaman-enhancement")]
    [InlineData("shaman-restoration")]
    [InlineData("priest-shadow")]
    [InlineData("priest-holy-discipline-preview")]
    public void EveryPresetPassesItsClassTalentValidator(string id)
    {
        var json = GetTalentNode(id);
        if (id == "hunter-marksmanship") Parse<ForeverMarksmanshipHunterTalents>(json).Validate();
        else if (id == "hunter-survival") Parse<ForeverSurvivalHunterTalents>(json).Validate();
        else if (id == "hunter-beast-mastery") Parse<ForeverBeastMasteryHunterTalents>(json).Validate();
        else if (id == "druid-balance") Parse<ForeverBalanceDruidTalents>(json).Validate();
        else if (id.StartsWith("druid-feral-", StringComparison.Ordinal))
            Parse<ForeverFeralDruidTalents>(json).Validate();
        else if (id == "druid-restoration")
            Parse<ForeverRestorationDruidTalents>(json).Validate();
        else if (id.StartsWith("rogue-", StringComparison.Ordinal))
            Parse<ForeverRogueTalentBuild>(json).Validate(60, id["rogue-".Length..]);
        else if (id.StartsWith("mage-", StringComparison.Ordinal))
            Parse<ForeverMageTalentBuild>(json).Validate(id["mage-".Length..]);
        else if (id.StartsWith("warlock-", StringComparison.Ordinal))
            Parse<ForeverWarlockTalentBuild>(json).Validate(60, id["warlock-".Length..]);
        else if (id == "shaman-elemental")
            Parse<ForeverElementalShamanTalents>(json).Validate();
        else if (id == "shaman-enhancement")
            Parse<ForeverEnhancementShamanTalents>(json).Validate(60);
        else if (id == "shaman-restoration")
            Parse<ForeverRestorationShamanTalents>(json).Validate();
        else if (id == "priest-shadow")
            Parse<ForeverPriestShadowTalentBuild>(json).Validate(60);
        else if (id == "priest-holy-discipline-preview")
            Parse<ForeverPriestHealingTalentBuild>(json).Validate(60);
        else throw new InvalidOperationException($"Unrecognized Lab preset {id}.");
    }

    [Fact]
    public void DefaultCatPresetActuallyCastsShiftingPowerAndRestoresEnergy()
    {
        var talents = Parse<ForeverFeralDruidTalents>(GetTalentNode("druid-feral-cat"));
        Assert.Equal(3, talents.Rank("shredding-attacks"));
        Assert.Equal(1, talents.Rank("shifting-power"));
        Assert.Equal(2, talents.Rank("improved-shifting-power"));
        var result = new FeralDruidSimRunner().Run(new FeralDruidSimRequest
        {
            Role = "cat", Talents = talents, DurationSeconds = 90m,
            StartInProwl = true, UseFeralCooldowns = true, CaptureTimeline = true,
            TargetMaximumHealth = 1000000m
        });
        Assert.True(result.Run.Summary.Abilities.TryGetValue("shifting-power", out var casts)
            && casts.CastCompletedCount > 0);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "shifting-power" &&
            e.ResourceKey == "energy" && e.Amount is > 0m);
    }

    [Fact]
    public void RestorationDefaultProvidesSwiftmendAndWildGrowth()
    {
        var talents = Parse<ForeverRestorationDruidTalents>(GetTalentNode("druid-restoration"));
        var spellKeys = ForeverRestorationDruidSpellbook.Create(talents,
            new ForeverRestorationDruidTuning()).Select(x => x.Key).ToHashSet();
        Assert.Contains("swiftmend", spellKeys);
        Assert.Contains("wild-growth", spellKeys);
        Assert.Contains("natures-swiftness", spellKeys);
    }
}
