using System.Text.Json;
using System.Text.Json.Serialization;
using WarcraftSim.Data.Forever.Druid;

namespace WarcraftSim.Api.Simulation;

// A separate input contract prevents legacy inclusive totals/defaults from
// competing with the historical baseline. Unknown JSON is rejected at every
// typed nesting level; this is not a new combat mode or character stat engine.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed class FeralHistoricalCharacterInput
{
    public string ClassKey { get; set; } = "druid";
    public string SpecializationKey { get; set; } = "feral";
    public string RulesetKey { get; set; } = "wow-forever";
    public int Level { get; set; } = 60;
    public string Race { get; set; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed class FeralCatManualInputs
{
    // REQUIRED total Cat AP before HOTW/Predatory, inclusive of all ordinary
    // attributes and caller-assumed Cat contribution. This is not an observation.
    public decimal? PreTalentCatAttackPower { get; set; }
    public decimal WeaponDps { get; set; } = 75m;
    public decimal MeleeHitPercent { get; set; } = 88m;
    public decimal MeleeCritPercent { get; set; } = 20m;
    public decimal ShapeshiftMana { get; set; } = 250m;
    public decimal ShiftingPowerBaseMana { get; set; } = 1244m;
    public decimal ShiftingPowerCostReductionPercent { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed class FeralHistoricalTalentInput
{
    public Dictionary<string,int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(FeralDeclaredPointsConverter))]
    public int? BalancePointsSpent { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(FeralDeclaredPointsConverter))]
    public int? FeralPointsSpent { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(FeralDeclaredPointsConverter))]
    public int? RestorationPointsSpent { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(FeralDeclaredPointsConverter))]
    public int? TotalPoints { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(FeralDeclaredPointsConverter))]
    public int? UnspentPoints { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[JsonNumberHandling(JsonNumberHandling.Strict)]
public sealed class FeralHistoricalSimRequest
{
    public FeralHistoricalCharacterInput Character { get; set; } = new();
    public string Role { get; set; } = "cat";
    public bool UseProvisionalLevel60Baseline { get; set; }
    public bool UseReviewedLevel60Baseline { get; set; }
    // Deliberately rejected: generic observations cannot prove Cat reference state.
    public ForeverLevel60BaselineObservation? Level60Baseline { get; set; }
    public FeralCatManualInputs ManualInputs { get; set; } = new();
    public FeralHistoricalTalentInput Talents { get; set; } = new();
    public int Seed { get; set; } = 144001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; }
    public int TargetLevel { get; set; } = 63;
    public decimal TargetInitialHealthPercent { get; set; } = 100m;
    public decimal TargetMaximumHealth { get; set; } = 1000000m;
    public decimal TargetArmor { get; set; }
    public bool StartInProwl { get; set; }
    public string PreferredCatOpener { get; set; } = "ravage";
    public bool TargetBehind { get; set; } = true;
    public bool UseFaerieFire { get; set; }
    public bool UseFeralCooldowns { get; set; }
}

// Absent optional declarations are legal; explicitly supplied null/string/fraction
// totals must not disappear during typed binding before shared validation.
public sealed class FeralDeclaredPointsConverter : JsonConverter<int?>
{
    public override bool HandleNull => true;
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if(reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var value))
            throw new JsonException("Declared Feral talent totals must be JSON integers when supplied.");
        return value;
    }
    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if(value.HasValue)writer.WriteNumberValue(value.Value);
        else writer.WriteNullValue();
    }
}

public sealed class FeralHistoricalPreparation
{
    public string InputMode { get; init; } = "historical-primary-resource-proxy-plus-manual-pre-talent-cat-ap";
    public FeralHistoricalSimRequest Request { get; init; } = new();
    public ForeverLevel60BaselineObservation Baseline { get; init; } = new();
    public FeralDruidSimRequest DirectRequest { get; init; } = new();
    public FeralDruidPrecombatPreview Preview { get; init; } = new();
    public IReadOnlyList<string> ModelingNotes { get; init; } = [];
}

