using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175FeralHistoricalStrictDispatch
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private const string SourceCommit = "1cc5ae1e9abdbf941b7326d2d90421822dce31ba";
    private const string SourceUrl = "https://github.com/gunba/wow-forever-sim/blob/" + SourceCommit + "/ui/core/talents/trees/druid.json";

    // Independently transcribed retained source metadata. Positions are one-based;
    // full-parent ranks are the disclosed local convention, not sourced rank requirements.
    private static readonly (string Key, string Tree, int Cap, int Row, int Column, string? Parent, int ParentRank)[] Expected = [
        ("ferocity", "feral", 5, 1, 2, null, 0),
        ("heart-of-the-wild", "feral", 5, 1, 3, null, 0),
        ("feral-swiftness", "feral", 2, 2, 1, null, 0),
        ("shredding-attacks", "feral", 3, 3, 1, null, 0),
        ("savage-fury", "feral", 2, 3, 2, null, 0),
        ("sharpened-claws", "feral", 2, 3, 4, null, 0),
        ("shifting-power", "feral", 1, 4, 1, "shredding-attacks", 3),
        ("predatory-strikes", "feral", 3, 4, 3, null, 0),
        ("blood-frenzy", "feral", 2, 4, 4, "sharpened-claws", 2),
        ("improved-shifting-power", "feral", 2, 5, 1, "shifting-power", 1),
        ("leader-of-the-pack", "feral", 1, 5, 2, null, 0),
        ("natural-instinct", "feral", 2, 5, 4, null, 0),
        ("berserk", "feral", 1, 7, 2, "leader-of-the-pack", 1),
        ("furor", "restoration", 5, 1, 3, null, 0)
    ];

    private static Dictionary<string,int> Candidate() => new(StringComparer.Ordinal) {
        ["ferocity"] = 5, ["heart-of-the-wild"] = 5, ["feral-swiftness"] = 2,
        ["shredding-attacks"] = 3, ["savage-fury"] = 2, ["sharpened-claws"] = 2,
        ["shifting-power"] = 1, ["predatory-strikes"] = 3, ["blood-frenzy"] = 2,
        ["improved-shifting-power"] = 2, ["leader-of-the-pack"] = 1,
        ["natural-instinct"] = 2, ["berserk"] = 1, ["furor"] = 5
    };

    private static FeralHistoricalTalentInput TalentInput(Dictionary<string,int> ranks, bool totals = true) => new() {
        Ranks = ranks,
        BalancePointsSpent = totals ? 0 : null,
        FeralPointsSpent = totals ? ranks.Where(x => x.Key != "furor").Sum(x => x.Value) : null,
        RestorationPointsSpent = totals ? ranks.GetValueOrDefault("furor") : null,
        TotalPoints = totals ? ranks.Values.Sum() : null,
        UnspentPoints = totals ? 51 - ranks.Values.Sum() : null
    };

    private static ForeverLevel60SimulationRequest Request(Dictionary<string,int>? ranks = null)
    {
        ranks ??= Candidate();
        return new() {
            ClassKey = "druid", Specialization = "feral", ItemizedTalentMode = true,
            ItemizedTalentRanks = new(ranks, ranks.Comparer),
            Feral = new() {
                Character = new() { Race = "night-elf" }, UseProvisionalLevel60Baseline = true,
                ManualInputs = new() { PreTalentCatAttackPower = 800m },
                Talents = TalentInput(new(ranks, ranks.Comparer)),
                DurationSeconds = 12m, CaptureTimeline = true, Seed = 175615
            }
        };
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, Web);
    private static JsonElement Ok(IActionResult result) => Json(Assert.IsType<OkObjectResult>(result).Value!);
    private static IActionResult Validate(object value) =>
        new ForeverSoloTalentController().ValidateItemized("druid", "feral", Json(value));
    private static IActionResult Raw(string value, bool legacyRoute = false)
    {
        using var data = JsonDocument.Parse(value);
        var c = new ForeverSoloTalentController();
        return legacyRoute ? c.Validate("druid", "feral", data.RootElement) :
            c.ValidateItemized("druid", "feral", data.RootElement);
    }
    private static string Fingerprint(object value)
    {
        var ids = new Dictionary<string,string>();
        return Regex.Replace(JsonSerializer.Serialize(value, Web), "[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", m => {
            if (!ids.TryGetValue(m.Value, out var id)) ids[m.Value] = id = $"ENGINE-ID-{ids.Count}";
            return id;
        });
    }

    [Fact]
    public void CatalogIsExactlyFourteenSourceMatchedKeysWithActualPositionsAndParents()
    {
        var actual = ForeverHistoricalCasterTalentCatalog.ForSpecialization("druid", "feral");
        Assert.Equal(14, actual.Count);
        Assert.True(ForeverHistoricalCasterTalentCatalog.Supports("druid", "feral"));
        Assert.Equal(SourceCommit, ForeverHistoricalCasterTalentCatalog.SourceCommit);
        Assert.Equal(SourceUrl, ForeverHistoricalCasterTalentCatalog.SourceUrl("druid"));
        Assert.Contains("not-current-client-certified", ForeverHistoricalCasterTalentCatalog.SourceStatus);
        foreach (var expected in Expected) {
            var entry = Assert.Single(actual, x => x.Key == expected.Key);
            Assert.Equal("druid", entry.ClassKey); Assert.Equal(expected.Tree, entry.Tree);
            Assert.Equal(expected.Cap, entry.MaxRank); Assert.Equal(expected.Row, entry.Row);
            Assert.Equal(expected.Column, entry.Column); Assert.Equal(expected.Parent, entry.PrerequisiteKey);
            Assert.Equal(expected.ParentRank, entry.PrerequisiteRank);
        }
        var rows = Ok(new ForeverSoloTalentController().Catalog("druid", "feral")).GetProperty("talents").EnumerateArray().ToArray();
        Assert.Equal(14, rows.Length);
        foreach (var expected in Expected) {
            var row = Assert.Single(rows, x => x.GetProperty("key").GetString() == expected.Key);
            Assert.Equal(expected.Tree, row.GetProperty("tree").GetString());
            Assert.Equal(expected.Cap, row.GetProperty("maxRank").GetInt32());
            Assert.Equal(expected.Row, row.GetProperty("row").GetInt32());
            Assert.Equal(expected.Column, row.GetProperty("column").GetInt32());
            Assert.Equal((expected.Row - 1) * 5, row.GetProperty("requiredEarlierPoints").GetInt32());
            Assert.Equal(expected.Parent, row.GetProperty("enginePrerequisiteKey").GetString());
            Assert.Equal(expected.ParentRank, row.GetProperty("enginePrerequisiteRank").GetInt32());
            Assert.Equal(expected.Key is "feral-swiftness" or "furor" ? "inactive-in-fixed-cat" : "fixed-cat-partial",
                row.GetProperty("simulationStatus").GetString());
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("knownLimitations").GetString()));
            Assert.Equal(SourceUrl, row.GetProperty("sourceUrl").GetString());
        }
        new ForeverFeralDruidTalents { Ranks = Candidate() }.Validate();
    }

    [Fact]
    public void CandidateUsesThirtySixRealPointsAndEveryEarlierRowBudgetIsIndependent()
    {
        var ranks = Candidate(); var result = Ok(Validate(TalentInput(ranks)));
        Assert.True(result.GetProperty("valid").GetBoolean());
        Assert.Equal(36, result.GetProperty("pointsSpent").GetInt32());
        Assert.Equal(15, result.GetProperty("remaining").GetInt32());
        Assert.Equal(31, result.GetProperty("treePoints").GetProperty("feral").GetInt32());
        Assert.Equal(5, result.GetProperty("treePoints").GetProperty("restoration").GetInt32());
        Assert.Empty(result.GetProperty("unmodeledSelections").EnumerateArray());
        foreach (var (row, points) in new[] { (2,10), (3,12), (4,19), (5,25), (7,30) })
            Assert.Equal(points, Expected.Where(x => x.Tree == "feral" && x.Row < row).Sum(x => ranks[x.Key]));
        var genericRoute = new ForeverSoloTalentController().Validate("druid", "feral", Json(TalentInput(ranks)));
        Assert.Equal(result.GetProperty("pointsSpent").GetInt32(), Ok(genericRoute).GetProperty("pointsSpent").GetInt32());
    }

    [Theory][InlineData(0,0,51)][InlineData(1,5,46)][InlineData(2,31,20)][InlineData(3,35,16)]
    public async Task EmptyAndLegalPartialRemovalsRemainHonestUnspentAllocations(int kind, int spent, int unspent)
    {
        var ranks = Candidate();
        if (kind == 0) ranks.Clear();
        if (kind == 1) ranks = new() { ["ferocity"] = 5 };
        if (kind == 2) ranks.Remove("furor");
        if (kind == 3) ranks.Remove("berserk");
        var valid = Ok(Validate(TalentInput(ranks)));
        Assert.Equal(spent, valid.GetProperty("pointsSpent").GetInt32());
        Assert.Equal(unspent, valid.GetProperty("remaining").GetInt32());
        var result = Ok(await new ForeverLevel60SimulationController().Simulate(Request(ranks), default));
        Assert.Equal(spent, result.GetProperty("allocatedTalentPoints").GetInt32());
        Assert.Equal(unspent, result.GetProperty("unspentTalentPoints").GetInt32());
    }

    [Theory][InlineData(0)][InlineData(5)]
    public void FurorZeroAndFiveAreAcceptedWithoutChangingHistoricalCap(int rank)
    {
        var ranks = Candidate(); ranks["furor"] = rank;
        Assert.IsType<OkObjectResult>(Validate(TalentInput(ranks)));
        Assert.Equal(5, ForeverHistoricalCasterTalentCatalog.Find("druid", "feral", "furor")!.MaxRank);
    }

    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public async Task ConservativeFixedCatGateRejectsUnmodeledLowerFurorRanks(int rank)
    {
        var ranks = Candidate(); ranks["furor"] = rank;
        Assert.IsType<BadRequestObjectResult>(Validate(TalentInput(ranks)));
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(Request(ranks), default));
    }

    public static IEnumerable<object[]> InvalidRanks() => Expected.SelectMany(x => new[] {
        new object[] { x.Key, -1 }, new object[] { x.Key, x.Cap + 1 }
    });
    [Theory][MemberData(nameof(InvalidRanks))]
    public void EverySelectedRankUsesTheActualRuntimeCap(string key, int rank)
    {
        var ranks = Candidate(); ranks[key] = rank;
        Assert.IsType<BadRequestObjectResult>(Validate(TalentInput(ranks, false)));
        Assert.Throws<ArgumentException>(() => new ForeverFeralDruidTalents { Ranks = ranks }.Validate());
    }

    [Theory][InlineData("rend-and-tear")][InlineData("natural-reaction")][InlineData("primal-bite")]
    [InlineData("feral-instinct")][InlineData("thick-hide")][InlineData("feral-charge")][InlineData("brutal-impact")]
    [InlineData("naturalist")][InlineData("genesis")][InlineData("improved-wrath")]
    public async Task NoExtraRuntimeOrHistoricalFillersAreAdmittedEvenAtZero(string key)
    {
        foreach (var rank in new[] { 0, 1 }) {
            var ranks = Candidate(); ranks[key] = rank;
            Assert.IsType<BadRequestObjectResult>(Validate(TalentInput(ranks, false)));
            Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(Request(ranks), default));
        }
        Assert.Null(ForeverHistoricalCasterTalentCatalog.Find("druid", "feral", key));
    }

    [Fact]
    public void RealEarlierRowsCannotBeReplacedWithSameRowOtherTreeOrSyntheticPoints()
    {
        var sameRow = new Dictionary<string,int> {
            ["ferocity"] = 5, ["feral-swiftness"] = 2,
            ["shredding-attacks"] = 3, ["savage-fury"] = 2, ["sharpened-claws"] = 2
        };
        var error = Json(Assert.IsType<BadRequestObjectResult>(Validate(TalentInput(sameRow))).Value!);
        Assert.Contains("earlier feral rows, found 7", error.GetProperty("error").GetString());
        var otherTree = new Dictionary<string,int> { ["ferocity"] = 5, ["furor"] = 5, ["shredding-attacks"] = 3 };
        Assert.IsType<BadRequestObjectResult>(Validate(TalentInput(otherTree)));
        Assert.IsType<BadRequestObjectResult>(Raw("{\"ranks\":{\"berserk\":1,\"leader-of-the-pack\":1},\"feralPointsSpent\":31}"));
        var missingEarlier = Candidate(); missingEarlier["ferocity"] = 4;
        var berserkError = Json(Assert.IsType<BadRequestObjectResult>(Validate(TalentInput(missingEarlier))).Value!);
        Assert.Contains("berserk needs 30 points in earlier feral rows, found 29", berserkError.GetProperty("error").GetString());
    }

    [Theory][InlineData("shifting-power", "shredding-attacks", 2)]
    [InlineData("blood-frenzy", "sharpened-claws", 1)]
    [InlineData("improved-shifting-power", "shifting-power", 0)]
    public void FullParentConventionIsEnforcedWhenEarlierRowsOtherwiseHaveEnoughPoints(string child, string parent, int rank)
    {
        var ranks = Candidate(); ranks.Remove("berserk"); ranks[parent] = rank;
        var error = Json(Assert.IsType<BadRequestObjectResult>(Validate(TalentInput(ranks))).Value!);
        Assert.Contains(child, error.GetProperty("error").GetString());
        Assert.Contains(parent, error.GetProperty("error").GetString());
        Assert.Contains("requires", error.GetProperty("error").GetString());
    }

    [Theory][InlineData("balancePointsSpent",1)][InlineData("feralPointsSpent",30)]
    [InlineData("restorationPointsSpent",4)][InlineData("totalPoints",51)][InlineData("unspentPoints",0)]
    public void SuppliedNamedTotalsMustEqualTheActualRankMap(string field, int incorrect)
    {
        var data = JsonNode.Parse(JsonSerializer.Serialize(TalentInput(Candidate()), Web))!.AsObject();
        data[field] = incorrect;
        Assert.IsType<BadRequestObjectResult>(Raw(data.ToJsonString()));
    }

    [Theory][InlineData("balancePointsSpent")][InlineData("feralPointsSpent")]
    [InlineData("restorationPointsSpent")][InlineData("totalPoints")][InlineData("unspentPoints")]
    public void OptionalTotalsMayBeOmittedButExplicitNullAndMalformedValuesAreRejected(string field)
    {
        var data = JsonNode.Parse(JsonSerializer.Serialize(TalentInput(Candidate()), Web))!.AsObject();
        data.Remove(field); Assert.IsType<OkObjectResult>(Raw(data.ToJsonString()));
        foreach (var bad in new[] { "null", "\"31\"", "1.5", "{}", "[]", "true", "-1", "2147483648" }) {
            data[field] = JsonNode.Parse(bad);
            Assert.IsType<BadRequestObjectResult>(Raw(data.ToJsonString()));
        }
        Assert.IsType<OkObjectResult>(Validate(TalentInput(Candidate(), false)));
    }

    public static IEnumerable<object[]> DeclaredTotals() => new object[][] {
        new object[] { "balancePointsSpent", 0 }, new object[] { "feralPointsSpent", 31 },
        new object[] { "restorationPointsSpent", 5 }, new object[] { "totalPoints", 36 },
        new object[] { "unspentPoints", 15 }
    };

    [Theory][MemberData(nameof(DeclaredTotals))]
    public void TypedBindingPreservesPresentIntegerTotalsAndOmittedOptionalDeclarations(string field, int expected)
    {
        var data = JsonNode.Parse(JsonSerializer.Serialize(Request(), Web))!.AsObject();
        var talents = data["feral"]!["talents"]!.AsObject();
        talents.Remove(field);
        var absent = JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(data.ToJsonString(), Web)!;
        Assert.False(Json(absent.Feral!.Talents).TryGetProperty(field, out _));
        Assert.IsType<OkObjectResult>(Validate(absent.Feral.Talents));
        talents[field] = expected;
        var present = JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(data.ToJsonString(), Web)!;
        Assert.Equal(expected, Json(present.Feral!.Talents).GetProperty(field).GetInt32());
        Assert.IsType<OkObjectResult>(Validate(present.Feral.Talents));
    }

    [Fact]
    public void AllOptionalTotalsMayBeAbsentThroughActualTypedJsonRoundTrip()
    {
        var r = Request(); r.Feral!.Talents = TalentInput(Candidate(), false);
        var rebound = JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(JsonSerializer.Serialize(r, Web), Web)!;
        var talents = Json(rebound.Feral!.Talents);
        Assert.Equal(new[] { "ranks" }, talents.EnumerateObject().Select(x => x.Name));
        Assert.IsType<OkObjectResult>(Validate(rebound.Feral.Talents));
    }

    public static IEnumerable<object[]> MalformedDeclaredTotals()
    {
        foreach (var declaration in DeclaredTotals())
            foreach (var bad in new[] { "null", $"\"{declaration[1]}\"", "1.5", "true" })
                yield return new[] { declaration[0], bad };
    }

    [Theory][MemberData(nameof(MalformedDeclaredTotals))]
    public void TypedBindingCannotEraseNullOrCoerceMalformedDeclaredTotals(string field, string rawValue)
    {
        var data = JsonNode.Parse(JsonSerializer.Serialize(Request(), Web))!.AsObject();
        var talents = data["feral"]!["talents"]!.AsObject();
        talents[field] = JsonNode.Parse(rawValue);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FeralHistoricalTalentInput>(talents.ToJsonString(), Web));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(data.ToJsonString(), Web));
    }

    [Theory]
    [InlineData("character", "level", "60")]
    [InlineData("manualInputs", "preTalentCatAttackPower", "800")]
    [InlineData("manualInputs", "weaponDps", "75")]
    [InlineData("manualInputs", "meleeHitPercent", "88")]
    [InlineData("manualInputs", "meleeCritPercent", "20")]
    [InlineData("manualInputs", "shapeshiftMana", "250")]
    [InlineData("manualInputs", "shiftingPowerBaseMana", "1244")]
    [InlineData("manualInputs", "shiftingPowerCostReductionPercent", "0")]
    [InlineData("", "seed", "175615")][InlineData("", "durationSeconds", "12")]
    [InlineData("", "targetLevel", "63")][InlineData("", "targetInitialHealthPercent", "100")]
    [InlineData("", "targetMaximumHealth", "1000000")][InlineData("", "targetArmor", "0")]
    [InlineData("talents.ranks", "ferocity", "5")]
    public void FeralTypedNumericInputsRejectStringsDespiteWebSerializerDefaults(string container, string key, string value)
    {
        var data = JsonNode.Parse(JsonSerializer.Serialize(Request(), Web))!.AsObject();
        var feral = data["feral"]!.AsObject();
        var target = container.Length == 0 ? feral :
            container == "talents.ranks" ? feral["talents"]!["ranks"]!.AsObject() : feral[container]!.AsObject();
        target[key] = JsonValue.Create(value);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FeralHistoricalSimRequest>(feral.ToJsonString(), Web));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(data.ToJsonString(), Web));
    }

    [Theory]
    [InlineData("{\"ranks\":{\"ferocity\":0,\"ferocity\":0}}")]
    [InlineData("{\"ranks\":{\"ferocity\":5,\"ferocity\":0}}")]
    [InlineData("{\"ranks\":{\"furor\":5,\"furor\":5}}")]
    [InlineData("{\"ranks\":{\"Ferocity\":0}}")]
    [InlineData("{\"ranks\":{\" ferocity\":0}}")]
    [InlineData("{\"ranks\":{\"ferocity \":0}}")]
    [InlineData("{\"ranks\":{\"primalFury\":0}}")]
    [InlineData("{\"ranks\":{\"predatoryInstincts\":0}}")]
    [InlineData("{\"ranks\":{\"unknown\":0}}")]
    [InlineData("{\"ranks\":{\"ferocity\":null}}")]
    [InlineData("{\"ranks\":{\"ferocity\":1.5}}")]
    [InlineData("{\"ranks\":{\"ferocity\":\"5\"}}")]
    [InlineData("{\"ranks\":null}")][InlineData("{\"ranks\":[]}")]
    [InlineData("{}")] [InlineData("null")]
    [InlineData("{\"ranks\":{},\"syntheticFiller\":0}")]
    [InlineData("{\"ranks\":{},\"ranks\":{}}")]
    [InlineData("{\"ranks\":{},\"totalPoints\":0,\"totalPoints\":0}")]
    [InlineData("{\"ranks\":{},\"TotalPoints\":0}")]
    [InlineData("{\"ranks\":{},\"combatPointsSpent\":0}")]
    public void BothTalentRoutesRejectRawDuplicateAliasMalformedAndUnknownInput(string raw)
    {
        Assert.IsType<BadRequestObjectResult>(Raw(raw));
        Assert.IsType<BadRequestObjectResult>(Raw(raw, true));
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public async Task FullAndTypedMapsRequireExactKeyPresenceIncludingExplicitZeros(int kind)
    {
        var r = Request(new() { ["ferocity"] = 5 });
        if (kind == 0) r.ItemizedTalentRanks!["furor"] = 0;
        if (kind == 1) r.Feral!.Talents.Ranks["furor"] = 0;
        if (kind == 2) r.ItemizedTalentRanks!["ferocity"] = 4;
        if (kind == 3) r.Feral!.Talents.Ranks["ferocity"] = 4;
        if (kind == 4) r.ItemizedTalentRanks!.Clear();
        if (kind == 5) r.Feral!.Talents.Ranks.Clear();
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(r, default));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task MatchedExplicitZeroKeysAndDictionaryComparersArePreserved(bool insensitive)
    {
        var comparer = insensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var r = Request(new(comparer) { ["furor"] = 0, ["ferocity"] = 5, ["heart-of-the-wild"] = 0 });
        var before = JsonSerializer.Serialize(r, Web);
        var result = Ok(await new ForeverLevel60SimulationController().Simulate(r, default));
        Assert.Equal(5, result.GetProperty("allocatedTalentPoints").GetInt32());
        var returned = result.GetProperty("preparation").GetProperty("request").GetProperty("talents").GetProperty("ranks");
        Assert.Equal(new[] { "furor", "ferocity", "heart-of-the-wild" }, returned.EnumerateObject().Select(x => x.Name));
        Assert.Equal(0, returned.GetProperty("furor").GetInt32());
        Assert.Same(comparer, r.Feral!.Talents.Ranks.Comparer); Assert.Same(comparer, r.ItemizedTalentRanks!.Comparer);
        Assert.Equal(before, JsonSerializer.Serialize(r, Web));
    }

    [Theory][InlineData("Ferocity")][InlineData("ferocity ")][InlineData(" ferocity")][InlineData("unknown")]
    public async Task TypedBindingCannotNormalizeCanonicalKeyAliasesEvenAtZero(string key)
    {
        var ranks = new Dictionary<string,int>(StringComparer.Ordinal) { [key] = 0 };
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(Request(ranks), default));
    }

    [Theory][InlineData("mage")][InlineData("warlock")][InlineData("marksmanship")]
    [InlineData("survival")][InlineData("beastMastery")][InlineData("balance")]
    [InlineData("elemental")][InlineData("enhancement")][InlineData("shadow")][InlineData("rogue")]
    public async Task EveryOtherKnownTypedPayloadConflictsWithFeral(string other)
    {
        var data = JsonNode.Parse(JsonSerializer.Serialize(Request(), Web))!.AsObject();
        data[other] = new JsonObject();
        var request = JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(data.ToJsonString(), Web)!;
        var result = Json(Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(request, default)).Value!);
        Assert.Contains("exactly one", result.GetProperty("error").GetString());
    }

    [Theory][InlineData("equipment")][InlineData("modifiers")][InlineData("combatItems")]
    [InlineData("rotationPolicy")][InlineData("customPriority")][InlineData("iterations")]
    [InlineData("encounter")][InlineData("useRacialActive")][InlineData("unknown")]
    public async Task UnknownOuterInputsCannotBeSilentlyIgnoredForFeral(string field)
    {
        var data = JsonNode.Parse(JsonSerializer.Serialize(Request(), Web))!.AsObject(); data[field] = 0;
        var r = JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(data.ToJsonString(), Web)!;
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(r, default));
    }

    public static IEnumerable<object[]> InvalidDispatch() => Enumerable.Range(0, 24).Select(i => new object[] { i });
    [Theory][MemberData(nameof(InvalidDispatch))]
    public async Task CanonicalIdentityBaselineAndRequiredMapGatesRejectWithoutMutatingTheCaller(int kind)
    {
        var r = Request();
        switch (kind) {
            case 0: r = null!; break;
            case 1: r.Feral = null; break;
            case 2: r.ItemizedTalentMode = false; break;
            case 3: r.ItemizedTalentRanks = null; break;
            case 4: r.ClassKey = "Druid"; break;
            case 5: r.ClassKey = "druid "; break;
            case 6: r.Specialization = "Feral"; break;
            case 7: r.Specialization = "feral "; break;
            case 8: r.Specialization = "balance"; break;
            case 9: r.Feral!.Character = null!; break;
            case 10: r.Feral!.Character.ClassKey = "Druid"; break;
            case 11: r.Feral!.Character.SpecializationKey = "balance"; break;
            case 12: r.Feral!.Character.Level = 59; break;
            case 13: r.Feral!.Character.Race = "human"; break;
            case 14: r.Feral!.Role = "bear"; break;
            case 15: r.Feral!.UseProvisionalLevel60Baseline = false; break;
            case 16: r.Feral!.UseProvisionalLevel60Baseline = false; r.Feral.UseReviewedLevel60Baseline = true; break;
            case 17: r.Feral!.Level60Baseline = new() { ClassKey = "druid", Race = "night-elf" }; break;
            case 18: r.Feral!.ManualInputs.PreTalentCatAttackPower = null; break;
            case 19: r.Feral!.Talents = null!; break;
            case 20: r.Feral!.Talents.Ranks = null!; break;
            case 21: r.Feral!.Talents.FeralPointsSpent = 34; break;
            case 22: r.Feral!.Talents.TotalPoints = 51; break;
            case 23: r.Feral!.Talents.UnspentPoints = 0; break;
        }
        var before = JsonSerializer.Serialize(r, Web);
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(r, default));
        Assert.Equal(before, JsonSerializer.Serialize(r, Web));
    }

    [Theory][InlineData("preTalentCatAttackPower")][InlineData("weaponDps")][InlineData("targetArmor")]
    public async Task RepresentableJsonNumbersThatOverflowSimulationArithmeticReturnActionableBadRequest(string field)
    {
        var data = JsonNode.Parse(JsonSerializer.Serialize(Request(), Web))!.AsObject();
        var feral = data["feral"]!.AsObject();
        var target = field == "targetArmor" ? feral : feral["manualInputs"]!.AsObject();
        target[field] = JsonValue.Create(decimal.MaxValue);
        var request = JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(data.ToJsonString(), Web)!;
        var rebound = field switch {
            "preTalentCatAttackPower" => request.Feral!.ManualInputs.PreTalentCatAttackPower!.Value,
            "weaponDps" => request.Feral!.ManualInputs.WeaponDps,
            _ => request.Feral!.TargetArmor
        };
        Assert.Equal(decimal.MaxValue, rebound);
        var before = JsonSerializer.Serialize(request, Web);
        var result = Json(Assert.IsType<BadRequestObjectResult>(
            await new ForeverLevel60SimulationController().Simulate(request, default)).Value!);
        Assert.Contains("Historical Feral numeric inputs exceed representable simulation arithmetic", result.GetProperty("error").GetString());
        Assert.Equal(before, JsonSerializer.Serialize(request, Web));
    }

    [Fact]
    public async Task StrictControllerReturnsTheSameOwnedPreparationAndCombatAsTheAdapter()
    {
        var r = Request(); var before = JsonSerializer.Serialize(r, Web);
        var prepared = FeralHistoricalInputAdapter.Run(r.Feral!);
        var result = Ok(await new ForeverLevel60SimulationController().Simulate(r, default));
        Assert.Equal("druid", result.GetProperty("classKey").GetString());
        Assert.Equal("feral", result.GetProperty("specialization").GetString()); Assert.Equal(60, result.GetProperty("level").GetInt32());
        Assert.Equal(SourceCommit, result.GetProperty("talentSourceCommit").GetString());
        Assert.Equal(SourceUrl, result.GetProperty("talentSourceUrl").GetString());
        Assert.Contains("not-current-client-certified", result.GetProperty("talentSourceStatus").GetString());
        Assert.NotEmpty(result.GetProperty("talentModelingNotes").EnumerateArray());
        Assert.Equal(36, result.GetProperty("allocatedTalentPoints").GetInt32());
        Assert.Equal(15, result.GetProperty("unspentTalentPoints").GetInt32());
        Assert.Equal(Fingerprint(prepared.Simulation), Fingerprint(result.GetProperty("sim")));
        Assert.Equal(Fingerprint(prepared.Preparation), Fingerprint(result.GetProperty("preparation")));
        var stats = result.GetProperty("preparation").GetProperty("preview");
        Assert.Equal(902.4m, stats.GetProperty("spellbookAttackPower").GetDecimal());
        Assert.Equal(2173m, stats.GetProperty("maximumHealth").GetDecimal());
        Assert.Equal(2594m, stats.GetProperty("maximumMana").GetDecimal());
        Assert.Equal(2344m, stats.GetProperty("manaAfterInitialShift").GetDecimal());
        Assert.False(result.GetProperty("baseline").GetProperty("includesRacialPassives").GetBoolean());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("baseline").GetProperty("nakedMeleeAttackPower").ValueKind);
        var direct = new FeralDruidSimRunner().Run(prepared.Preparation.DirectRequest);
        Assert.Equal(Fingerprint(direct), Fingerprint(result.GetProperty("sim")));
        Assert.Equal(before, JsonSerializer.Serialize(r, Web));
    }

    [Fact]
    public async Task RuntimeCaveatsReachPreparationTalentReceiptAndCapabilitiesWithoutCertifyingEligibility()
    {
        var result = Ok(await new ForeverLevel60SimulationController().Simulate(Request(), default));
        var preparationNotes = result.GetProperty("preparation").GetProperty("modelingNotes")
            .EnumerateArray().Select(x => x.GetString()!).ToArray();
        var talentNotes = result.GetProperty("talentModelingNotes")
            .EnumerateArray().Select(x => x.GetString()!).ToArray();
        var capabilityNotes = ForeverClassSimulationCapabilities.Get("druid", "feral").KnownRestrictions;
        static void AssertCaveats(IEnumerable<string> notes)
        {
            var text = string.Join(" ", notes);
            foreach (var phrase in new[] { "Omen", "6%", "10-second", "without a selectable Omen rank",
                "baseline", "talent", "unresolved", "absence from", "does not settle",
                "Natural Instinct", "white/periodic", "provisional", "no healing model" })
                Assert.Contains(phrase, text, StringComparison.OrdinalIgnoreCase);
        }
        AssertCaveats(preparationNotes); AssertCaveats(talentNotes); AssertCaveats(capabilityNotes);
        AssertCaveats(ForeverHistoricalCasterTalentCatalog.FeralModelingNotes);
        Assert.Contains("not-current-client-certified", result.GetProperty("talentSourceStatus").GetString());
        Assert.Contains("does not certify complete source effects", string.Join(" ", preparationNotes));
        Assert.Contains("historical metadata is not current-client certification", string.Join(" ", talentNotes));
        Assert.Contains("complete talent effects are not certified", string.Join(" ", capabilityNotes));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task InputTimingReceiptReflectsAmbientRuntimeAndMatchingDirectReplay(bool scopedLatency)
    {
        var previous = SimulationInputTimingScope.AdditionalDelaySeconds;
        try {
            // Normal shared HTTP execution has no added latency. A direct C# caller
            // can still carry the same ambient 80 ms used by legacy /api/sim/ HTTP.
            SimulationInputTimingScope.AdditionalDelaySeconds = scopedLatency ? .08m : null;
            var r = Request();
            var prepared = FeralHistoricalInputAdapter.Run(r.Feral!);
            var actual = Ok(await new ForeverLevel60SimulationController().Simulate(r, default));
            var timing = actual.GetProperty("inputTiming");
            Assert.Equal(scopedLatency ? 80m : 0m, timing.GetProperty("additionalInputLatencyMilliseconds").GetDecimal());
            Assert.Contains("shared level-60 HTTP route adds zero", timing.GetProperty("note").GetString());
            Assert.Contains("Legacy /api/sim/ HTTP defaults to 80 ms", timing.GetProperty("note").GetString());
            Assert.Contains("inputLatencyMs matching this receipt", timing.GetProperty("note").GetString());
            Assert.Equal(Fingerprint(prepared.Simulation), Fingerprint(actual.GetProperty("sim")));
            Assert.Equal(Fingerprint(new FeralDruidSimRunner().Run(prepared.Preparation.DirectRequest)),
                Fingerprint(actual.GetProperty("sim")));
            Assert.Equal(scopedLatency ? .08m : (decimal?)null, SimulationInputTimingScope.AdditionalDelaySeconds);
        }
        finally {
            SimulationInputTimingScope.AdditionalDelaySeconds = previous;
        }
        Assert.Equal(previous, SimulationInputTimingScope.AdditionalDelaySeconds);
    }

    [Fact]
    public async Task JsonBoundAndConcurrentDispatchHaveStableResultsAndDoNotMutateTheInput()
    {
        var r = JsonSerializer.Deserialize<ForeverLevel60SimulationRequest>(JsonSerializer.Serialize(Request(), Web), Web)!;
        var before = JsonSerializer.Serialize(r, Web); var c = new ForeverLevel60SimulationController();
        var expected = Ok(await c.Simulate(r, default));
        await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ => {
            var actual = Ok(await new ForeverLevel60SimulationController().Simulate(r, default));
            Assert.Equal(Fingerprint(expected.GetProperty("sim")), Fingerprint(actual.GetProperty("sim")));
            Assert.Equal(Fingerprint(expected.GetProperty("preparation")), Fingerprint(actual.GetProperty("preparation")));
        }));
        Assert.Equal(before, JsonSerializer.Serialize(r, Web));
    }

    [Fact]
    public async Task CallerChangesAfterDispatchCannotChangeTheAlreadyValidatedExecution()
    {
        var r = Request();
        var expected = FeralHistoricalInputAdapter.Run(r.Feral!);
        var pending = new ForeverLevel60SimulationController().Simulate(r, default);
        // Simulate owns its input before its first await. Do not require a timing-dependent
        // IsCompleted assertion: this checks returned ownership whether the job is pending or done.
        r.ItemizedTalentRanks!.Clear(); r.Feral!.Talents.Ranks.Clear();
        r.Feral.ManualInputs.PreTalentCatAttackPower = 0m; r.Feral.Character.Race = "tauren";
        r.Feral.DurationSeconds = 1m; r.Feral.TargetArmor = 99999m; r.ClassKey = "rogue";
        var actual = Ok(await pending);
        Assert.Equal(36, actual.GetProperty("allocatedTalentPoints").GetInt32());
        Assert.Equal(Fingerprint(expected.Simulation), Fingerprint(actual.GetProperty("sim")));
        Assert.Equal(Fingerprint(expected.Preparation), Fingerprint(actual.GetProperty("preparation")));
        Assert.Empty(r.ItemizedTalentRanks); Assert.Empty(r.Feral.Talents.Ranks);
        Assert.Equal("tauren", r.Feral.Character.Race); // No delayed write back into caller data.
    }

    [Fact]
    public void FeralPresetAndPreparationDoNotExpandClassOnlyBalanceOrHistoricalFillers()
    {
        var c = new ForeverSoloTalentController();
        Assert.Empty(Ok(c.ItemizedCatalog("druid", "feral")).GetProperty("unmodeled").EnumerateArray());
        var preset=Assert.IsType<ForeverItemizedPresetCatalog.Preset>(Assert.IsType<OkObjectResult>(c.DefaultItemized("druid", "feral")).Value);
        Assert.Equal(Candidate().OrderBy(x=>x.Key),preset.Ranks.OrderBy(x=>x.Key));
        Assert.Equal(36,preset.TotalRankedPoints); Assert.Equal(15,preset.UnspentPoints);
        Assert.IsType<BadRequestObjectResult>(new ForeverSoloRotationController().Preview(Request()));
        var feral = ForeverClassSimulationCapabilities.Get("druid", "feral");
        Assert.Equal(new[] { "feral" }, feral.Specializations);
        Assert.True(feral.ExecutesThroughCurrentUnifiedEndpoint); Assert.True(feral.HasClassTalentValidation);
        Assert.False(feral.ExecutesArbitraryCrossTreeModeledTalents); Assert.False(feral.HasSharedCombatItemProcessor);
        Assert.False(feral.HasRotationPreview); Assert.False(feral.HasExecutableCustomRotation); Assert.False(feral.HasSeededBatchResults);
        Assert.Contains(ForeverClassSimulationCapabilities.SpecializationOverrides, x => ReferenceEquals(x, feral));
        Assert.Equal(new[] { "druid/feral", "shaman/enhancement" },
            ForeverClassSimulationCapabilities.SpecializationOverrides
                .SelectMany(x => x.Specializations.Select(spec => $"{x.ClassKey}/{spec}"))
                .OrderBy(x => x, StringComparer.Ordinal).ToArray());
        var balance = ForeverClassSimulationCapabilities.Get("druid");
        Assert.Same(balance, ForeverClassSimulationCapabilities.Get("druid", "balance"));
        Assert.Equal(new[] { "balance" }, balance.Specializations); Assert.True(balance.HasRotationPreview);
        Assert.Equal(19, ForeverHistoricalCasterTalentCatalog.ForClass("druid").Count);
        Assert.Equal(JsonSerializer.Serialize(ForeverHistoricalCasterTalentCatalog.ForClass("druid"), Web),
            JsonSerializer.Serialize(ForeverHistoricalCasterTalentCatalog.ForSpecialization("druid", "balance"), Web));
        Assert.Null(ForeverHistoricalCasterTalentCatalog.Find("druid", "ferocity"));
        Assert.Equal(26, ForeverHistoricalCasterTalentCatalog.ForClass("shaman").Count);
        Assert.Equal(new[] { "elemental" }, ForeverClassSimulationCapabilities.Get("shaman").Specializations);
        Assert.Equal(new[] { "enhancement" }, ForeverClassSimulationCapabilities.Get("shaman", "enhancement").Specializations);
        Assert.Throws<ArgumentException>(() => ForeverClassSimulationCapabilities.Get("druid", "restoration"));
    }

    [Fact]
    public async Task ExistingBalancePayloadStillRunsThroughItsUnchangedSpecialization()
    {
        var r = new ForeverLevel60SimulationRequest {
            ClassKey = "druid", Specialization = "balance", ItemizedTalentMode = true,
            ItemizedTalentRanks = new() { ["improved-wrath"] = 5 },
            Balance = new() { DurationSeconds = 2m, UseProvisionalLevel60Baseline = true,
                MajorManaPotionCharges = 0,
                Talents = new() { BalancePointsSpent = 5, FeralPointsSpent = 0, RestorationPointsSpent = 0,
                    Ranks = new() { ["improved-wrath"] = 5 } } }
        };
        r.Balance.Character.Race = "tauren";
        var before = JsonSerializer.Serialize(r, Web);
        Assert.IsType<OkObjectResult>(await new ForeverLevel60SimulationController().Simulate(r, default));
        Assert.Equal(before, JsonSerializer.Serialize(r, Web));
        r.Specialization = "feral";
        Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(r, default));
    }
}
