using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using WarcraftSim.Api.Controllers;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Strict preparation and real catalog gates, not observations or complete Feral certification.</summary>
public sealed class Cp175FeralPreviewEndpoints
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly (string Key, string Tree, int Cap, int Row, int Column, string? Parent)[] Source = [
        ("ferocity", "feral", 5, 1, 2, null), ("heart-of-the-wild", "feral", 5, 1, 3, null),
        ("feral-swiftness", "feral", 2, 2, 1, null), ("shredding-attacks", "feral", 3, 3, 1, null),
        ("savage-fury", "feral", 2, 3, 2, null), ("sharpened-claws", "feral", 2, 3, 4, null),
        ("shifting-power", "feral", 1, 4, 1, "shredding-attacks"), ("predatory-strikes", "feral", 3, 4, 3, null),
        ("blood-frenzy", "feral", 2, 4, 4, "sharpened-claws"), ("improved-shifting-power", "feral", 2, 5, 1, "shifting-power"),
        ("leader-of-the-pack", "feral", 1, 5, 2, null), ("natural-instinct", "feral", 2, 5, 4, null),
        ("berserk", "feral", 1, 7, 2, "leader-of-the-pack"), ("furor", "restoration", 5, 1, 3, null)
    ];
    private static string Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "WarcraftSim.slnx"))) return d.FullName;
        throw new DirectoryNotFoundException("Source checkout required for retained source receipt tests.");
    }
    private static string Json(object? value) => JsonSerializer.Serialize(value, Web);
    private static JsonElement Node(object? value) => JsonSerializer.SerializeToElement(value, Web);
    private static FeralHistoricalSimRequest Request(string race = "night-elf") => new() {
        Character = new() { Race = race }, UseProvisionalLevel60Baseline = true,
        ManualInputs = new() { PreTalentCatAttackPower = 800m }, // Illustrative input, never player data.
        Talents = new() { Ranks = new(ForeverItemizedPresetCatalog.Load("druid", "feral").Ranks, StringComparer.Ordinal),
            BalancePointsSpent = 0, FeralPointsSpent = 31, RestorationPointsSpent = 5, TotalPoints = 36, UnspentPoints = 15 },
        Seed = 175617, DurationSeconds = 18m, CaptureTimeline = true
    };
    private static void Totals(FeralHistoricalSimRequest r)
    {
        r.Talents.FeralPointsSpent = r.Talents.Ranks.Where(x => x.Key != "furor").Sum(x => x.Value);
        r.Talents.RestorationPointsSpent = r.Talents.Ranks.GetValueOrDefault("furor");
        r.Talents.TotalPoints = r.Talents.Ranks.Values.Sum(); r.Talents.UnspentPoints = 51 - r.Talents.TotalPoints;
    }
    private static FeralHistoricalPreparation Preview(FeralHistoricalSimRequest r) =>
        Assert.IsType<FeralHistoricalPreparation>(Assert.IsType<OkObjectResult>(new ForeverCharacterController().PreviewFeral(r)).Value);
    private static ForeverLevel60SimulationRequest Envelope(FeralHistoricalSimRequest r) => new() {
        ClassKey = "druid", Specialization = "feral", ItemizedTalentMode = true,
        ItemizedTalentRanks = r.Talents?.Ranks is { } ranks ? new(ranks, ranks.Comparer) : new(StringComparer.Ordinal), Feral = r
    };
    private static string Fingerprint(object value)
    {
        var ids = new Dictionary<string,string>();
        return Regex.Replace(Json(value), "[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", m => {
            if (!ids.TryGetValue(m.Value, out var id)) ids[m.Value] = id = $"ENGINE-ID-{ids.Count}";
            return id;
        });
    }

    [Fact]
    public void RealPresetMatchesEveryRetainedSourceKeyCapRowParentAndAdmissionReceipt()
    {
        var c = new ForeverSoloTalentController();
        var p = Assert.IsType<ForeverItemizedPresetCatalog.Preset>(Assert.IsType<OkObjectResult>(c.DefaultItemized("druid", "feral")).Value);
        Assert.Equal("warcraftsim.cp174-itemized-preset", p.Schema); Assert.Equal(1, p.Version);
        Assert.Equal("druid", p.ClassKey); Assert.Equal("feral", p.Specialization); Assert.Equal(60, p.Level);
        Assert.Equal("source-backed-partial-historical-provisional-36-ranks", p.AllocationStatus);
        Assert.Equal(36, p.TotalRankedPoints); Assert.Equal(15, p.UnspentPoints); Assert.Equal(14, p.Ranks.Count);
        using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "docs/audits/CP175-feral-historical-source.json")));
        Assert.Equal(source.RootElement.GetProperty("pinnedCommit").GetString(), p.Provenance.GetProperty("sourceCommit").GetString());
        Assert.Equal(source.RootElement.GetProperty("sourceUrl").GetString(), Assert.Single(p.Provenance.GetProperty("sources").EnumerateArray()).GetString());
        foreach (var name in new[] { "acquisition", "localDecodedReceiptSha256", "hashMeaning", "parentRankConvention" })
            Assert.Equal(source.RootElement.GetProperty(name).GetString(), p.Provenance.GetProperty(name).GetString());
        var retained = source.RootElement.GetProperty("admittedKeys").EnumerateArray().ToArray();
        Assert.Equal(14, retained.Length);
        var catalog = ForeverHistoricalCasterTalentCatalog.ForSpecialization("druid", "feral");
        foreach (var x in Source) {
            Assert.Equal(x.Cap, p.Ranks[x.Key]);
            var actual = Assert.Single(catalog, t => t.Key == x.Key);
            Assert.Equal(x.Tree, actual.Tree); Assert.Equal(x.Cap, actual.MaxRank);
            Assert.Equal(x.Row, actual.Row); Assert.Equal(x.Column, actual.Column); Assert.Equal(x.Parent, actual.PrerequisiteKey);
            Assert.Equal(x.Parent is null ? 0 : Source.Single(y => y.Key == x.Parent).Cap, actual.PrerequisiteRank);
            var record = Assert.Single(retained, t => t.GetProperty("canonicalKey").GetString() == x.Key);
            Assert.Equal(x.Tree, record.GetProperty("tree").GetString());
            var raw = record.GetProperty("retainedSourceRecord");
            Assert.Equal(x.Cap, raw.GetProperty("maxPoints").GetInt32());
            Assert.Equal(x.Row - 1, raw.GetProperty("location").GetProperty("rowIdx").GetInt32());
            Assert.Equal(x.Column - 1, raw.GetProperty("location").GetProperty("colIdx").GetInt32());
            if (x.Parent is null) Assert.False(raw.TryGetProperty("prereqLocation", out _));
            else {
                var parent = Source.Single(y => y.Key == x.Parent);
                Assert.Equal(parent.Row - 1, raw.GetProperty("prereqLocation").GetProperty("rowIdx").GetInt32());
                Assert.Equal(parent.Column - 1, raw.GetProperty("prereqLocation").GetProperty("colIdx").GetInt32());
            }
        }
        foreach (var (row, earlier) in new[] { (2, 10), (3, 12), (4, 19), (5, 25), (7, 30) })
            Assert.Equal(earlier, Source.Where(x => x.Tree == "feral" && x.Row < row).Sum(x => p.Ranks[x.Key]));
        var validation = Node(Assert.IsType<OkObjectResult>(c.ValidateItemized("druid", "feral", Node(Request().Talents))).Value);
        Assert.Equal(36, validation.GetProperty("pointsSpent").GetInt32()); Assert.Equal(15, validation.GetProperty("remaining").GetInt32());
        Assert.Equal(31, validation.GetProperty("treePoints").GetProperty("feral").GetInt32());
        Assert.Equal(5, validation.GetProperty("treePoints").GetProperty("restoration").GetInt32());
        foreach (var phrase in new[] { "0/31/5", "36 allocated", "15 unspent", "Not optimized", "not current-client certified", "local convention", "Omen", "6%", "10-second", "unresolved", "Natural Instinct" })
            Assert.Contains(phrase, p.Provenance.GetProperty("statement").GetString());
        Assert.DoesNotContain("manualInputs", Json(p)); Assert.DoesNotContain("observedUnbuffed", Json(p));
        var again = ForeverItemizedPresetCatalog.Load("druid", "feral"); p.Ranks.Clear(); Assert.Equal(14, again.Ranks.Count);
    }

    [Theory][InlineData("night-elf", 62, 100, 2173, 2444)][InlineData("tauren", 70, 95, 2203, 2369)][InlineData("troll", 66, 96, 2193, 2384)]
    public async Task EndpointUsesHistoricalResourcesManualApAndExactExecutionPreparation(string race, int strength, int intellect, int health, int mana)
    {
        var r = Request(race); var before = Json(r); var p = Preview(r);
        Assert.Equal("historical-primary-resource-proxy-plus-manual-pre-talent-cat-ap", p.InputMode);
        Assert.Equal(800m, p.Preview.AuthoredAttackPower); Assert.Equal(strength * .1m * 2m, p.Preview.HeartOfTheWildAttackPowerIncrement);
        Assert.Equal(90m, p.Preview.PredatoryStrikesAttackPower); Assert.Equal(800m + strength * .2m + 90m, p.Preview.SpellbookAttackPower);
        Assert.Equal(health, p.Preview.MaximumHealth); Assert.Equal(mana + intellect * 1.5m, p.Preview.MaximumMana);
        Assert.Equal(mana + intellect * 1.5m - 250m, p.Preview.ManaAfterInitialShift); Assert.Equal(0m, p.Preview.StartingEnergy);
        Assert.Equal(mana, p.DirectRequest.MaximumMana); Assert.Equal(health, p.DirectRequest.MaximumHealth);
        Assert.Null(p.Baseline.ObservedUnbuffedMeleeAttackPower); Assert.False(p.Baseline.IncludesRacialPassives);
        Assert.Equal("historical-classic-stat-proxy", p.Baseline.SourceKind); Assert.Equal("provisional-documented-proxy", p.Baseline.VerificationStatus);
        Assert.Null(p.Request.Level60Baseline);
        Assert.Equal(before, Json(p.Request)); Assert.NotEmpty(p.Preview.Abilities); Assert.NotEmpty(p.Preview.ConfiguredRotation.Entries);
        Assert.Contains(p.ModelingNotes, x => x.Contains("Troll/Druid")); Assert.Contains(p.ModelingNotes, x => x.Contains("Tauren Endurance"));
        Assert.Contains(p.ModelingNotes, x => x.Contains("Omen") && x.Contains("6%") && x.Contains("10-second") && x.Contains("unresolved"));
        Assert.Contains(p.ModelingNotes, x => x.Contains("Natural Instinct") && x.Contains("provisional"));
        var run = Node(Assert.IsType<OkObjectResult>(await new ForeverLevel60SimulationController().Simulate(Envelope(r), default)).Value);
        Assert.Equal(Fingerprint(p), Fingerprint(run.GetProperty("preparation"))); Assert.Equal(before, Json(r));
        var notes = string.Join(" ", run.GetProperty("talentModelingNotes").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("partial historical default allocation", notes); Assert.Contains("read-only preparation endpoint", notes);
        Assert.DoesNotContain("Backend-only", notes); Assert.DoesNotContain("No frontend, Feral preset", notes);
        Assert.Equal(0, run.GetProperty("inputTiming").GetProperty("additionalInputLatencyMilliseconds").GetDecimal());
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void ExplicitZeroFractionalAndManualKnobsArePreservedWithoutObservedFallback(int kind)
    {
        var r = Request(); var ap = kind switch { 0 => 0m, 1 => .125m, _ => 800m };
        r.ManualInputs.PreTalentCatAttackPower = ap; r.ManualInputs.WeaponDps = .25m;
        r.ManualInputs.MeleeHitPercent = 81.125m; r.ManualInputs.MeleeCritPercent = .75m;
        r.ManualInputs.ShapeshiftMana = kind == 0 ? 0m : 250.125m;
        r.ManualInputs.ShiftingPowerBaseMana = 1244.25m; r.ManualInputs.ShiftingPowerCostReductionPercent = 12.5m;
        var p = Preview(r); Assert.Equal(ap, p.DirectRequest.Tuning.AttackPower); Assert.Equal(ap + 102.4m, p.Preview.SpellbookAttackPower);
        Assert.Equal(2594m - r.ManualInputs.ShapeshiftMana, p.Preview.ManaAfterInitialShift);
        Assert.Equal(.25m, p.DirectRequest.Tuning.WeaponDps); Assert.Equal(81.125m, p.DirectRequest.Tuning.MeleeHitPercent);
        Assert.Equal(.75m, p.DirectRequest.Tuning.MeleeCritPercent); Assert.Equal(1244.25m, p.DirectRequest.Tuning.ShiftingPowerBaseMana);
        Assert.Equal(12.5m, p.DirectRequest.Tuning.ShiftingPowerCostReductionPercent); Assert.Null(p.Request.Level60Baseline);
    }

    [Theory][InlineData(0, 0)][InlineData(1, 5)][InlineData(2, 31)][InlineData(3, 35)][InlineData(4, 31)][InlineData(5, 36)]
    public void LegalRemovalsAndExplicitFurorZeroOrFiveRetainExactMaps(int kind, int points)
    {
        var r = Request();
        if (kind == 0) r.Talents.Ranks.Clear();
        if (kind == 1) r.Talents.Ranks = new() { ["ferocity"] = 5 };
        if (kind == 2) r.Talents.Ranks.Remove("furor");
        if (kind == 3) r.Talents.Ranks.Remove("berserk");
        if (kind == 4) r.Talents.Ranks["furor"] = 0;
        Totals(r); var p = Preview(r);
        Assert.Equal(points, p.Request.Talents.TotalPoints); Assert.Equal(51 - points, p.Request.Talents.UnspentPoints);
        Assert.Equal(Json(r.Talents), Json(p.Request.Talents)); Assert.Equal(0m, p.Preview.StartingEnergy);
        Assert.Equal(r.Talents.Ranks.ContainsKey("furor"), p.DirectRequest.Talents.Ranks.ContainsKey("furor"));
    }

    public static IEnumerable<object[]> InvalidTalents()
    {
        foreach (var x in Source) yield return new object[] { x.Key, x.Cap + 1 };
        foreach (var rank in new[] { 1, 2, 3, 4 }) yield return new object[] { "furor", rank };
        foreach (var key in new[] { "ferocity", "feral-swiftness", "shredding-attacks", "sharpened-claws", "shifting-power", "leader-of-the-pack" })
            yield return new object[] { key, 0 };
        yield return new object[] { "FEROCITY", 0 }; yield return new object[] { "natural-reaction", 0 };
        yield return new object[] { "rend-and-tear", 1 }; yield return new object[] { "ferocity", -1 };
    }
    [Theory][MemberData(nameof(InvalidTalents))]
    public async Task StrictItemizedRejectionPrecedesInvalidManualPreparation(string key, int value)
    {
        var r = Request(); r.Talents.Ranks[key] = value; Totals(r); r.ManualInputs.PreTalentCatAttackPower = null;
        var before = Json(r); var direct = new ForeverSoloTalentController().ValidateItemized("druid", "feral", Node(r.Talents));
        Assert.IsType<BadRequestObjectResult>(direct);
        var expected = Json(new { error = "Strict itemized talent allocation was rejected.", validation = direct });
        var preview = Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewFeral(r));
        Assert.Equal(expected, Json(preview.Value)); Assert.Equal(before, Json(r));
        // Shared dispatch performs its earlier canonical-key check for aliases/unknown keys.
        var run = Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(Envelope(r), default));
        if (Source.Any(x => x.Key == key)) Assert.Equal(expected, Json(run.Value));
    }

    [Theory][InlineData("balancePointsSpent")][InlineData("feralPointsSpent")][InlineData("restorationPointsSpent")][InlineData("totalPoints")][InlineData("unspentPoints")]
    public void DeclaredTotalsCannotBeForged(string field)
    {
        var json = JsonSerializer.SerializeToNode(Request(), Web)!; json["talents"]![field] = 999;
        Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewFeral(json.Deserialize<FeralHistoricalSimRequest>(Web)));
    }

    [Theory][InlineData("null")][InlineData("talents")][InlineData("ranks")][InlineData("character")][InlineData("manual")]
    [InlineData("ap")][InlineData("opt-in")][InlineData("reviewed")][InlineData("inline")][InlineData("front-ravage")][InlineData("shift")]
    [InlineData("overflow")][InlineData("negative-ap")][InlineData("negative-armor")][InlineData("role")][InlineData("race")]
    public async Task InvalidPreparationAndOverflowStayBadRequestsWithExecutionPrecedence(string kind)
    {
        var r = Request();
        switch (kind) {
            case "null": Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewFeral(null)); return;
            case "talents": r.Talents = null!; break; case "ranks": r.Talents.Ranks = null!; break;
            case "character": r.Character = null!; break; case "manual": r.ManualInputs = null!; break;
            case "ap": r.ManualInputs.PreTalentCatAttackPower = null; break;
            case "opt-in": r.UseProvisionalLevel60Baseline = false; break;
            case "reviewed": r.UseReviewedLevel60Baseline = true; break;
            case "inline": r.Level60Baseline = new(); break;
            case "front-ravage": r.StartInProwl = true; r.TargetBehind = false; r.ManualInputs.ShapeshiftMana = 99999; break;
            case "shift": r.ManualInputs.ShapeshiftMana = 2594.001m; break;
            case "overflow": r.ManualInputs.PreTalentCatAttackPower = decimal.MaxValue; break;
            case "negative-ap": r.ManualInputs.PreTalentCatAttackPower = -1m; break;
            case "negative-armor": r.TargetArmor = -1m; break;
            case "role": r.Role = "bear"; break; case "race": r.Character.Race = "human"; break;
        }
        var before = Json(r); var p = Assert.IsType<BadRequestObjectResult>(new ForeverCharacterController().PreviewFeral(r));
        var run = Assert.IsType<BadRequestObjectResult>(await new ForeverLevel60SimulationController().Simulate(Envelope(r), default));
        Assert.Equal(Json(run.Value), Json(p.Value)); Assert.Equal(before, Json(r));
        if (kind == "front-ravage") Assert.Contains("Ravage from Prowl", Json(p.Value));
        if (kind == "overflow") Assert.Contains("representable simulation arithmetic", Json(p.Value));
    }

    public static IEnumerable<object[]> RejectedJson()
    {
        foreach (var field in new[] { "preTalentCatAttackPower", "weaponDps", "meleeHitPercent", "meleeCritPercent", "shapeshiftMana", "shiftingPowerBaseMana", "shiftingPowerCostReductionPercent" })
            yield return new object[] { "manualInputs", field, "\"1\"" };
        foreach (var field in new[] { "durationSeconds", "seed", "targetLevel", "targetMaximumHealth", "targetInitialHealthPercent", "targetArmor" })
            yield return new object[] { "", field, "\"1\"" };
        foreach (var field in new[] { "balancePointsSpent", "feralPointsSpent", "restorationPointsSpent", "totalPoints", "unspentPoints" })
            foreach (var value in new[] { "null", "\"0\"", "0.5", "2147483648" }) yield return new object[] { "talents", field, value };
        foreach (var field in new[] { "tuning", "maximumMana", "maximumHealth", "externalModifiers", "combatItems", "racialActive", "rotationPolicy", "additionalTargets", "formTransitions", "targetTransitions", "initialComboPointsByTarget", "enableBossAttacks", "useThorns", "useBearUtility", "inputLatencyMs" })
            yield return new object[] { "", field, "{}" };
        foreach (var field in new[] { "name", "equipment", "baseStats", "observedCatAttackPower" }) yield return new object[] { "character", field, "{}" };
        yield return new object[] { "manualInputs", "observedAttackPower", "800" };
        yield return new object[] { "talents", "unknown", "0" };
    }
    [Theory][MemberData(nameof(RejectedJson))]
    public void StrictPreviewDtoRejectsUnsupportedFieldsNumericStringsAndInvalidDeclarations(string section, string field, string raw)
    {
        var node = JsonSerializer.SerializeToNode(Request(), Web)!;
        (section == "" ? node : node[section])![field] = JsonNode.Parse(raw);
        Assert.Throws<JsonException>(() => node.Deserialize<FeralHistoricalSimRequest>(Web));
    }

    [Fact]
    public async Task ConcurrentCallsAndReturnedMutableGraphsAreIndependentlyOwned()
    {
        var r = Request(); r.Talents.Ranks["furor"] = 0; Totals(r);
        var before = Json(r); var p = Preview(r); var expected = Fingerprint(p);
        p.Request.ManualInputs.PreTalentCatAttackPower = 5; p.Request.Talents.Ranks.Clear(); p.Baseline.Strength = 999;
        p.DirectRequest.Tuning.AttackPower = 999; p.DirectRequest.Talents.Ranks.Clear();
        p.Preview.ConfiguredRotation.Entries.Clear(); p.Preview.Abilities[0].Effects.Clear();
        Assert.Equal(before, Json(r)); Assert.Equal(expected, Fingerprint(Preview(r)));
        await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => {
            var local = Request(i % 2 == 0 ? "night-elf" : "tauren"); local.ManualInputs.PreTalentCatAttackPower = i + .25m;
            var result = Preview(local); Assert.Equal(i + .25m, result.Preview.AuthoredAttackPower);
            Assert.Equal(local.Character.Race, result.Baseline.Race); Assert.Equal(expected, Fingerprint(Preview(r)));
        })));
        var isolated = Preview(r); r.Talents.Ranks.Clear(); r.Character.Race = "troll"; r.ManualInputs.PreTalentCatAttackPower = 999;
        Assert.Equal(expected, Fingerprint(isolated));
    }

    [Fact]
    public void PreparationCallGraphCannotRunEngineOrGenericDruidGeneration()
    {
        // Inspect actual compiled call targets, including compiler-generated delegates.
        // This is independent of duration, speed, response labels or empty timelines.
        var start = typeof(ForeverCharacterController).GetMethod(nameof(ForeverCharacterController.PreviewFeral))!;
        Assert.Equal("preview-feral", Assert.Single(start.GetCustomAttributes<HttpPostAttribute>()).Template);
        var visited = new HashSet<MethodBase>(); var pending = new Stack<MethodBase>(); pending.Push(start);
        while (pending.TryPop(out var method)) {
            if (!visited.Add(method)) continue;
            foreach (var target in Calls(method)) {
                var type = target.DeclaringType;
                Assert.False(type == typeof(SimulationContext) || type == typeof(SimulationEngine), $"Preparation reaches engine: {target}");
                Assert.False(target.Name == "Run" && (type == typeof(FeralDruidSimRunner) || type == typeof(FeralHistoricalInputAdapter)), $"Preparation runs combat: {target}");
                Assert.False(type == typeof(ForeverLevel60Baseline) && target.Name == "Generate", "Preparation uses generic Druid generation.");
                if (type?.Assembly.GetName().Name?.StartsWith("WarcraftSim", StringComparison.Ordinal) == true) pending.Push(target);
            }
        }
        Assert.Contains(visited, x => x.DeclaringType == typeof(ForeverSoloTalentController) && x.Name == "ValidateItemized");
        Assert.Contains(visited, x => x.DeclaringType == typeof(FeralHistoricalInputAdapter) && x.Name == "Prepare");
        Assert.Contains(visited, x => x.DeclaringType == typeof(FeralDruidSimRunner) && x.Name == "Preview");
    }

    private static IEnumerable<MethodBase> Calls(MethodBase method)
    {
        var code = method.GetMethodBody()?.GetILAsByteArray(); if (code is null) yield break;
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
        for (var i = 0; i < code.Length;) {
            var value = (ushort)code[i++]; if (value == 0xfe) value = (ushort)(0xfe00 | code[i++]);
            var op = opcodes[value];
            if (op.OperandType == OperandType.InlineMethod) {
                var target = method.Module.ResolveMethod(BitConverter.ToInt32(code, i), method.DeclaringType?.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                if (target is not null) yield return target;
            }
            i += op.OperandType switch {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(code, i),
                _ => 4
            };
        }
    }
}