public sealed class FeralHistoricalSimResult
{
    public FeralHistoricalPreparation Preparation { get; init; } = new();
    public FeralDruidSimResponse Simulation { get; init; } = new();
}

/// <summary>Own and adapt historical primary/resource inputs, then reuse the
/// exact legacy Feral preparation. Never call generic Druid Generate/AP conversion.</summary>
public static class FeralHistoricalInputAdapter
{
    public static IReadOnlyList<string> ModelingNotes { get; } = Array.AsReadOnly(new[] {
        "Fixed Cat only, level 60 against one full-health level-63 target; no form/target transitions, seeded combo points, adds or incoming attacks. This is a partial historical/manual scenario, not a complete Feral character or optimized build.",
        "The embedded historical baseline owns all five primary attributes and pre-talent maximum Health/Mana. No independent attributes, resources, gear, enchant, buff or modifier contributions are accepted. Historical values are not verified Forever naked observations; Troll/Druid is a lower-confidence extrapolated pairing.",
        "Manual pre-talent total Cat Attack Power includes ordinary attributes and assumed Cat contribution. Only incremental Heart of the Wild and sourced Predatory Strikes are then added, once, by existing preparation. No generic Druid AP reconstruction, ordinary Strength/Agility AP, invented Cat base AP or Agility crit is added.",
        "Manual weapon DPS, hit, crit, shift cost and Shifting Power base Mana remain explicit assumptions. Base Mana is separate from baseline or talent-adjusted maximum Mana. Existing damage coefficients and rotation policy remain provisional.",
        "IncludesRacialPassives remains false. Racial passives and actives are unavailable: scenario Health omits Tauren Endurance, including its Health-cap bonus; racial-complete caps or critical bonuses are not claimed.",
        "Predatory Strikes adds 30/60/90 AP at level 60 after HOTW. Existing AP consumers are Claw, Shred, Rake impact/ticks and Swipe; white attacks, Ravage, Rip, Ferocious Bite, Maul, Primal Bite, Pounce and Lacerate have no added AP coefficient.",
        "The inherited Omen processor executes a provisional 6% chance with a 10-second lockout, without a selectable Omen rank in this contract. Whether it should be baseline or talent-dependent is unresolved; absence from the retained talent tree does not settle eligibility.",
        "Natural Instinct retains provisional white/periodic critical eligibility and no healing model. Admission does not certify complete source effects.",
        "No observed Cat AP is accepted without explicit form, naked/unbuffed, zero-talent reference-state evidence. Manual AP never becomes a baseline observation. No mixed-form HOTW correctness or complete tooltip behavior is claimed."
    });

    public static FeralHistoricalPreparation Preview(FeralHistoricalSimRequest request) => Prepare(request);

