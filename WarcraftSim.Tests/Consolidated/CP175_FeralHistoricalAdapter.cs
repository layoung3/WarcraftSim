using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Independent historical/manual adapter oracles, not current-client stat certification.
/// Tier/parent legality belongs to the separately tested strict admission boundary.</summary>
public sealed class Cp175FeralHistoricalAdapter
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private const string InputMode = "historical-primary-resource-proxy-plus-manual-pre-talent-cat-ap";

    private static FeralHistoricalSimRequest Request(string race = "night-elf") => new() {
        Character = new() { Race = race }, UseProvisionalLevel60Baseline = true,
        ManualInputs = new() { PreTalentCatAttackPower = 800m },
        DurationSeconds = 18m, CaptureTimeline = true, Seed = 175614
    };

    private static string Json(object? value) => JsonSerializer.Serialize(value, Web);
    private static string Fingerprint(object value)
    {
        var ids = new Dictionary<string,string>();
        return Regex.Replace(Json(value), "[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", m => {
            if (!ids.TryGetValue(m.Value, out var id)) ids[m.Value] = id = $"ENGINE-ID-{ids.Count}";
            return id;
        });
    }

    // Fixed independent literals from the retained historical resource ledger.
    // Do not obtain expected values through the catalog or production conversion helpers.
    public static IEnumerable<object[]> Baselines() => new object[][] {
        new object[] { "night-elf", 62, 65, 69, 100, 110, 2173, 2444 },
        new object[] { "tauren", 70, 55, 72, 95, 112, 2203, 2369 },
        new object[] { "troll", 66, 62, 71, 96, 111, 2193, 2384 }
    };

    [Theory][MemberData(nameof(Baselines))]
    public void HistoricalPrimariesAndResourceTotalsOwnPreparationWithoutOrdinaryConversions(
        string race, int strength, int agility, int stamina, int intellect, int spirit, int health, int mana)
    {
        var r = Request(race); var before = Json(r);
        var p = FeralHistoricalInputAdapter.Prepare(r);
        Assert.Equal(InputMode, p.InputMode);
        Assert.Equal(race, p.Baseline.Race); Assert.Equal("druid", p.Baseline.ClassKey);
        Assert.Equal(60, p.Baseline.Level);
        Assert.Equal(strength, p.Baseline.Strength); Assert.Equal(agility, p.Baseline.Agility);
        Assert.Equal(stamina, p.Baseline.Stamina); Assert.Equal(intellect, p.Baseline.Intellect);
        Assert.Equal(spirit, p.Baseline.Spirit);
        Assert.Equal((decimal?)health, p.Baseline.ObservedUnbuffedMaximumHealth);
        Assert.Equal((decimal?)mana, p.Baseline.ObservedUnbuffedMaximumMana);
        Assert.Null(p.Baseline.ObservedUnbuffedMeleeAttackPower);
        Assert.Equal(health, p.DirectRequest.MaximumHealth); Assert.Equal(mana, p.DirectRequest.MaximumMana);
        Assert.Equal(strength, p.DirectRequest.Tuning.BaseStrength);
        Assert.Equal(stamina, p.DirectRequest.Tuning.BaseStamina);
        Assert.Equal(intellect, p.DirectRequest.Tuning.BaseIntellect);
        Assert.Equal(800m, p.DirectRequest.Tuning.AttackPower);
        Assert.Equal(800m, p.Preview.AuthoredAttackPower); Assert.Equal(800m, p.Preview.SpellbookAttackPower);
        Assert.Equal(0m, p.Preview.HeartOfTheWildAttackPowerIncrement);
        Assert.Equal(0m, p.Preview.PredatoryStrikesAttackPower);
        Assert.Equal(health, p.Preview.MaximumHealth); Assert.Equal(mana, p.Preview.MaximumMana);
        Assert.Equal(mana - 250m, p.Preview.ManaAfterInitialShift);
        Assert.Equal(20m, p.DirectRequest.Tuning.MeleeCritPercent); // No Agility crit addition.
        Assert.Equal(100m, p.Preview.MaximumEnergy); Assert.Equal(0m, p.Preview.StartingEnergy);
        Assert.Equal(0m, p.Preview.StartingRage); Assert.Equal("cat", p.Preview.InitialForm);
        Assert.False(p.DirectRequest.EnableBossAttacks); Assert.False(p.DirectRequest.UseThorns);
        Assert.False(p.DirectRequest.UseBearUtility); Assert.Equal(0, p.DirectRequest.AdditionalTargets);
        Assert.Empty(p.DirectRequest.FormTransitions); Assert.Empty(p.DirectRequest.TargetTransitions);
        Assert.Empty(p.DirectRequest.InitialComboPointsByTarget);
        Assert.Equal(before, Json(r));
    }

    public static IEnumerable<object[]> RankMatrix()
    {
        foreach (var baseline in Baselines())
            for (var hotw = 0; hotw <= 5; hotw++)
                for (var predatory = 0; predatory <= 3; predatory++)
                    foreach (var ap in new[] { 0, 800 })
                        yield return new[] { baseline[0], baseline[1], baseline[4], baseline[6], baseline[7], hotw, predatory, ap };
    }

    [Theory][MemberData(nameof(RankMatrix))]
    public void EveryHotwAndPredatoryRankAddsOnlyItsIndependentIncrement(
        string race, int strength, int intellect, int health, int mana, int hotw, int predatory, int ap)
    {
        var r = Request(race); r.ManualInputs.PreTalentCatAttackPower = ap;
        r.Talents.Ranks["heart-of-the-wild"] = hotw; r.Talents.Ranks["predatory-strikes"] = predatory;
        var p = FeralHistoricalInputAdapter.Preview(r);
        var hotwAp = strength * .04m * hotw;
        var hotwMana = intellect * .30m * hotw;
        Assert.Equal(ap, p.Preview.AuthoredAttackPower);
        Assert.Equal(hotwAp, p.Preview.HeartOfTheWildAttackPowerIncrement);
        Assert.Equal(predatory * 30m, p.Preview.PredatoryStrikesAttackPower);
        Assert.Equal(ap + hotwAp + predatory * 30m, p.Preview.SpellbookAttackPower);
        Assert.Equal(health, p.Preview.MaximumHealth); // No Cat HOTW Health, Stamina or racial add-on.
        Assert.Equal(mana + hotwMana, p.Preview.MaximumMana);
        Assert.Equal(mana + hotwMana - 250m, p.Preview.ManaAfterInitialShift);
        Assert.Equal((decimal?)ap, r.ManualInputs.PreTalentCatAttackPower);
        Assert.Equal(ap, p.DirectRequest.Tuning.AttackPower); // Returned authored input stays untalented.
    }

    [Fact]
    public void NightElfExampleAndRankRemovalDoNotRetainPriorBonuses()
    {
        var r = Request(); r.Talents.Ranks["heart-of-the-wild"] = 5;
        r.Talents.Ranks["predatory-strikes"] = 3;
        var p = FeralHistoricalInputAdapter.Prepare(r);
        Assert.Equal(902.4m, p.Preview.SpellbookAttackPower);
        Assert.Equal(12.4m, p.Preview.HeartOfTheWildAttackPowerIncrement);
        Assert.Equal(90m, p.Preview.PredatoryStrikesAttackPower);
        Assert.Equal(2173m, p.Preview.MaximumHealth);
        Assert.Equal(2594m, p.Preview.MaximumMana); Assert.Equal(2344m, p.Preview.ManaAfterInitialShift);
        r.Talents.Ranks.Remove("heart-of-the-wild");
        var noHotw = FeralHistoricalInputAdapter.Prepare(r).Preview;
        Assert.Equal(890m, noHotw.SpellbookAttackPower); Assert.Equal(2444m, noHotw.MaximumMana);
        r.Talents.Ranks.Remove("predatory-strikes");
        var neither = FeralHistoricalInputAdapter.Prepare(r).Preview;
        Assert.Equal(800m, neither.SpellbookAttackPower); Assert.Equal(2194m, neither.ManaAfterInitialShift);
        Assert.Equal(902.4m, p.Preview.SpellbookAttackPower); // Older receipt remains independent.
    }

    [Theory][InlineData(0)][InlineData(800)]
    public void ManualWeaponHitCritAndExplicitZeroValuesAreNotReplaced(int ap)
    {
        var r = Request(); r.ManualInputs = new() {
            PreTalentCatAttackPower = ap, WeaponDps = 0m, MeleeHitPercent = 0m,
            MeleeCritPercent = 0m, ShapeshiftMana = 0m, ShiftingPowerBaseMana = 0m,
            ShiftingPowerCostReductionPercent = 0m
        };
        var p = FeralHistoricalInputAdapter.Prepare(r);
        Assert.Equal(ap, p.Preview.SpellbookAttackPower);
        Assert.Equal(0m, p.DirectRequest.Tuning.WeaponDps);
        Assert.Equal(0m, p.DirectRequest.Tuning.MeleeHitPercent);
        Assert.Equal(0m, p.DirectRequest.Tuning.MeleeCritPercent);
        Assert.Equal(0m, p.DirectRequest.Tuning.ShiftingPowerBaseMana);
        Assert.Equal(0m, p.Preview.InitialShiftManaCost);
        Assert.Equal(2444m, p.Preview.ManaAfterInitialShift);
        var claw = p.Preview.Abilities.Single(a => a.Key == "claw");
        Assert.Equal(100m + ap * .15m, claw.Effects.Single(e => e.Key == "claw-hit").MinimumValue);
        r.ManualInputs.WeaponDps = 123.25m; r.ManualInputs.MeleeHitPercent = 97.75m;
        r.ManualInputs.MeleeCritPercent = 31.5m;
        var changed = FeralHistoricalInputAdapter.Prepare(r);
        Assert.Equal(123.25m, changed.DirectRequest.Tuning.WeaponDps);
        Assert.Equal(97.75m, changed.DirectRequest.Tuning.MeleeHitPercent);
        Assert.Equal(31.5m, changed.DirectRequest.Tuning.MeleeCritPercent);
    }

    [Fact]
    public void ShiftingPowerUsesManualBaseManaRatherThanBaselineOrHotwMaximum()
    {
        var r = Request(); r.Talents.Ranks = new() {
            ["heart-of-the-wild"] = 5, ["shredding-attacks"] = 3,
            ["shifting-power"] = 1, ["improved-shifting-power"] = 2
        };
        r.ManualInputs.ShiftingPowerCostReductionPercent = 20m;
        var p = FeralHistoricalInputAdapter.Prepare(r);
        var shifting = p.Preview.Abilities.Single(a => a.Key == "shifting-power");
        Assert.Equal(547.36m, shifting.ResourceCosts.Single().Amount); // 1244 * .55 * .8
        Assert.Equal(8m, shifting.CooldownSeconds); Assert.Equal(2594m, p.Preview.MaximumMana);
        Assert.Equal(250m, p.Preview.InitialShiftManaCost); // Not silently reduced by the separate knob.
        var run = FeralHistoricalInputAdapter.Run(r).Simulation;
        Assert.Contains(run.Run.Timeline, e => e.AbilityKey == "shifting-power" && e.TimeSeconds == 0m &&
            e.Type == CombatEventType.ResourceChanged && e.Amount == 40m);
        r.ManualInputs.ShiftingPowerBaseMana = 1000m;
        var changed = FeralHistoricalInputAdapter.Prepare(r);
        Assert.Equal(440m, changed.Preview.Abilities.Single(a => a.Key == "shifting-power").ResourceCosts.Single().Amount);
        Assert.Equal(2594m, changed.Preview.MaximumMana); Assert.Equal(2344m, changed.Preview.ManaAfterInitialShift);
    }

    [Fact]
    public void StartupAffordabilityUsesTalentAdjustedMaximumManaWithExactBoundary()
    {
        var r = Request(); r.Talents.Ranks["heart-of-the-wild"] = 5;
        r.ManualInputs.ShapeshiftMana = 2594m;
        var p = FeralHistoricalInputAdapter.Prepare(r);
        Assert.Equal(0m, p.Preview.ManaAfterInitialShift);
        Assert.Equal(0m, FeralHistoricalInputAdapter.Run(r).Simulation.EndingMana);
        r.ManualInputs.ShapeshiftMana = 2594.01m;
        Assert.Throws<ArgumentException>(() => FeralHistoricalInputAdapter.Prepare(r));
        Assert.Throws<ArgumentException>(() => FeralHistoricalInputAdapter.Run(r));
        r.ManualInputs.ShapeshiftMana = 2500m; r.Talents.Ranks.Clear();
        Assert.Throws<ArgumentException>(() => FeralHistoricalInputAdapter.Prepare(r));
    }

    [Theory][InlineData("night-elf")][InlineData("tauren")][InlineData("troll")]
    public void ReceiptsKeepManualAndHistoricalProvenanceAndRacialOmissionsVisible(string race)
    {
        var p = FeralHistoricalInputAdapter.Prepare(Request(race));
        Assert.Equal("provisional-documented-proxy", p.Baseline.VerificationStatus);
        Assert.Equal("historical-classic-stat-proxy", p.Baseline.SourceKind);
        Assert.Contains("7779ebbf79dc7f1341e6ab939b28a3402c9a730a", p.Baseline.SourceReference);
        Assert.False(p.Baseline.IncludesRacialPassives);
        Assert.Null(p.Baseline.ObservedUnbuffedMeleeAttackPower);
        Assert.Contains(p.ModelingNotes, x => x.Contains("Manual pre-talent", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(p.ModelingNotes, x => x.Contains("Tauren Endurance", StringComparison.Ordinal));
        Assert.Contains(p.ModelingNotes, x => x.Contains("not verified Forever", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(p.ModelingNotes, x => x.Contains("white attacks", StringComparison.OrdinalIgnoreCase));
        if (race == "troll") Assert.Contains("extrapolated", p.Baseline.ConfidenceClassification);
        // The inner runner preview keeps its actual inclusive-input contract.
        Assert.Equal("authored-inclusive", p.Preview.InputMode);
        var json = JsonSerializer.SerializeToElement(p, Web);
        Assert.False(json.TryGetProperty("simulation", out _));
        Assert.False(json.GetProperty("preview").TryGetProperty("damageDone", out _));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void SnapshotsPreserveComparersInsertionOrderAndExplicitZeroOwnership(bool ignoreCase)
    {
        var r = Request(); var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        r.Talents.Ranks = new(comparer) { ["predatory-strikes"] = 0, ["heart-of-the-wild"] = 5, ["furor"] = 0 };
        r.Talents.BalancePointsSpent = 0; r.Talents.FeralPointsSpent = 5; r.Talents.RestorationPointsSpent = 0;
        r.Talents.TotalPoints = 5; r.Talents.UnspentPoints = 46;
        var before = Json(r); var p = FeralHistoricalInputAdapter.Prepare(r);
        Assert.NotSame(r, p.Request); Assert.NotSame(r.Character, p.Request.Character);
        Assert.NotSame(r.ManualInputs, p.Request.ManualInputs); Assert.NotSame(r.Talents, p.Request.Talents);
        Assert.NotSame(r.Talents.Ranks, p.Request.Talents.Ranks);
        Assert.NotSame(p.Request.Talents.Ranks, p.DirectRequest.Talents.Ranks);
        Assert.Same(comparer, p.Request.Talents.Ranks.Comparer);
        Assert.Same(comparer, p.DirectRequest.Talents.Ranks.Comparer);
        Assert.Equal(r.Talents.Ranks.Keys.ToArray(), p.Request.Talents.Ranks.Keys.ToArray());
        Assert.Equal(r.Talents.Ranks.Keys.ToArray(), p.DirectRequest.Talents.Ranks.Keys.ToArray());
        Assert.Equal(before, Json(p.Request));
        p.Request.Talents.Ranks.Clear(); p.Request.Character.Race = "troll";
        p.Request.ManualInputs.PreTalentCatAttackPower = 9999m;
        Assert.Equal(3, p.DirectRequest.Talents.Ranks.Count); Assert.Equal(800m, p.DirectRequest.Tuning.AttackPower);
        Assert.Equal(before, Json(r));
    }

    [Fact]
    public void ReturnedGraphsAndCatalogBaselineCannotPoisonLaterPreviewOrExecution()
    {
        var r = Request(); r.Talents.Ranks["heart-of-the-wild"] = 5;
        r.Talents.Ranks["predatory-strikes"] = 3;
        var before = Json(r); var expected = Fingerprint(FeralHistoricalInputAdapter.Run(r).Simulation);
        var p = FeralHistoricalInputAdapter.Preview(r);
        Assert.NotSame(ForeverLevel60BaselineCatalog.FindProvisional("night-elf", "druid"), p.Baseline);
        var expectedPreview = Fingerprint(p.Preview);
        p.Baseline.Strength = 9999m; p.Baseline.ObservedUnbuffedMaximumHealth = 1m;
        p.Request.Character.Race = "tauren"; p.Request.Talents.Ranks.Clear();
        p.Request.ManualInputs.PreTalentCatAttackPower = 1m;
        p.DirectRequest.Tuning.AttackPower = 9999m; p.DirectRequest.Talents.Ranks.Clear();
        p.DirectRequest.FormTransitions.Add(new() { AtSeconds = 7m, Form = "bear" });
        var claw = p.Preview.Abilities.Single(a => a.Key == "claw");
        claw.Effects[0].MinimumValue = -999m; claw.ResourceCosts[0].Amount = 999m;
        claw.RequiredSourceAuraKeys.Clear();
        p.Preview.ConfiguredRotation.Entries[0].Target.ActorKey = "mutated";
        p.Preview.ConfiguredRotation.Entries.Clear();
        Assert.Equal(before, Json(r));
        Assert.Equal(expectedPreview, Fingerprint(FeralHistoricalInputAdapter.Preview(r).Preview));
        Assert.Equal(expected, Fingerprint(FeralHistoricalInputAdapter.Run(r).Simulation));
        Assert.Equal(62m, ForeverLevel60BaselineCatalog.FindProvisional("night-elf", "druid").Strength);
        var returned = FeralHistoricalInputAdapter.Run(r);
        returned.Preparation.DirectRequest.Tuning.AttackPower = 0m;
        returned.Preparation.Request.Talents.Ranks.Clear();
        Assert.Equal(expected, Fingerprint(returned.Simulation));
        Assert.Equal(expected, Fingerprint(FeralHistoricalInputAdapter.Run(r).Simulation));
    }

    [Fact]
    public void CallerMutationAfterPreparationCannotRewriteAnExistingReceipt()
    {
        var r = Request(); r.Talents.Ranks["heart-of-the-wild"] = 5;
        var p = FeralHistoricalInputAdapter.Prepare(r); var before = Fingerprint(p);
        r.Character.Race = "tauren"; r.ManualInputs.PreTalentCatAttackPower = 0m;
        r.Talents.Ranks.Clear(); r.TargetArmor = 9999m; r.TargetMaximumHealth = 1m;
        Assert.Equal(before, Fingerprint(p));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task JsonRoundTripsAndConcurrentCallsRetainIndependentPreparation(bool roundTrip)
    {
        var r = Request(); r.Talents.Ranks = new(StringComparer.Ordinal) {
            ["heart-of-the-wild"] = 5, ["predatory-strikes"] = 3, ["furor"] = 0
        };
        if (roundTrip) r = JsonSerializer.Deserialize<FeralHistoricalSimRequest>(Json(r), Web)!;
        var before = Json(r); var expected = Fingerprint(FeralHistoricalInputAdapter.Run(r).Simulation);
        var preview = Fingerprint(FeralHistoricalInputAdapter.Prepare(r).Preview);
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => {
            var p = FeralHistoricalInputAdapter.Prepare(r);
            Assert.Same(r.Talents.Ranks.Comparer, p.Request.Talents.Ranks.Comparer);
            Assert.Same(r.Talents.Ranks.Comparer, p.DirectRequest.Talents.Ranks.Comparer);
            Assert.Equal(preview, Fingerprint(p.Preview));
            Assert.Equal(expected, Fingerprint(FeralHistoricalInputAdapter.Run(r).Simulation));
        })));
        Assert.Equal(before, Json(r));
    }

    [Theory][InlineData("night-elf",62,69,100,2173,2444)]
    [InlineData("tauren",70,72,95,2203,2369)][InlineData("troll",66,71,96,2193,2384)]
    public void ActualExecutionMatchesIndependentlyAuthoredLegacyInputRatherThanLegacyDefaults(
        string race, int strength, int stamina, int intellect, int health, int mana)
    {
        var r = Request(race); r.Talents.Ranks = new() {
            ["heart-of-the-wild"] = 5, ["predatory-strikes"] = 3
        };
        r.ManualInputs.WeaponDps = 87m; r.ManualInputs.MeleeHitPercent = 100m;
        r.ManualInputs.MeleeCritPercent = 31m; r.TargetArmor = 1700m; r.UseFaerieFire = true;
        var direct = new FeralDruidSimRequest {
            Role = "cat", DurationSeconds = 18m, CaptureTimeline = true, Seed = 175614,
            MaximumHealth = health, MaximumMana = mana, TargetMaximumHealth = 1000000m,
            TargetArmor = 1700m, UseFaerieFire = true,
            Talents = new() { Ranks = new() { ["heart-of-the-wild"] = 5, ["predatory-strikes"] = 3 } },
            Tuning = new() { AttackPower = 800m, BaseStrength = strength, BaseStamina = stamina,
                BaseIntellect = intellect, WeaponDps = 87m, MeleeHitPercent = 100m, MeleeCritPercent = 31m }
        };
        var result = FeralHistoricalInputAdapter.Run(r);
        Assert.Equal(Fingerprint(new FeralDruidSimRunner().Run(direct)), Fingerprint(result.Simulation));
        Assert.Equal(Fingerprint(FeralDruidSimRunner.Preview(direct)), Fingerprint(result.Preparation.Preview));
        Assert.Equal(800m + strength * .20m, result.Simulation.Run.CustomMetrics["feral-heart-of-the-wild-attack-power"]);
        Assert.Equal(health, result.Simulation.Run.CustomMetrics["feral-heart-of-the-wild-maximum-health"]);
        Assert.Equal(mana + intellect * 1.5m, result.Simulation.Run.CustomMetrics["feral-heart-of-the-wild-maximum-mana"]);
    }

    [Theory][InlineData("cat",926,5500)][InlineData("bear",890,5940)]
    public void HistoricalAdapterDoesNotChangeLegacyInclusiveCatOrBearBudgets(string role, int ap, int health)
    {
        var legacy = new FeralDruidSimRequest { Role = role, DurationSeconds = 2m,
            Talents = new() { Ranks = new() { ["heart-of-the-wild"] = 5, ["predatory-strikes"] = 3 } } };
        var before = Json(legacy); var expected = Fingerprint(new FeralDruidSimRunner().Run(legacy));
        _ = FeralHistoricalInputAdapter.Run(Request());
        var preview = FeralDruidSimRunner.Preview(legacy);
        Assert.Equal("authored-inclusive", preview.InputMode); Assert.Equal(800m, preview.AuthoredAttackPower);
        Assert.Equal(ap, preview.SpellbookAttackPower); Assert.Equal(health, preview.MaximumHealth);
        Assert.Equal(4270m, preview.MaximumMana); Assert.Equal(4020m, preview.ManaAfterInitialShift);
        Assert.Equal(expected, Fingerprint(new FeralDruidSimRunner().Run(legacy)));
        Assert.Equal(before, Json(legacy));
    }

    public static IEnumerable<object[]> InvalidRequests() => Enumerable.Range(0, 32).Select(i => new object[] { i });

    [Theory][MemberData(nameof(InvalidRequests))]
    public void InvalidInputAndErrorPrecedenceAreIdenticalForPreparePreviewAndRun(int kind)
    {
        var r = Request();
        switch (kind) {
            case 0: r = null!; break;
            case 1: r.Character = null!; break;
            case 2: r.Character.ClassKey = "Druid"; break;
            case 3: r.Character.SpecializationKey = "balance"; break;
            case 4: r.Character.Level = 59; break;
            case 5: r.Character.RulesetKey = "classic"; break;
            case 6: r.Character.Race = "human"; break;
            case 7: r.Character.Race = "Night-Elf"; break;
            case 8: r.Role = "bear"; break;
            case 9: r.TargetLevel = 62; break;
            case 10: r.TargetInitialHealthPercent = 99m; break;
            case 11: r.Talents = null!; break;
            case 12: r.Talents.Ranks = null!; break;
            case 13: r.ManualInputs = null!; break;
            case 14: r.UseProvisionalLevel60Baseline = false; break;
            case 15: r.UseProvisionalLevel60Baseline = false; r.UseReviewedLevel60Baseline = true; break;
            case 16: r.UseReviewedLevel60Baseline = true; break;
            case 17: r.Level60Baseline = new() { Race = "tauren", ClassKey = "druid" }; break;
            case 18: r.ManualInputs.PreTalentCatAttackPower = null; break;
            case 19: r.ManualInputs.PreTalentCatAttackPower = -1m; break;
            case 20: r.ManualInputs.WeaponDps = -1m; break;
            case 21: r.ManualInputs.MeleeHitPercent = 101m; break;
            case 22: r.ManualInputs.MeleeCritPercent = -1m; break;
            case 23: r.ManualInputs.ShiftingPowerBaseMana = -1m; break;
            case 24: r.ManualInputs.ShiftingPowerCostReductionPercent = 101m; break;
            case 25: r.DurationSeconds = 0m; break;
            case 26: r.TargetMaximumHealth = 0m; break;
            case 27: r.TargetArmor = -1m; break;
            case 28: r.StartInProwl = true; r.TargetBehind = false; r.ManualInputs.ShapeshiftMana = 9999m; break;
            case 29: r.Character.ClassKey = "rogue"; r.Level60Baseline = new(); r.ManualInputs.PreTalentCatAttackPower = null; break;
            case 30: r.UseProvisionalLevel60Baseline = false; r.ManualInputs.PreTalentCatAttackPower = null; break;
            case 31: r.ManualInputs.PreTalentCatAttackPower = null; r.DurationSeconds = 0m; break;
        }
        var before = Json(r);
        var prepare = Assert.ThrowsAny<ArgumentException>(() => FeralHistoricalInputAdapter.Prepare(r!));
        var preview = Assert.ThrowsAny<ArgumentException>(() => FeralHistoricalInputAdapter.Preview(r!));
        var run = Assert.ThrowsAny<ArgumentException>(() => FeralHistoricalInputAdapter.Run(r!));
        Assert.Equal(prepare.GetType(), preview.GetType()); Assert.Equal(prepare.GetType(), run.GetType());
        Assert.Equal(prepare.Message, preview.Message); Assert.Equal(prepare.Message, run.Message);
        Assert.Equal(before, Json(r));
        if (kind == 0) Assert.Equal("request", Assert.IsType<ArgumentNullException>(prepare).ParamName);
        if (kind == 15) Assert.Contains("No verified Forever", prepare.Message);
        if (kind == 17) Assert.Contains("not inline observations", prepare.Message);
        if (kind == 28) Assert.StartsWith("Ravage from Prowl requires", prepare.Message);
        if (kind == 29) Assert.Contains("canonical", prepare.Message);
        if (kind == 30) Assert.Contains("baseline opt-in", prepare.Message);
        if (kind == 31) Assert.Contains("preTalentCatAttackPower", prepare.Message);
    }

    [Theory]
    [InlineData("", "tuning")][InlineData("", "maximumHealth")][InlineData("", "maximumMana")]
    [InlineData("", "formTransitions")][InlineData("", "targetTransitions")][InlineData("", "initialComboPointsByTarget")]
    [InlineData("", "additionalTargets")][InlineData("", "enableBossAttacks")][InlineData("", "useThorns")]
    [InlineData("", "useBearUtility")][InlineData("", "combatItems")][InlineData("", "rotationPolicy")]
    [InlineData("character", "baseStats")][InlineData("character", "equipment")][InlineData("character", "modifiers")]
    [InlineData("manualInputs", "baseStrength")][InlineData("manualInputs", "maximumMana")]
    [InlineData("manualInputs", "attackPower")][InlineData("talents", "syntheticFiller")]
    public void UnknownOrConflictingJsonCannotSilentlyEnterTheHistoricalContract(string container, string key)
    {
        var json = JsonNode.Parse(Json(Request()))!.AsObject();
        var target = container.Length == 0 ? json : json[container]!.AsObject();
        target[key] = JsonValue.Create(0);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<FeralHistoricalSimRequest>(json.ToJsonString(), Web));
    }
}
