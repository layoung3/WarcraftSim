using System.Text.Json;
using System.Text.RegularExpressions;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175FeralPrecombatPreview
{
    private static FeralDruidSimRequest Request(string role = "cat") => new() {
        Role = role, DurationSeconds = 35, CaptureTimeline = true, Seed = 175611,
        Talents = new() { Ranks = new() { ["heart-of-the-wild"] = 5 } },
        Tuning = new() { MeleeHitPercent = 100, MeleeCritPercent = 0, OmenProcPercent = 0 }
    };
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private static string Fingerprint(object value)
    {
        var ids = new Dictionary<string,string>();
        return Regex.Replace(Json(value), "[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", m => {
            if (!ids.TryGetValue(m.Value,out var id)) ids[m.Value] = id = $"ENGINE-ID-{ids.Count}";
            return id;
        });
    }

    [Theory][InlineData("cat",836,5500,0)][InlineData("bear",800,5940,20)]
    public void InitialBoundaryHasExactAuthoredInclusiveHotwResourcesAndActualSpellbook(
        string role, int ap, int health, int rage)
    {
        var r = Request(role); var before = Json(r);
        var preview = FeralDruidSimRunner.Preview(r); var run = new FeralDruidSimRunner().Run(r);
        Assert.Equal(role == "cat" ? "cat" : "dire-bear", preview.InitialForm);
        Assert.Equal("authored-inclusive", preview.InputMode);
        Assert.Equal(800, preview.AuthoredAttackPower);
        Assert.Equal(ap - 800, preview.HeartOfTheWildAttackPowerIncrement);
        Assert.Equal(ap, preview.SpellbookAttackPower); Assert.Equal(health, preview.MaximumHealth);
        Assert.Equal(4270, preview.MaximumMana); Assert.Equal(250, preview.InitialShiftManaCost);
        Assert.Equal(4020, preview.ManaAfterInitialShift);
        Assert.Equal(100, preview.MaximumEnergy); Assert.Equal(0, preview.StartingEnergy);
        Assert.Equal(100, preview.MaximumRage); Assert.Equal(rage, preview.StartingRage);
        Assert.Equal(preview.SpellbookAttackPower, run.Run.CustomMetrics["feral-heart-of-the-wild-attack-power"]);
        Assert.All(preview.ConfiguredRotation.Entries, e => Assert.Contains(preview.Abilities, a => a.Key == e.AbilityKey));
        Assert.Equal(before, Json(r));
        Assert.Contains(preview.ModelingNotes, n => n.Contains("not immediate eligibility"));
        Assert.Contains(preview.ModelingNotes, n => n.Contains("partial/provisional"));
    }

    [Fact]
    public void ZeroTuningStaysZeroAndShiftingPowerHappensAfterPreviewBoundary()
    {
        var r = Request(); r.Tuning.AttackPower = 0; r.Tuning.BaseStrength = 0;
        r.Tuning.ShapeshiftMana = 0; r.Talents.Ranks["shredding-attacks"] = 3;
        r.Talents.Ranks["shifting-power"] = 1;
        var p = FeralDruidSimRunner.Preview(r); var run = new FeralDruidSimRunner().Run(r);
        Assert.Equal(0, p.AuthoredAttackPower); Assert.Equal(0, p.SpellbookAttackPower);
        Assert.Equal(0, p.InitialShiftManaCost); Assert.Equal(0, p.StartingEnergy);
        Assert.Equal(p.MaximumMana, p.ManaAfterInitialShift);
        Assert.Contains(run.Run.Timeline, e => e.AbilityKey == "shifting-power" &&
            e.TimeSeconds == 0 && e.Type == CombatEventType.ResourceChanged && e.Amount == 40);
    }

    [Fact]
    public void PreviewContainsConfiguredPriorityNotCombatEligibilityOrSharedMutableDefinitions()
    {
        var r = Request(); r.AdditionalTargets = 1; r.FormTransitions.Add(new() { AtSeconds = 9, Form = "bear" });
        r.UseBearUtility = true; r.UseFaerieFire = true;
        var before = Json(r); var p = FeralDruidSimRunner.Preview(r); var expected = Fingerprint(p);
        Assert.Equal("growl", p.ConfiguredRotation.Entries[0].AbilityKey);
        Assert.Contains(p.ConfiguredRotation.Entries, e => e.AbilityKey == "ravage" && e.IsEnabled);
        Assert.Contains(p.ConfiguredRotation.Entries, e => e.AbilityKey == "maul");
        var claw = p.Abilities.Single(a => a.Key == "claw");
        claw.ResourceCosts[0].Amount = 999;
        claw.Effects[0].MinimumValue = -999;
        claw.RequiredSourceAuraKeys.Clear();
        p.ConfiguredRotation.Entries[0].Target.ActorKey = "mutated";
        p.ConfiguredRotation.Entries[0].Conditions.Add(new());
        p.ConfiguredRotation.Entries[0].IsEnabled = false;
        p.ConfiguredRotation.Entries.Clear();
        Assert.Equal(expected, Fingerprint(FeralDruidSimRunner.Preview(r)));
        var run = new FeralDruidSimRunner().Run(r);
        Assert.Equal(expected, Fingerprint(FeralDruidSimRunner.Preview(r)));
        Assert.Equal(before, Json(r)); Assert.True(run.DamageDone > 0);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void SnapshotPreservesComparersInsertionOrderCaseSensitiveTalentMeaningAndNestedIsolation(bool insensitive)
    {
        var r = Request(); var comparer = insensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        r.Talents.Ranks = new(comparer) { ["HEART-OF-THE-WILD"] = 5, ["furor"] = 0 };
        r.InitialComboPointsByTarget = new(comparer) { ["target-1"] = 4, ["target"] = 5 };
        r.AdditionalTargets = 1; r.TargetTransitions.Add(new() { AtSeconds = 8, TargetKey = "target-1" });
        r.FormTransitions.Add(new() { AtSeconds = 9, Form = "bear" });
        var c = FeralDruidSimulationRequestSnapshot.Create(r);
        Assert.Same(comparer, c.Talents.Ranks.Comparer); Assert.Same(comparer, c.InitialComboPointsByTarget.Comparer);
        Assert.Equal(r.Talents.Ranks.Keys.ToArray(), c.Talents.Ranks.Keys.ToArray());
        Assert.Equal(r.InitialComboPointsByTarget.Keys.ToArray(), c.InitialComboPointsByTarget.Keys.ToArray());
        Assert.Equal(insensitive ? 836 : 800, FeralDruidSimRunner.Preview(r).SpellbookAttackPower);
        c.TargetTransitions[0].TargetKey = "target"; c.FormTransitions[0].Form = "caster";
        c.Talents.Ranks.Clear(); c.InitialComboPointsByTarget.Clear(); c.Tuning.AttackPower = 0;
        Assert.Equal("target-1", r.TargetTransitions[0].TargetKey); Assert.Equal("bear", r.FormTransitions[0].Form);
        Assert.Equal(2, r.Talents.Ranks.Count); Assert.Equal(2, r.InitialComboPointsByTarget.Count);
        Assert.Equal(800, r.Tuning.AttackPower);
    }

    public static IEnumerable<object[]> InvalidCases() => Enumerable.Range(0, 19).Select(i => new object[] { i });
    [Theory][MemberData(nameof(InvalidCases))]
    public void RunAndPreviewPreserveInvalidInputsAndValidationPrecedence(int kind)
    {
        var r = Request();
        switch (kind) {
            case 0: r = null!; break;
            case 1: r.Talents = null!; break;
            case 2: r.Tuning = null!; break;
            case 3: r.Talents.Ranks = null!; break;
            case 4: r.TargetTransitions = null!; break;
            case 5: r.TargetTransitions.Add(null!); break;
            case 6: r.FormTransitions = null!; break;
            case 7: r.FormTransitions.Add(null!); break;
            case 8: r.InitialComboPointsByTarget = null!; break;
            case 9: r.InitialComboPointsByTarget["TARGET"] = 1; break;
            case 10: r.Role = "CAT"; r.Talents.Ranks["invalid"] = 1; break;
            case 11: r.Talents.Ranks["invalid"] = 1; r.Tuning.AttackPower = -1; break;
            case 12: r.Tuning.AttackPower = -1; r.StartInProwl = true; r.TargetBehind = false; break;
            case 13: r.StartInProwl = true; r.TargetBehind = false; r.MaximumMana = 1; break;
            case 14: r.MaximumMana = 1; r.Talents.Ranks.Clear(); break;
            case 15: r.DurationSeconds = 0; break;
            case 16: r.FormTransitions.Add(new() { AtSeconds = 5, Form = "cat" }); break;
            case 17: r.TargetTransitions.Add(new() { AtSeconds = 2, TargetKey = null! }); break;
            case 18: r.Talents.Ranks["predatory-strikes"] = 4; break;
        }
        var before = Json(r!);
        var a = Assert.ThrowsAny<ArgumentException>(() => new FeralDruidSimRunner().Run(r!));
        var b = Assert.ThrowsAny<ArgumentException>(() => FeralDruidSimRunner.Preview(r!));
        Assert.Equal(a.GetType(), b.GetType()); Assert.Equal(a.Message, b.Message);
        Assert.Equal(before, Json(r!));
        if (kind == 0) { Assert.IsType<ArgumentNullException>(a); Assert.Equal("request", a.ParamName); Assert.Equal(new ArgumentNullException("request").Message, a.Message); }
        if (kind == 10) Assert.Equal("Invalid Feral simulation request.", a.Message);
        if (kind == 11) Assert.Equal("Unsupported Feral talent invalid=1", a.Message);
        if (kind == 12) Assert.Equal("Invalid Feral tuning.", a.Message);
        if (kind == 13) Assert.StartsWith("Ravage from Prowl requires", a.Message);
    }

    [Theory][InlineData("cat")][InlineData("bear")]
    public async Task RepeatedAndConcurrentRunPreviewCallsOwnTheirMutableState(string role)
    {
        var r = Request(role); r.AdditionalTargets = 1;
        r.TargetTransitions.Add(new() { AtSeconds = 8, TargetKey = "target-1" });
        r.FormTransitions.Add(new() { AtSeconds = 15, Form = role == "cat" ? "bear" : "cat" });
        var before = Json(r); var runner = new FeralDruidSimRunner();
        var expected = Fingerprint(runner.Run(r)); var preview = Fingerprint(FeralDruidSimRunner.Preview(r));
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => {
            Assert.Equal(expected, Fingerprint(runner.Run(r)));
            Assert.Equal(preview, Fingerprint(FeralDruidSimRunner.Preview(r)));
        })));
        Assert.Equal(before, Json(r));
    }
}