    public static FeralHistoricalPreparation Prepare(FeralHistoricalSimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var copy = JsonSerializer.Deserialize<FeralHistoricalSimRequest>(JsonSerializer.Serialize(request))!;
        if (request.Talents?.Ranks is { } ranks)
            copy.Talents.Ranks = new(ranks, ranks.Comparer);
        if (copy.Character is not { ClassKey: "druid", SpecializationKey: "feral", Level: 60, RulesetKey: "wow-forever" } character ||
            character.Race is not ("night-elf" or "tauren" or "troll") || copy.Role != "cat" ||
            copy.TargetLevel != 63 || copy.TargetInitialHealthPercent != 100m ||
            copy.Talents?.Ranks is null || copy.ManualInputs is null)
            throw new ArgumentException("Historical Feral requires canonical level-60 Druid/Feral identity, a supported canonical race, fixed Cat and one full-health level-63 target with talents and manual inputs.");
        ForeverPlayableRaceCatalog.Validate(character.Race, character.ClassKey, character.Level);
        if (copy.Level60Baseline is not null)
            throw new ArgumentException("Historical Feral accepts only embedded historical Druid baselines, not inline observations or observed Cat AP.");
        var baseline = ForeverLevel60Baseline.Resolve(null, copy.UseReviewedLevel60Baseline,
            character.Race, character.ClassKey, copy.UseProvisionalLevel60Baseline) ??
            throw new ArgumentException("Historical Feral requires explicit historical-provisional baseline opt-in.");
        // Never return mutable catalog objects, or share request/talent ownership.
        baseline = JsonSerializer.Deserialize<ForeverLevel60BaselineObservation>(JsonSerializer.Serialize(baseline))!;
        // Resolve alone does not compare selected identity. Retain the identity
        // check normally performed by Generate, without using its AP conversions.
        ForeverLevel60Baseline.Validate(baseline);
        if (baseline.Race != character.Race || baseline.ClassKey != character.ClassKey || baseline.Level != character.Level ||
            baseline.VerificationStatus != "provisional-documented-proxy" || baseline.SourceKind != "historical-classic-stat-proxy" ||
            baseline.IncludesRacialPassives || baseline.ObservedUnbuffedMeleeAttackPower is not null ||
            baseline.ObservedUnbuffedMaximumHealth is null || baseline.ObservedUnbuffedMaximumMana is null)
            throw new ArgumentException("Historical Feral baseline must match selected race/class/level and the embedded historical primary/resource-only contract.");
        var manual = copy.ManualInputs;
        if (manual.PreTalentCatAttackPower is null)
            throw new ArgumentException("Historical Feral requires explicit manual preTalentCatAttackPower before HOTW and Predatory Strikes, inclusive of ordinary attributes and assumed Cat contribution.");
        var direct = new FeralDruidSimRequest {
            Seed = copy.Seed, DurationSeconds = copy.DurationSeconds, CaptureTimeline = copy.CaptureTimeline,
            Role = "cat", MaximumHealth = baseline.ObservedUnbuffedMaximumHealth.Value,
            MaximumMana = baseline.ObservedUnbuffedMaximumMana.Value,
            TargetMaximumHealth = copy.TargetMaximumHealth, TargetArmor = copy.TargetArmor,
            StartInProwl = copy.StartInProwl, PreferredCatOpener = copy.PreferredCatOpener,
            TargetBehind = copy.TargetBehind, UseFaerieFire = copy.UseFaerieFire, UseFeralCooldowns = copy.UseFeralCooldowns,
            Talents = new ForeverFeralDruidTalents { Ranks = new(copy.Talents.Ranks, copy.Talents.Ranks.Comparer) },
            Tuning = new ForeverFeralDruidTuning {
                AttackPower = manual.PreTalentCatAttackPower.Value, BaseStrength = baseline.Strength,
                BaseStamina = baseline.Stamina, BaseIntellect = baseline.Intellect,
                WeaponDps = manual.WeaponDps, MeleeHitPercent = manual.MeleeHitPercent,
                MeleeCritPercent = manual.MeleeCritPercent, ShapeshiftMana = manual.ShapeshiftMana,
                ShiftingPowerBaseMana = manual.ShiftingPowerBaseMana,
                ShiftingPowerCostReductionPercent = manual.ShiftingPowerCostReductionPercent
            }
        };
        return new FeralHistoricalPreparation { Request = copy, Baseline = baseline,
            DirectRequest = direct, Preview = FeralDruidSimRunner.Preview(direct), ModelingNotes = ModelingNotes };
    }

    public static FeralHistoricalSimResult Run(FeralHistoricalSimRequest request, CancellationToken token = default)
    {
        var prepared = Prepare(request);
        var result = new FeralDruidSimRunner().Run(prepared.DirectRequest, token);
        return new FeralHistoricalSimResult { Preparation = prepared, Simulation = result };
    }
}
