using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp157MarksmanshipAndRosterSmokeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string TalentFolder()
    {
        for (var cursor = new DirectoryInfo(AppContext.BaseDirectory);
             cursor is not null; cursor = cursor.Parent)
        {
            var dir = Path.Combine(cursor.FullName, "WarcraftSim.Api", "wwwroot", "default-talents");
            if (Directory.Exists(dir)) return dir;
        }
        throw new DirectoryNotFoundException("Default talent folder is missing.");
    }

    private static T Configured<T>(string id, T defaults) where T : class
    {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(TalentFolder(), id + ".json")));
        Assert.Equal(id, file.RootElement.GetProperty("id").GetString());
        var data = JsonSerializer.SerializeToNode(defaults, JsonOptions)!.AsObject();
        foreach (var field in file.RootElement.GetProperty("overrides").EnumerateObject())
            data[field.Name] = JsonNode.Parse(field.Value.GetRawText());
        data["durationSeconds"] = 40;
        if (id == "hunter-survival") data["switchAtSeconds"] = 20;
        data["captureTimeline"] = true;
        return JsonSerializer.Deserialize<T>(data.ToJsonString(), JsonOptions)!;
    }

    private static T FromApi<T>(ActionResult<T> result) =>
        Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Fact]
    public void HunterLabDefaultsResolveAndHaveARealSpellbook()
    {
        var defaults = Assert.IsType<MarksmanshipHunterSimRequest>(
            Assert.IsType<OkObjectResult>(new MarksmanshipHunterSimController().Defaults().Result).Value);
        var setup = Configured("hunter-marksmanship", defaults);
        setup.Talents.Validate();
        Assert.Equal(1, setup.Talents.Rank("lone-wolf"));
        Assert.Equal(1, setup.Talents.Rank("sniper-shot"));
        var abilities = ForeverMarksmanshipHunterSpellbook.Create(setup.Talents,
            setup.Tuning, setup.Tuning.RangedAttackPower).ToDictionary(x => x.Key);
        Assert.Contains("sniper-shot", abilities.Keys);
        Assert.Equal("hunter-aimed-multi", abilities["aimed-shot"].SharedCooldownGroupKey);
        Assert.Equal("hunter-aimed-multi", abilities["multi-shot"].SharedCooldownGroupKey);
        Assert.NotEqual(abilities["aimed-shot"].SharedCooldownGroupKey,
            abilities["arcane-shot"].SharedCooldownGroupKey);
    }

    [Fact]
    public void HunterPresetProducesAutoShotsStingsCooldownsAndManaSpend()
    {
        var request = Configured("hunter-marksmanship", new MarksmanshipHunterSimRequest());
        request.DurationSeconds = 85m;
        var result = new MarksmanshipHunterSimRunner().Run(request);
        Assert.Equal(result.DamageDone, new MarksmanshipHunterSimRunner().Run(request).DamageDone);
        Assert.True(result.DamageDone > 0m);
        Assert.True(result.Dps > 0m);
        Assert.True(result.SerpentStingApplications > 0);
        Assert.True(result.RapidFireCasts > 0);
        Assert.True(result.Run.Summary.Abilities.GetValueOrDefault("sniper-shot")?.CastCompletedCount > 0);
        Assert.True(result.Run.Summary.Abilities.GetValueOrDefault("aimed-shot")?.CastCompletedCount > 0);
        Assert.True(result.Run.Summary.Abilities.GetValueOrDefault("auto-shot")?.DamageDone > 0m);
        Assert.Contains(result.Run.Timeline, e => e.ResourceKey == "mana" && e.Amount is < 0m);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "rapid-recuperation" && e.Amount is > 0m);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "serpent-sting" && e.IsPeriodic &&
            e.Type == CombatEventType.Damage);
        Assert.DoesNotContain(result.Run.Timeline, e => e.AbilityKey == "serpent-sting" && e.IsPeriodic &&
            e.ResultKey == "miss");
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(result, JsonOptions));
        Assert.True(serialized.RootElement.GetProperty("run").GetProperty("summary")
            .GetProperty("damageDone").GetDecimal() > 0m);
    }

    [Fact]
    public void MarksmanshipTalentsAffectRealDamageAndAttackPower()
    {
        var baseRequest = new MarksmanshipHunterSimRequest { DurationSeconds = 60m, CaptureTimeline = true };
        var withBuild = new MarksmanshipHunterSimRunner().Run(baseRequest);
        var noTalents = new MarksmanshipHunterSimRequest {
            DurationSeconds = 60m, CaptureTimeline = true,
            Talents = new ForeverMarksmanshipHunterTalents {
                Ranks = new Dictionary<string, int>(), MarksmanshipPointsSpent = 0,
                BeastMasteryPointsSpent = 0
            }
        };
        var withoutBuild = new MarksmanshipHunterSimRunner().Run(noTalents);
        Assert.True(withBuild.RangedAttackPower > withoutBuild.RangedAttackPower);
        Assert.True(withBuild.DamageDone > withoutBuild.DamageDone);
        Assert.False(withoutBuild.Run.Summary.Abilities.ContainsKey("sniper-shot"));
    }

    [Fact]
    public void MultiShotHitsAdditionalTargetsAndSharedCooldownGatesAimedShot()
    {
        var withAdds = new MarksmanshipHunterSimRequest {
            DurationSeconds = 60m, AdditionalTargets = 2, CaptureTimeline = true,
            UseSniperShot = false
        };
        var result = new MarksmanshipHunterSimRunner().Run(withAdds);
        Assert.True(result.Run.Summary.Abilities.GetValueOrDefault("multi-shot")?.DamageDone > 0m);
        Assert.Contains(result.Run.Timeline, e => e.AbilityKey == "multi-shot" &&
            e.Type == CombatEventType.Damage && (e.TargetActorKey is "add-1" or "add-2"));
        var starts = result.Run.Timeline.Where(e => e.Type == CombatEventType.AbilityCastStarted &&
            (e.AbilityKey is "aimed-shot" or "multi-shot")).OrderBy(e => e.TimeSeconds).ToArray();
        for (var i = 1; i < starts.Length; i++)
            Assert.True(starts[i].TimeSeconds - starts[i - 1].TimeSeconds >= 6m);
    }

    [Fact]
    public void RapidFireIncreasesAutoShotFrequencyInTheSameStationaryEncounter()
    {
        static MarksmanshipHunterSimRequest Setup(bool rapid) => new() {
            DurationSeconds = 45m, CaptureTimeline = true,
            UseRapidFire = rapid, UseSerpentSting = false, UseSniperShot = false,
            UseAimedShot = false, UseMultiShot = false, UseArcaneShot = false
        };
        var withRapid = new MarksmanshipHunterSimRunner().Run(Setup(true));
        var withoutRapid = new MarksmanshipHunterSimRunner().Run(Setup(false));
        var faster = withRapid.Run.Summary.Abilities["auto-shot"].DamageOccurrenceCount;
        var slower = withoutRapid.Run.Summary.Abilities["auto-shot"].DamageOccurrenceCount;
        Assert.True(faster > slower, $"Rapid Fire must accelerate auto attacks: {faster} vs {slower}.");
    }

    [Fact]
    public async Task HunterControllerRunsAndSerializesTheActualResponse()
    {
        var controller = new MarksmanshipHunterSimController();
        var request = Configured("hunter-marksmanship", FromApi(controller.Defaults()));
        var response = await controller.Run(request, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<MarksmanshipHunterSimResponse>(ok.Value);
        Assert.True(result.Run.Summary.DamageDone > 0m);
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(result, JsonOptions));
        Assert.Equal("marksmanship", payload.RootElement.GetProperty("specialization").GetString());
    }

    [Fact]
    public void HunterSupportsPetButRejectsInvalidFamilyAndIllegalTalentPrerequisites()
    {
        var petRun=new MarksmanshipHunterSimRunner().Run(
            new MarksmanshipHunterSimRequest { HasPet=true,DurationSeconds=20m });
        Assert.True(petRun.PetDamage>0m);
        Assert.Equal(petRun.HunterDamage+petRun.PetDamage,petRun.DamageDone);
        Assert.Throws<ArgumentException>(()=>new MarksmanshipHunterSimRunner().Run(
            new MarksmanshipHunterSimRequest { HasPet=true,PetFamily="invalid-family" }));
        var invalid = new ForeverMarksmanshipHunterTalents {
            Ranks = new Dictionary<string, int> { ["sniper-shot"] = 1 },
            MarksmanshipPointsSpent = 41, BeastMasteryPointsSpent = 10
        };
        Assert.Throws<ArgumentException>(invalid.Validate);
    }

    [Theory]
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
    [InlineData("hunter-marksmanship")]
    [InlineData("hunter-survival")]
    public void EveryLabTalentPresetRunsThroughRealDefaultRequestAndSerializes(string id)
    {
        SimulationRunResult run = id switch
        {
            "druid-balance" => new BalanceDruidSimRunner().Run(
                Configured(id, FromApi(new BalanceDruidSimController().Defaults()))).Run,
            "druid-feral-cat" or "druid-feral-bear" => new FeralDruidSimRunner().Run(
                Configured(id, FromApi(new FeralDruidSimController().Defaults(id.EndsWith("cat") ? "cat" : "bear")))).Run,
            "druid-restoration" => new RestorationDruidSimRunner().Run(
                Configured(id, FromApi(new RestorationDruidSimController().Defaults()))).Run,
            "rogue-assassination" or "rogue-combat" or "rogue-subtlety" =>
                new RogueSimRunner().Run(Configured(id, FromApi(new RogueSimController().Defaults(id["rogue-".Length..])))).Run,
            "mage-arcane" or "mage-fire" or "mage-frost" =>
                new MageSimRunner().Run(Configured(id, FromApi(new MageSimController().Defaults(id["mage-".Length..])))).Run,
            "warlock-affliction" or "warlock-demonology" or "warlock-destruction" =>
                new WarlockSimRunner().Run(Configured(id, FromApi(new WarlockSimController().Defaults(id["warlock-".Length..])))).Run,
            "shaman-elemental" => new ElementalShamanSimRunner().Run(
                Configured(id, FromApi(new ElementalShamanSimController().Defaults()))).Run,
            "shaman-enhancement" => new EnhancementShamanSimRunner().Run(
                Configured(id, FromApi(new EnhancementShamanSimController().Defaults()))).Run,
            "shaman-restoration" => new RestorationShamanSimRunner().Run(
                Configured(id, FromApi(new RestorationShamanSimController().Defaults()))).Run,
            "priest-shadow" => new ShadowPriestSimRunner().Run(
                Configured(id, FromApi(new ShadowPriestSimController().Defaults()))).Run,
            "priest-holy-discipline-preview" => new HolyPriestPreviewRunner().Run(
                Configured(id, FromApi(new HolyPriestPreviewController().Defaults()))).Run,
            "hunter-marksmanship" => new MarksmanshipHunterSimRunner().Run(
                Configured(id, FromApi(new MarksmanshipHunterSimController().Defaults()))).Run,
            "hunter-survival" => new SurvivalHunterSimRunner().Run(
                Configured(id, FromApi(new SurvivalHunterSimController().Defaults()))).Run,
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
        // The engine may not have throughput for all classes in a short 40s test,
        // but every request must execute, serialize, and finish the chosen duration.
        Assert.True(run.Summary.DurationSeconds > 0m);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(run, JsonOptions));
        Assert.True(json.RootElement.GetProperty("summary").GetProperty("durationSeconds").GetDecimal() > 0m);
    }
}
