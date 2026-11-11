using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175EnhancementPreviewEndpoints
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static EnhancementShamanSimRequest Request(string mode = "observed")
    {
        var r = new EnhancementShamanSimRequest { DurationSeconds = 18m, CaptureTimeline = true };
        r.Character.Race = "orc";
        if (mode == "provisional") r.UseProvisionalLevel60Baseline = true;
        if (mode == "observed") r.Level60Baseline = new()
        {
            Race = "orc", ClassKey = "shaman", Strength = 100m, Agility = 100m,
            Stamina = 100m, Intellect = 100m, Spirit = 100m,
            ObservedUnbuffedMeleeAttackPower = 700m,
            ObservedUnbuffedMaximumHealth = 3000m, ObservedUnbuffedMaximumMana = 2000m,
            ClientBuild = "synthetic", SourceReference = "controlled-preview-endpoint-test",
            RecordedAtUtc = "2026-11-10T00:00:00Z"
        };
        return r;
    }

    private static EnhancementPrecombatPreview Stats(EnhancementShamanSimRequest r) =>
        Assert.IsType<EnhancementPrecombatPreview>(Assert.IsType<OkObjectResult>(
            new ForeverCharacterController().PreviewEnhancement(r)).Value);

    private static ForeverLevel60SimulationRequest Envelope(EnhancementShamanSimRequest r) =>
        new() { ClassKey = "shaman", Specialization = "enhancement", Enhancement = r };

    private static JsonElement Rotation(EnhancementShamanSimRequest r) => JsonSerializer.SerializeToElement(
        Assert.IsType<OkObjectResult>(new ForeverSoloRotationController().Preview(Envelope(r))).Value, Web);

    [Theory]
    [InlineData("legacy")][InlineData("observed")][InlineData("provisional")]
    public void BothEndpointsProjectTheExactPreparedValuesWithoutCombatOrRequestMutation(string mode)
    {
        var r = Request(mode);
        r.Talents.Ranks = new(r.Talents.Ranks, StringComparer.Ordinal);
        r.Character.Equipment.Items.Add(new() { SlotKey = "chest", ItemKey = "manual",
            Stats = new() { Values = new(StringComparer.Ordinal) { ["intellect"] = 12m, ["spell-power"] = 6m } } });
        var before = JsonSerializer.Serialize(r);
        var p = Stats(r);
        var rotation = Rotation(r);
        var run = new EnhancementShamanSimRunner().Run(r);
        Assert.Equal(run.AttackPower, p.AttackPower);
        Assert.Equal(run.SpellPower, p.SpellPower);
        Assert.Equal(run.MaximumMana, p.MaximumMana);
        Assert.Equal(run.MaximumHealth, p.MaximumHealth);
        Assert.Equal(p.AttackPower, rotation.GetProperty("attackPower").GetDecimal());
        Assert.Equal(p.SpellPower, rotation.GetProperty("spellPower").GetDecimal());
        Assert.Equal("fixed-toggles", rotation.GetProperty("mode").GetString());
        Assert.True(rotation.GetProperty("supported").GetBoolean());
        Assert.Equal("EnhancementShamanSimRunner.BuildRotation", rotation.GetProperty("execution").GetString());
        Assert.Equal("fixed-initial-toggle-priority-no-custom-rotation", p.RotationPreviewScope);
        Assert.Equal(p.RotationPreviewScope, rotation.GetProperty("rotationPreviewScope").GetString());
        Assert.Contains("not-current-client-certified", p.StatPreviewScope);
        Assert.Equal(JsonSerializer.Serialize(p.Rotation.Entries.Select(x => new
            { x.AbilityKey, x.IsEnabled, x.Priority, x.Conditions }), Web),
            rotation.GetProperty("entries").GetRawText());
        var json = JsonSerializer.SerializeToElement(p, Web);
        Assert.False(json.TryGetProperty("run", out _));
        Assert.False(json.TryGetProperty("damageDone", out _));
        Assert.False(json.GetProperty("rotation").TryGetProperty("id", out _));
        Assert.Equal(before, JsonSerializer.Serialize(r));
        Assert.Same(StringComparer.Ordinal, r.Talents.Ranks.Comparer);
        Assert.Same(StringComparer.Ordinal, r.Character.Equipment.Items[0].Stats.Values.Comparer);
    }

    [Theory][InlineData(true, 3035)][InlineData(false, 4835)]
    public void EndpointResourceOraclePreservesObservedHealthPrecedenceAndSingleApplication(bool observedHealth, int health)
    {
        var r = Request(); r.MaximumHealth = 4800m;
        if (!observedHealth) r.Level60Baseline!.ObservedUnbuffedMaximumHealth = null;
        r.Talents.Ranks["ancestral-knowledge"] = 5;
        r.Character.Equipment.Items.Add(new() { SlotKey = "chest", ItemKey = "manual",
            Stats = new() { Values = new() { ["intellect"] = 12m, ["strength"] = 10m,
                ["stamina"] = 3m, ["health"] = 5m, ["mana"] = 4m, ["spell-power"] = 6m } } });
        r.DualWield = true; r.MainHandImbue = "rockbiter"; r.OffHandImbue = "rockbiter";
        r.UseCallOfElements = true;
        var p = Stats(r);
        Assert.Equal(123.2m, p.EffectiveStats.Get("intellect")); // (100 + 12) * 1.10
        Assert.Equal(843.2m, p.AttackPower); // 700 + 2 * 10 + 123.2
        Assert.Equal(42.96m, p.SpellPower); // 6 + 123.2 * .30
        Assert.Equal(2352m, p.MaximumMana); // 2000 + 23.2 * 15 + 4
        Assert.Equal(health, p.MaximumHealth);
        Assert.False(p.EffectiveStats.Contains("shaman-main-hand-attack-power"));
        Assert.False(p.EffectiveStats.Contains("shaman-off-hand-attack-power"));
        Assert.Contains(p.ModelingNotes, x => x.Contains("before hand-local Rockbiter"));
        Assert.Contains(p.ModelingNotes, x => x.Contains("Nature spells remain unmitigated"));
        var run = new EnhancementShamanSimRunner().Run(r);
        Assert.Equal(p.AttackPower, run.AttackPower);
        Assert.Equal(p.SpellPower, run.SpellPower);
        Assert.Equal(p.MaximumMana, run.MaximumMana);
        Assert.Equal(p.MaximumHealth, run.MaximumHealth);
    }

    [Fact]
    public void ProjectionPreservesActualFixedOrderingAndAuraPredicates()
    {
        var r = Request();
        r.UseStoneskinTotem = r.UseCallOfAncestors = r.UseCallOfSpirits = true;
        r.UseCallOfElements = r.UseTotemicRecall = r.UseLightningShield = true;
        r.UseLightningBoltFiller = r.DeployWindfuryTotem = r.DualWield = true;
        r.MainHandImbue = "rockbiter"; r.OffHandImbue = "flametongue";
        var entries = Stats(r).Rotation.Entries;
        Assert.Equal(new[] { "stoneskin-totem", "call-of-ancestors", "call-of-spirits",
            "call-of-elements", "totemic-recall", "lightning-shield", "rockbiter-weapon-main-hand",
            "flametongue-weapon-off-hand", "rage-of-the-farseer", "maelstrom-lightning-bolt",
            "stormstrike", "earth-shock", "lightning-bolt" }, entries.Select(x => x.AbilityKey));
        Assert.Equal(Enumerable.Range(1, entries.Count), entries.Select(x => x.Priority));
        Assert.All(entries, x => Assert.True(x.IsEnabled));
        var predicates = new Dictionary<string, (string Type, string Key)>
        {
            ["stoneskin-totem"] = (RotationConditionTypes.SourceAuraMissing, "shaman-stoneskin-totem"),
            ["call-of-ancestors"] = (RotationConditionTypes.SourceAuraMissing, "shaman-stoneskin-totem"),
            ["call-of-spirits"] = (RotationConditionTypes.SourceAuraMissing, "shaman-strength-of-earth-totem"),
            ["call-of-elements"] = (RotationConditionTypes.SourceAuraMissing, "shaman-strength-of-earth-totem"),
            ["totemic-recall"] = (RotationConditionTypes.SourceAuraActive, "shaman-strength-of-earth-totem"),
            ["lightning-shield"] = (RotationConditionTypes.SourceAuraMissing, "shaman-lightning-shield"),
            ["rockbiter-weapon-main-hand"] = (RotationConditionTypes.SourceAuraMissing, "shaman-imbue-main-hand-rockbiter"),
            ["flametongue-weapon-off-hand"] = (RotationConditionTypes.SourceAuraMissing, "shaman-imbue-off-hand-flametongue"),
            ["maelstrom-lightning-bolt"] = (RotationConditionTypes.SourceAuraActive, "maelstrom-ready")
        };
        foreach (var entry in entries)
        {
            if (!predicates.TryGetValue(entry.AbilityKey, out var expected)) { Assert.Empty(entry.Conditions); continue; }
            var condition = Assert.Single(entry.Conditions);
            Assert.Equal(expected.Type, condition.ConditionType);
            Assert.Equal(expected.Key, condition.Key);
            Assert.Null(condition.Value);
        }
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(4)][InlineData(5)]
    public void MaelstromProjectionUsesActualRankGateAndDoesNotAddUnlearnedInstantBolt(int rank)
    {
        var r = Request(); r.Talents.Ranks["maelstrom-weapon"] = rank;
        r.UseLightningBoltFiller = true;
        var entries = Stats(r).Rotation.Entries;
        var gated = entries.Where(x => x.Conditions.Any(c => c.Key == "maelstrom-ready")).ToArray();
        if (rank == 0) Assert.Empty(gated);
        else Assert.Equal(rank == 5 ? "maelstrom-lightning-bolt" : "lightning-bolt", Assert.Single(gated).AbilityKey);
        Assert.Equal("lightning-bolt", entries.Last().AbilityKey);
        Assert.Empty(entries.Last().Conditions);
        r.UseMaelstromLightningBolt = false;
        Assert.DoesNotContain(Stats(r).Rotation.Entries, x => x.Conditions.Any(c => c.Key == "maelstrom-ready"));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void ToggleAndLearnedTalentGatesRemainTheRuntimeGates(bool learned)
    {
        var r = Request();
        if (!learned)
        {
            r.Talents.Ranks["stormstrike"] = r.Talents.Ranks["improved-stormstrike"] = 0;
            r.Talents.Ranks["rage-of-the-farseer"] = r.Talents.Ranks["maelstrom-weapon"] = 0;
        }
        else r.UseStormstrike = r.UseRageOfTheFarseer = r.UseMaelstromLightningBolt = false;
        r.UseEarthShock = r.UseLightningBoltFiller = false;
        Assert.Empty(Stats(r).Rotation.Entries);
        var run = new EnhancementShamanSimRunner().Run(r);
        Assert.True(run.DamageByAbility.GetValueOrDefault("main-hand-auto") > 0m);
        Assert.DoesNotContain(run.Run.Timeline, e => e.Type == CombatEventType.AbilityCastStarted &&
            e.AbilityKey is "stormstrike" or "rage-of-the-farseer" or "maelstrom-lightning-bolt" or "earth-shock");
    }

    [Theory][InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public void WindfuryDeploymentIsDistinctFromStaticProcAndCallPolicies(bool deploy, bool call)
    {
        var r = Request(); r.DeployWindfuryTotem = deploy; r.UseCallOfElements = call;
        var entries = Stats(r).Rotation.Entries;
        Assert.Equal(deploy && !call, entries.Any(x => x.AbilityKey == "windfury-totem"));
        Assert.Equal(call, entries.Any(x => x.AbilityKey == "call-of-elements"));
        Assert.DoesNotContain(entries, x => x.AbilityKey.Contains("auto") || x.AbilityKey.Contains("extra") ||
            x.AbilityKey == "windfury-weapon");
        r.UseWindfuryTotem = false;
        Assert.DoesNotContain(Stats(r).Rotation.Entries, x => x.AbilityKey == "windfury-totem");
    }

    [Theory][InlineData("none")][InlineData("windfury")][InlineData("flametongue")]
    [InlineData("frostbrand")][InlineData("rockbiter")]
    public void HandImbueProjectionIncludesOnlyRealAuraCasts(string mainHand)
    {
        var r = Request(); r.MainHandImbue = mainHand; r.DualWield = true; r.OffHandImbue = "frostbrand";
        var entries = Stats(r).Rotation.Entries;
        Assert.Equal(mainHand is not ("none" or "windfury"), entries.Any(x => x.AbilityKey.EndsWith("-main-hand")));
        if (mainHand is not ("none" or "windfury"))
            Assert.Contains(entries, x => x.AbilityKey == $"{mainHand}-weapon-main-hand");
        Assert.Contains(entries, x => x.AbilityKey == "frostbrand-weapon-off-hand");
        r.DualWield = false; r.OffHandImbue = "none";
        Assert.DoesNotContain(Stats(r).Rotation.Entries, x => x.AbilityKey.EndsWith("-off-hand"));
    }

    [Fact]
    public void ReturnedStatsEntriesConditionsAndRequestRemainIndependentlyIsolated()
    {
        var r = Request(); r.UseLightningShield = true;
        var before = JsonSerializer.Serialize(r);
        var p = Stats(r); var expected = JsonSerializer.Serialize(p, Web);
        var run = new EnhancementShamanSimRunner().Run(r);
        p.EffectiveStats.Values.Clear();
        p.Rotation.Entries[0].AbilityKey = "not-a-real-ability";
        p.Rotation.Entries[0].Conditions[0].Key = "not-a-real-aura";
        p.Rotation.Entries[0].Conditions.Add(new() { ConditionType = "not-a-real-condition" });
        p.Rotation.Entries[1].IsEnabled = false;
        Assert.Equal(expected, JsonSerializer.Serialize(Stats(r), Web));
        Assert.Equal(before, JsonSerializer.Serialize(r));
        var replay = new EnhancementShamanSimRunner().Run(r);
        Assert.Equal(Fingerprint(run), Fingerprint(replay));
        var independent = Stats(r);
        r.UseLightningShield = false; r.Character.BaseStats.Set("intellect", 99999m);
        r.Talents.Ranks.Clear(); r.CallOfElementsTotems.Clear();
        Assert.Equal(expected, JsonSerializer.Serialize(independent, Web));
    }

    private static string Fingerprint(EnhancementShamanSimResponse r) => JsonSerializer.Serialize(new
    { r.DamageDone, r.EndingMana, r.MaximumMana, r.MaximumHealth, r.EndingHealth, r.AttackPower,
      r.SpellPower, r.DamageByAbility, r.WindfuryWeaponProcs, r.MaelstromInstantBolts, r.Run.ProcessedEventCount });

    [Theory]
    [InlineData("character")][InlineData("spec")][InlineData("mana")][InlineData("health")]
    [InlineData("target-health")][InlineData("target-armor")][InlineData("weapon")][InlineData("offhand-windfury")]
    [InlineData("rank")][InlineData("catalog")][InlineData("call")][InlineData("combat-items")]
    public void NewEndpointsRejectTheSameMalformedSetupAsExecution(string kind)
    {
        var r = Request();
        switch (kind)
        {
            case "character": r.Character = null!; break;
            case "spec": r.Character.SpecializationKey = "elemental"; break;
            case "mana": r.Level60Baseline!.ObservedUnbuffedMaximumMana = 0m; break;
            case "health": r.MaximumHealth = 0m; break;
            case "target-health": r.TargetMaximumHealth = 0m; break;
            case "target-armor": r.TargetArmor = -1m; break;
            case "weapon": r.WeaponSpeedSeconds = 0m; break;
            case "offhand-windfury": r.DualWield = true; r.OffHandImbue = "windfury"; break;
            case "rank": r.Talents.Ranks["ancestral-knowledge"] = 6; break;
            case "catalog": r.Character.Equipment.Items.Add(new() { ItemId = 19102 }); break;
            case "call": r.CallOfAncestorsTotems = ["unknown-totem"]; break;
            case "combat-items": r.CombatItems.ManaRubyCharges = 1; break;
        }
        var before = JsonSerializer.Serialize(r);
        var error = Assert.ThrowsAny<ArgumentException>(() => new EnhancementShamanSimRunner().Run(r));
        var stats = Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewEnhancement(r));
        var rotation = Assert.IsType<BadRequestObjectResult>(new ForeverSoloRotationController().Preview(Envelope(r)));
        Assert.Equal(error.Message, JsonSerializer.SerializeToElement(stats.Value, Web).GetProperty("error").GetString());
        Assert.Equal(error.Message, JsonSerializer.SerializeToElement(rotation.Value, Web).GetProperty("error").GetString());
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }

    [Fact]
    public void EmptyAndMismatchedTypedPreviewEnvelopesFailClosed()
    {
        Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewEnhancement(null));
        var controller = new ForeverSoloRotationController();
        Assert.IsType<BadRequestObjectResult>(controller.Preview(null));
        Assert.IsType<BadRequestObjectResult>(controller.Preview(new() { ClassKey = "shaman", Specialization = "enhancement" }));
        var r = Envelope(Request()); r.Specialization = "elemental";
        Assert.IsType<BadRequestObjectResult>(controller.Preview(r));
        r.Specialization = "enhancement"; r.ClassKey = "druid";
        Assert.IsType<BadRequestObjectResult>(controller.Preview(r));
        r.ClassKey = null!;
        Assert.IsType<BadRequestObjectResult>(controller.Preview(r));
    }

    [Fact]
    public void UnknownJsonFieldsRetainLegacyBindingButCannotBecomeCustomRotationOrModifiers()
    {
        var r = Request(); var expected = Rotation(r).GetRawText();
        var json = JsonSerializer.SerializeToNode(Envelope(r), Web)!.AsObject();
        json["customPriority"] = new JsonArray("fabricated-action");
        json["priorityConditions"] = new JsonObject { ["stormstrike"] = new JsonArray("fabricated-predicate") };
        json["enhancement"]!["customPriority"] = new JsonArray("fabricated-action");
        json["enhancement"]!["rotationPolicy"] = "unsupported";
        json["enhancement"]!["externalModifiers"] = new JsonObject { ["buffs"] = new JsonArray("blessing-of-kings") };
        var rebound = json.Deserialize<ForeverLevel60SimulationRequest>(Web)!;
        var result = Assert.IsType<OkObjectResult>(new ForeverSoloRotationController().Preview(rebound));
        Assert.Equal(expected, JsonSerializer.Serialize(result.Value, Web));
        Assert.Equal(JsonSerializer.Serialize(Stats(r), Web), JsonSerializer.Serialize(Stats(rebound.Enhancement!), Web));
        Assert.DoesNotContain(typeof(EnhancementShamanSimRequest).GetProperties(), p =>
            p.Name is "CustomPriority" or "PriorityConditions" or "RotationPolicy" or "ExternalModifiers");
    }

    [Fact]
    public void SpecializationCapabilitiesDoNotBroadenTheExistingElementalContract()
    {
        var controller = new ForeverSharedCharacterController();
        var original = ForeverClassSimulationCapabilities.Get("shaman");
        var elemental = ForeverClassSimulationCapabilities.Get("shaman", "elemental");
        Assert.Same(original, elemental);
        Assert.Equal(new[] { "elemental" }, original.Specializations);
        Assert.True(original.HasExecutableCustomRotation);
        var enhancement = Assert.IsType<ForeverClassSimulationCapability>(Assert.IsType<OkObjectResult>(
            controller.Capability("Shaman", "Enhancement")).Value);
        Assert.Equal(new[] { "enhancement" }, enhancement.Specializations);
        Assert.True(enhancement.ExecutesThroughCurrentUnifiedEndpoint);
        Assert.True(enhancement.HasRotationPreview);
        Assert.True(enhancement.HasSharedCombatItemProcessor);
        Assert.False(enhancement.HasExecutableCustomRotation);
        Assert.False(enhancement.HasSeededBatchResults);
        Assert.False(enhancement.ExecutesArbitraryCrossTreeModeledTalents);
        Assert.Contains("target-armor", enhancement.EncounterControls);
        Assert.DoesNotContain("rotation-policy", enhancement.EncounterControls);
        Assert.Contains(enhancement.KnownRestrictions, x => x.Contains("15 unspent"));
        Assert.Contains(enhancement.KnownRestrictions, x => x.Contains("not fully modeled, optimized or current-client certified"));
        Assert.Contains(enhancement.KnownRestrictions, x => x.Contains("off-hand Windfury is unavailable"));
        Assert.IsType<BadRequestObjectResult>(controller.Capability("shaman", "restoration"));
        Assert.IsType<BadRequestObjectResult>(controller.Capability("mage", "enhancement"));
        var discovery = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(controller.Capabilities()).Value, Web);
        // Other classes may add their own bounded overrides; Shaman still has
        // exactly its original Enhancement override and unchanged Elemental default.
        var discovered = Assert.Single(discovery.GetProperty("specializationOverrides").EnumerateArray(),
            x => x.GetProperty("classKey").GetString() == "shaman");
        Assert.Equal("enhancement", discovered.GetProperty("specializations")[0].GetString());
    }
}
