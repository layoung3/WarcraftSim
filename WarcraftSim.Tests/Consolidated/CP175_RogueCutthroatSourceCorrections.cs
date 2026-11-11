using System.Reflection;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Historical/provisional 1cc5ae1 Cutthroat cap, chance and opportunity window.
/// Other hit, refresh, ownership and consumption behavior is a preserved local convention,
/// not current-client certification or strict Subtlety admission.</summary>
public sealed class Cp175RogueCutthroatSourceCorrections
{
    private static readonly decimal[] SourceThresholds = [0, 3, 6, 9, 12, 15];

    private static RogueSimRequest Request(int? rank = 1, string spec = "subtlety")
    {
        var r = RogueSimRequest.CreatePreset(spec);
        r.Talents = new() { AssassinationPointsSpent = 20, CombatPointsSpent = 15,
            SubtletyPointsSpent = 16, Ranks = new(StringComparer.OrdinalIgnoreCase) };
        if (rank.HasValue) r.Talents.Ranks["cutthroat"] = rank.Value;
        r.StartInStealth = false; r.Character.BaseStats = new();
        r.BothWeaponsDaggers = true; r.IsBehindTarget = true;
        r.MainHandWeaponType = r.OffHandWeaponType = "dagger";
        r.UseInstantPoison = false; r.MainHandPoison = r.OffHandPoison = "none";
        r.Tuning.MainHandWeaponDamage = 100; r.Tuning.OffHandWeaponDamage = 80;
        r.Tuning.MainHandSpeedSeconds = r.Tuning.OffHandSpeedSeconds = 2;
        r.Tuning.MainHandHitPercent = r.Tuning.OffHandHitPercent = 100;
        r.Tuning.PhysicalCritPercent = 0; r.Tuning.EnergyPerSecond = 60;
        r.TargetArmor = r.TargetDodgePercent = r.TargetParryPercent = 0;
        r.TargetMaximumHealth = 10000000; r.CaptureTimeline = true; r.DurationSeconds = 20;
        return r;
    }

    private static IReadOnlyList<AbilityDefinition> PreparedAbilities(RogueSimRequest r)
    {
        var p = typeof(RogueSimRunner).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [r])!;
        return (IReadOnlyList<AbilityDefinition>)p.GetType().GetProperty("Abilities")!.GetValue(p)!;
    }

    private sealed class Harness : ICombatEventProcessor
    {
        public RogueSimRequest Request { get; }
        public SimulationContext Context { get; }
        public SimulationActorState Actor => Context.GetActor("rogue")!;
        public SimulationActorState Target => Context.GetActor("target")!;
        public RogueCombatProcessor Combat { get; }
        public RoguePoisonProcessor Poisons { get; }
        public AbilityExecutor Executor { get; }
        public AuraManager Auras { get; } = new();
        public RotationProfile Rotation { get; } = new() { Entries = [
            new() { AbilityKey = "ambush", Priority = 1 },
            new() { AbilityKey = "vanish", Priority = 2, IsEnabled = false } ] };
        public List<CombatEvent> Dispatched { get; } = [];
        public SimulationRunResult? Result { get; private set; }
        private readonly Dictionary<string, Action> _actions = new();
        private readonly PriorityRotationExecutor _priority;
        public bool PriorityEnabled { get; set; }
        public Harness(RogueSimRequest r, decimal duration = 3, int seed = 35)
        {
            Request = r;
            Context = new(new() { Seed = seed, DurationSeconds = duration,
                CaptureTimeline = true, PrimaryActorKey = "rogue", MaximumProcessedEvents = 10000 });
            foreach (var key in new[] { "rogue", "other-rogue", "target", "target-2" })
            {
                var friendly = key.Contains("rogue", StringComparison.Ordinal);
                var actor = new SimulationActorState { Key = key, Name = key,
                    TeamKey = friendly ? "raid" : "enemy", Level = friendly ? 60 : 63,
                    MaximumHealth = 10000000 };
                actor.InitializeHealth(10000000);
                if (friendly)
                {
                    actor.AddResource(new ResourceState("energy", 100, 100, 0));
                    foreach (var ability in PreparedAbilities(r)) actor.AddAbility(ability);
                }
                Context.AddActor(actor);
            }
            RogueCombatProcessor? combat = null;
            Func<RogueCombatProcessor?> getCombat = () => combat;
            object Adapter(string name) => Activator.CreateInstance(
                typeof(RogueSimRunner).GetNestedType(name, BindingFlags.NonPublic)!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [r, getCombat], null)!;
            Executor = new(Auras, (ICombatRollResolver)Adapter("RogueRollResolver"),
                (IDamageMitigationResolver)Adapter("RogueArmorMitigation"));
            var auto = new AutoAttackProcessor(Executor);
            var weapons = new[] { new AutoAttackDefinition { Key = "test-main-hand",
                WeaponHandKey = WeaponHandKeys.MainHand, SwingIntervalSeconds = 2,
                DamageEffect = Actor.Abilities["sinister-strike"].Definition.Effects[0] } };
            Combat = combat = new(r, Actor, Rotation, Executor, Auras, auto, weapons);
            Poisons = new(r, Actor, Auras, Executor);
            _priority = new(Rotation, "rogue", "target", Executor);
        }
        public void At(decimal time, Action action)
        {
            var key = "test-" + _actions.Count; _actions.Add(key, action);
            Context.ScheduleEvent(new() { TimeSeconds = time, Type = CombatEventType.RotationDecision,
                SourceActorKey = "rogue", EffectKey = key, IsInternal = true });
        }
        public void Process(SimulationContext context, CombatEvent ev)
        {
            Dispatched.Add(ev);
            // Engine-isolation fixture: validated requests retain positive regeneration.
            // Explicitly suspend regeneration after each event so literal cast costs
            // and deliberate refill steps do not inherit elapsed passive Energy.
            Actor.Resources["energy"].RegenerationPerSecond = 0;
            if (ev.EffectKey is { } key && _actions.TryGetValue(key, out var action)) action();
            if (PriorityEnabled && ev.EffectKey is null) _priority.Process(context, ev);
        }
        public void Decide() => _priority.Process(Context, new() { Type = CombatEventType.RotationDecision,
            TimeSeconds = Context.CurrentTimeSeconds, SourceActorKey = "rogue", TargetActorKey = "target" });
        public void Cast(string ability, string target = "target", string source = "rogue") =>
            Assert.True(Executor.TryStartAbility(Context, source, target, ability).Success,
                $"{source} could not cast {ability} at {Context.CurrentTimeSeconds}");
        public void Refill() => Actor.Resources["energy"].SetCurrent(100);
        public CombatEvent[] Damage(string ability) => Context.Timeline.Where(e =>
            e.Type == CombatEventType.Damage && e.AbilityKey == ability).ToArray();
        public AuraInstance Opportunity() => Assert.Single(Actor.ActiveAuras,
            a => a.Definition.Key == "rogue-cutthroat");
        public bool HasOpportunity => Actor.ActiveAuras.Any(a => a.Definition.Key == "rogue-cutthroat" &&
            a.IsActiveAt(Context.CurrentTimeSeconds));
        public void Run() => Result = new SimulationEngine([Executor, Auras, Combat, Poisons, this]).Run(Context);
    }

    private static CombatEvent BackstabEvent(string actor = "rogue", string? target = "target",
        decimal amount = 235, bool critical = false, bool periodic = false, string ability = "backstab",
        string? hand = WeaponHandKeys.MainHand, string result = CombatResultTypes.Hit, Guid? execution = null) =>
        new() { Type = CombatEventType.Damage, SourceActorKey = actor, TargetActorKey = target,
            AbilityKey = ability, Amount = amount, RawAmount = amount, IsCritical = critical,
            EffectDeliveryType = periodic ? CombatEffectDeliveryType.Periodic : CombatEffectDeliveryType.Direct, SchoolKey = "physical", WeaponHandKey = hand,
            ResultKey = result, AbilityExecutionId = execution ?? Guid.NewGuid() };

    public static IEnumerable<object[]> RankSpecs()
    {
        foreach (var spec in new[] { "assassination", "combat", "subtlety" })
        for (var rank = 0; rank <= 5; rank++) yield return [spec, rank];
    }

    [Theory][MemberData(nameof(RankSpecs))]
    public void LegacyValidationAndBothSpellbookOverloadsAcceptEverySourceRank(string spec, int rank)
    {
        var r = Request(rank, spec); var before = JsonSerializer.Serialize(r);
        r.Talents.Validate(60, spec);
        var oldSignature = ForeverRogueSpellbook.Create(spec, r.Talents, r.Tuning, 0, true, true);
        var explicitHand = ForeverRogueSpellbook.Create(spec, r.Talents, r.Tuning, 0, true, true, true);
        Assert.Equal(JsonSerializer.Serialize(oldSignature), JsonSerializer.Serialize(explicitHand));
        Assert.Contains(oldSignature, x => x.Key == "ambush");
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }

    [Theory][MemberData(nameof(RankSpecs))]
    public void LegacyPreviewRunAndSavedRequestRoundTripAcceptSourceRanksWithoutMutation(string spec, int rank)
    {
        var r = Request(rank, spec); r.DurationSeconds = 2;
        r.Tuning.MutilateBonusDamagePerHand = 17.25m; r.Tuning.HemorrhageBonusDamage = 73;
        var before = JsonSerializer.Serialize(r);
        var saved = JsonSerializer.Deserialize<RogueSimRequest>(before)!;
        var copy = RogueSimulationRequestSnapshot.Create(r);
        Assert.Same(r.Talents.Ranks.Comparer, copy.Talents.Ranks.Comparer);
        Assert.NotSame(r.Talents.Ranks, copy.Talents.Ranks);
        Assert.Equal(rank, copy.Talents.Rank("CUTTHROAT"));
        Assert.Equal(rank, saved.Talents.Rank("cutthroat"));
        var preview = RogueSimRunner.Preview(r); var run = new RogueSimRunner().Run(r);
        Assert.Equal(spec, run.Specialization); Assert.Equal(100m, preview.MaximumEnergy);
        if (spec != "subtlety") Assert.DoesNotContain(preview.Rotation.Entries, x => x.AbilityKey == "backstab");
        Assert.Equal(before, JsonSerializer.Serialize(r));
        Assert.Equal(17.25m, saved.Tuning.MutilateBonusDamagePerHand);
        Assert.Equal(73m, saved.Tuning.HemorrhageBonusDamage);
        Assert.Equal(20, r.Talents.AssassinationPointsSpent); Assert.Equal(15, r.Talents.CombatPointsSpent);
        Assert.Equal(16, r.Talents.SubtletyPointsSpent);
    }

    [Fact]
    public void MetadataReportsFiveWhileDefaultsAndStrictCatalogsStillOmitCutthroat()
    {
        Assert.Equal(5, Assert.Single(ForeverRogueTalentBuild.SupportedTalentMetadata,
            x => x.Key == "cutthroat").MaxRank);
        foreach (var spec in new[] { "assassination", "combat", "subtlety" })
            Assert.DoesNotContain("cutthroat", ForeverRogueTalentBuild.Default(spec).Ranks.Keys);
        foreach (var spec in new[] { "assassination", "combat" })
            Assert.DoesNotContain(ForeverHistoricalCasterTalentCatalog.ForSpecialization("rogue", spec), x => x.Key == "cutthroat");
    }

    [Theory][InlineData(-1)][InlineData(6)]
    public void InvalidRanksRemainRejectedByEveryLegacyCapConsumer(int rank)
    {
        foreach (var spec in new[] { "assassination", "combat", "subtlety" })
        {
            var r = Request(rank, spec);
            Assert.Throws<ArgumentException>(() => r.Talents.Validate(60, spec));
            Assert.Throws<ArgumentException>(() => ForeverRogueSpellbook.Create(spec, r.Talents, r.Tuning, 0, true));
            Assert.Throws<ArgumentException>(() => ForeverRogueSpellbook.Create(spec, r.Talents, r.Tuning, 0, true, true, true));
            Assert.Throws<ArgumentException>(() => RogueSimRunner.Preview(r));
            Assert.Throws<ArgumentException>(() => new RogueSimRunner().Run(r));
        }
    }

    [Fact]
    public void RankSumIdentityAndMalformedInputRejectionsAreNotRelaxed()
    {
        var r = Request(3); r.Talents.SubtletyPointsSpent = 2;
        Assert.Throws<ArgumentException>(() => RogueSimRunner.Preview(r));
        r = Request(); r.Talents.Ranks["not-cutthroat"] = 1;
        Assert.Throws<ArgumentException>(() => new RogueSimRunner().Run(r));
        r = Request(); r.Character.SpecializationKey = "combat";
        Assert.Throws<ArgumentException>(() => RogueSimRunner.Preview(r));
        foreach (var token in new[] { "1.5", "\"three\"", "null" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ForeverRogueTalentBuild>(
                "{\"Ranks\":{\"cutthroat\":" + token + "}}"));
        var sensitive = Request(); sensitive.Talents.Ranks = new(StringComparer.Ordinal) { ["CUTTHROAT"] = 1 };
        var copy = RogueSimulationRequestSnapshot.Create(sensitive);
        Assert.Same(StringComparer.Ordinal, copy.Talents.Ranks.Comparer);
        Assert.Equal(0, copy.Talents.Rank("cutthroat"));
        Assert.Equal(1, copy.Talents.Rank("CUTTHROAT"));
    }

    public static IEnumerable<object[]> BoundarySeeds()
    {
        var cases = new[] { (1, 73056, true), (1, 22176, false), (2, 49022, true),
            (2, 87483, false), (3, 24988, true), (3, 63449, false),
            (4, 954, true), (4, 39415, false), (5, 66261, true), (5, 15381, false) };
        foreach (var (rank, seed, success) in cases)
        foreach (var critical in new[] { false, true }) yield return [rank, seed, success, critical];
    }

    [Theory][MemberData(nameof(BoundarySeeds))]
    public void IsolatedProcUsesLiteralNearBoundaryThresholdAndExactlyOneDraw(
        int rank, int seed, bool success, bool critical)
    {
        var oracle = new Random(seed); var percent = (decimal)oracle.NextDouble() * 100;
        Assert.InRange(Math.Abs(percent - SourceThresholds[rank]), 0m, .002m);
        Assert.Equal(success, percent < SourceThresholds[rank]);
        var h = new Harness(Request(rank), seed: seed);
        h.Combat.Process(h.Context, BackstabEvent(critical: critical));
        Assert.Equal(success, h.HasOpportunity);
        Assert.Equal(oracle.NextDouble(), h.Context.Random.NextDouble());
    }

    [Theory][InlineData(1, 954)][InlineData(2, 1)][InlineData(3, 1)]
    public void OldOnlyChanceBandsNoLongerGrantAnOpportunity(int rank, int seed)
    {
        var percent = (decimal)new Random(seed).NextDouble() * 100;
        Assert.InRange(percent, SourceThresholds[rank], 20m * rank - .000001m);
        var h = new Harness(Request(rank), seed: seed);
        h.Combat.Process(h.Context, BackstabEvent()); Assert.False(h.HasOpportunity);
    }

    [Theory][InlineData(null)][InlineData(0)]
    public void MissingAndZeroRankConsumeNoCutthroatDraw(int? rank)
    {
        var h = new Harness(Request(rank), seed: 35);
        h.Combat.Process(h.Context, BackstabEvent()); Assert.False(h.HasOpportunity);
        Assert.Equal(new Random(35).NextDouble(), h.Context.Random.NextDouble());
    }

    [Theory][InlineData("foreign")][InlineData("periodic")][InlineData("other-ability")]
    [InlineData("zero")][InlineData("negative")][InlineData("uppercase-key")]
    public void IneligibleSyntheticEventsDoNotDrawOrApply(string mode)
    {
        var h = new Harness(Request(), seed: 35);
        h.Combat.Process(h.Context, BackstabEvent(actor: mode == "foreign" ? "other-rogue" : "rogue",
            periodic: mode == "periodic", amount: mode == "zero" ? 0 : mode == "negative" ? -1 : 235,
            ability: mode == "other-ability" ? "sinister-strike" : mode == "uppercase-key" ? "BACKSTAB" : "backstab"));
        Assert.False(h.HasOpportunity); Assert.Equal(new Random(35).NextDouble(), h.Context.Random.NextDouble());
    }

    [Theory][InlineData("case-source")][InlineData("no-target")][InlineData("no-hand")]
    [InlineData("positive-miss-label")]
    public void ExistingPositiveEventFilterAddsNoNewTargetHandOrResultRestrictions(string mode)
    {
        var h = new Harness(Request(), seed: 35);
        h.Combat.Process(h.Context, BackstabEvent(actor: mode == "case-source" ? "RoGuE" : "rogue",
            target: mode == "no-target" ? null : "target", hand: mode == "no-hand" ? null : WeaponHandKeys.MainHand,
            result: mode == "positive-miss-label" ? CombatResultTypes.Miss : CombatResultTypes.Hit));
        Assert.True(h.HasOpportunity); Assert.Equal("rogue", h.Opportunity().SourceActorKey);
    }

    [Fact]
    public void ExistingProcDoesNotDeduplicatePositiveEventsButGeneratorPointsStillDo()
    {
        var h = new Harness(Request(3), seed: 35); var ev = BackstabEvent();
        h.Combat.Process(h.Context, ev); var first = h.Opportunity().InstanceId;
        h.Combat.Process(h.Context, ev);
        Assert.NotEqual(first, h.Opportunity().InstanceId); Assert.Equal(1, h.Combat.ComboPointsFor("target"));
        var oracle = new Random(35); oracle.NextDouble(); oracle.NextDouble();
        Assert.Equal(oracle.NextDouble(), h.Context.Random.NextDouble());
    }

    [Theory][InlineData(0, false)][InlineData(1, false)][InlineData(3, false)]
    [InlineData(5, false)][InlineData(0, true)][InlineData(1, true)][InlineData(3, true)][InlineData(5, true)]
    public void RealBackstabConsumesHitAvoidanceCriticalThenOneProcDraw(int rank, bool critical)
    {
        var r = Request(rank); r.Tuning.PhysicalCritPercent = critical ? 100 : 0;
        var h = new Harness(r); h.At(2, () => h.Cast("backstab")); h.Run();
        var hit = Assert.Single(h.Damage("backstab")); Assert.Equal(critical ? 470m : 235m, hit.Amount);
        var oracle = new Random(35); for (var i = 0; i < 3; i++) oracle.NextDouble();
        var expected = rank > 0 && (decimal)oracle.NextDouble() * 100 < SourceThresholds[rank];
        Assert.Equal(expected, h.HasOpportunity); Assert.Equal(oracle.NextDouble(), h.Context.Random.NextDouble());
        Assert.Equal(1, h.Combat.ComboPointsFor("target")); Assert.Equal(40m, h.Actor.Resources["energy"].Current);
    }

    [Fact]
    public void ColdBloodStillShortCircuitsOnlyTheCriticalDrawBeforeCutthroat()
    {
        var r = Request(); r.Talents.Ranks["cold-blood"] = 1; var h = new Harness(r);
        h.At(1, () => h.Auras.ApplyAura(h.Context, h.Actor,
            new() { Key = "cold-blood", DurationSeconds = 20 }, "rogue", "cold-blood", "cold-blood"));
        h.At(2, () => h.Cast("backstab")); h.Run();
        Assert.True(Assert.Single(h.Damage("backstab")).IsCritical);
        var oracle = new Random(35); oracle.NextDouble(); oracle.NextDouble();
        Assert.Equal((decimal)oracle.NextDouble() * 100 < 3m, h.HasOpportunity);
        Assert.Equal(oracle.NextDouble(), h.Context.Random.NextDouble());
        Assert.DoesNotContain(h.Actor.ActiveAuras, a => a.Definition.Key == "cold-blood");
    }

    [Fact]
    public void FullCastPreservesCutthroatHackExtraAttackPointAndPoisonDrawOrder()
    {
        var r = Request(); r.Talents.Ranks["hack-and-slash"] = 5;
        r.Talents.Ranks["puncturing-wounds"] = 3; r.Talents.Ranks["seal-fate"] = 5;
        // Explicit sword identity deliberately differs from the retained legacy dagger flag.
        r.MainHandWeaponType = "sword"; r.Tuning.PhysicalCritPercent = 100;
        r.UseInstantPoison = true; r.MainHandPoison = "instant"; r.PoisonChargesPerWeapon = 100;
        r.Tuning.InstantPoisonChancePercent = 100;
        var h = new Harness(r); h.At(2, () => h.Cast("backstab")); h.Run();
        Assert.True(h.HasOpportunity); Assert.Equal(1, h.Combat.HackAndSlashAttacks);
        Assert.Single(h.Damage("hack-and-slash-extra")); Assert.Equal(3, h.Combat.ComboPointsFor("target"));
        Assert.Equal(2, h.Poisons.Applications); Assert.Equal(2, h.Damage("instant-poison").Length);
        // Backstab hit/avoid/crit, Cutthroat, Hack, extra hit/avoid/crit,
        // Puncturing Wounds, Seal Fate, original poison chance/preservation/hit/crit,
        // then extra-attack poison chance/preservation/hit/crit: 18 draws.
        var oracle = new Random(35); for (var i = 0; i < 18; i++) oracle.NextDouble();
        Assert.Equal(oracle.NextDouble(), h.Context.Random.NextDouble());
    }

    [Theory][InlineData("miss", 1)][InlineData("dodge", 2)]
    public void ActualUnlandedBackstabConsumesOnlyResolverDrawsWithoutAProc(string outcome, int draws)
    {
        var r = Request(); if (outcome == "miss") r.Tuning.MainHandHitPercent = 0;
        else r.TargetDodgePercent = 100;
        var h = new Harness(r); h.At(2, () => h.Cast("backstab")); h.Run();
        var damage = Assert.Single(h.Damage("backstab")); Assert.Equal(outcome, damage.ResultKey);
        Assert.Equal(0m, damage.Amount); Assert.False(h.HasOpportunity);
        Assert.Equal(0, h.Combat.ComboPointsFor("target")); Assert.Equal(40m, h.Actor.Resources["energy"].Current);
        var oracle = new Random(35); for (var i = 0; i < draws; i++) oracle.NextDouble();
        Assert.Equal(oracle.NextDouble(), h.Context.Random.NextDouble());
    }

    [Theory][InlineData("11.999", true)][InlineData("12", false)][InlineData("12.001", false)]
    public void RealProcAtTwoHasHalfOpenTenSecondAmbushWindow(string at, bool allowed)
    {
        var time = decimal.Parse(at, System.Globalization.CultureInfo.InvariantCulture);
        var h = new Harness(Request(), 15); h.At(2, () => h.Cast("backstab"));
        h.At(time, () => {
            h.Refill(); h.Rotation.Entries[0].IsEnabled = true; // Deliberately stale UI/priority selection.
            Assert.Equal(allowed, h.Executor.TryStartAbility(h.Context, "rogue", "target", "ambush").Success);
        });
        h.Run(); Assert.Equal(allowed ? 1 : 0, h.Damage("ambush").Length);
        Assert.Equal(allowed ? 40m : 100m, h.Actor.Resources["energy"].Current);
        if (allowed) Assert.Equal(400m, Assert.Single(h.Damage("ambush")).Amount);
        Assert.Equal(allowed ? 2 : 1, h.Combat.ComboPointsFor("target"));
    }

    [Fact]
    public void RealProcRecordsLiteralApplicationAndExpiryTimes()
    {
        var h = new Harness(Request(), 4); h.At(2, () => h.Cast("backstab")); h.Run();
        Assert.Equal(2m, h.Opportunity().AppliedAtSeconds);
        Assert.Equal(12m, h.Opportunity().ExpiresAtSeconds);
    }

    [Fact]
    public void DurationOnlyAttemptInOldExtraWindowExecutesBeforeComparingTheOutcome()
    {
        var h = new Harness(Request(), 13.5m); var started = false;
        h.At(2, () => h.Cast("backstab"));
        h.At(13, () => { h.Refill(); started = h.Executor.TryStartAbility(
            h.Context, "rogue", "target", "ambush").Success; });
        h.Run();
        Assert.False(started, $"Actual duration-only Ambush attempt at 13: started={started}, " +
            $"damage={h.Damage("ambush").Sum(e => e.Amount ?? 0)}, energy={h.Actor.Resources["energy"].Current}, " +
            $"points={h.Combat.ComboPointsFor("target")}. No preceding duration assertion prevents execution.");
        Assert.Empty(h.Damage("ambush")); Assert.Equal(100m, h.Actor.Resources["energy"].Current);
        Assert.Equal(1, h.Combat.ComboPointsFor("target"));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void ExpiredObjectsOrExplicitRemovalCannotSatisfyTheLiveGate(bool retainExpired)
    {
        var h = new Harness(Request(), 13); h.At(2, () => h.Cast("backstab"));
        h.At(3, () => {
            if (retainExpired) h.Opportunity().InstanceId = Guid.NewGuid();
            else Assert.Equal(1, h.Auras.RemoveAuras(h.Context, h.Actor, "rogue-cutthroat", "rogue"));
        });
        h.At(retainExpired ? 12 : 4, () => {
            h.Refill(); Assert.False(h.Executor.TryStartAbility(h.Context, "rogue", "target", "ambush").Success);
            Assert.Equal(100m, h.Actor.Resources["energy"].Current);
        });
        h.Run(); Assert.Empty(h.Damage("ambush"));
        Assert.Equal(retainExpired, h.Actor.ActiveAuras.Any(a => a.Definition.Key == "rogue-cutthroat"));
    }

    [Fact]
    public void SecondRealProcRefreshesOneOpportunityAndOldExpiryCannotRemoveIt()
    {
        var h = new Harness(Request(), 16); Guid first = default, second = default;
        h.At(2, () => h.Cast("backstab")); h.At(3, () => first = h.Opportunity().InstanceId);
        h.At(5, () => { h.Refill(); h.Cast("backstab"); });
        h.At(6, () => { second = h.Opportunity().InstanceId; Assert.NotEqual(first, second);
            Assert.Equal(5m, h.Opportunity().AppliedAtSeconds); Assert.Equal(15m, h.Opportunity().ExpiresAtSeconds);
            Assert.Equal(1, h.Opportunity().Stacks); });
        h.At(12.001m, () => { Assert.Equal(second, h.Opportunity().InstanceId); Assert.True(h.HasOpportunity); });
        h.At(15.001m, () => Assert.False(h.HasOpportunity)); h.Run();
        var checks = h.Dispatched.Where(e => e.Type == CombatEventType.AuraExpiration && e.AbilityKey == "cutthroat").ToArray();
        Assert.Equal(new[] { 12m, 15m }, checks.Select(e => e.TimeSeconds));
        Assert.Equal(new[] { first, second }, checks.Select(e => e.AuraInstanceId!.Value));
        Assert.Single(h.Context.Timeline, e => e.Type == CombatEventType.AuraRemoved && e.AbilityKey == "cutthroat");
        Assert.DoesNotContain(h.Context.Timeline, e => e.Type == CombatEventType.AuraExpiration);
    }

    [Theory][InlineData("failed-chance")][InlineData("miss")][InlineData("ineligible")]
    public void FailedOrIneligibleFollowupDoesNotRefreshOrRemoveTheExistingOpportunity(string mode)
    {
        // Seed 124: fourth draw 2.508% succeeds; eighth 65.104% fails both old and new rank-one thresholds.
        var h = new Harness(Request(), 8, seed: 124); Guid first = default; decimal expiry = 0;
        h.At(2, () => h.Cast("backstab")); h.At(3, () => { first = h.Opportunity().InstanceId; expiry = h.Opportunity().ExpiresAtSeconds; });
        h.At(5, () => { h.Refill(); if (mode == "miss") h.Request.Tuning.MainHandHitPercent = 0;
            h.Cast(mode == "ineligible" ? "sinister-strike" : "backstab"); });
        h.At(6, () => { Assert.Equal(first, h.Opportunity().InstanceId); Assert.Equal(expiry, h.Opportunity().ExpiresAtSeconds); });
        h.Run(); Assert.True(h.HasOpportunity);
    }

    [Theory][InlineData("energy")][InlineData("input")][InlineData("gcd")][InlineData("target")]
    public void RejectedLiveAmbushSpendsNothingAndDoesNotConsumeOpportunity(string gate)
    {
        var h = new Harness(Request(), 6); h.At(2, () => h.Cast("backstab"));
        h.At(4, () => {
            h.Refill(); if (gate == "energy") h.Actor.Resources["energy"].SetCurrent(59);
            if (gate == "input") h.Actor.ConfigureActionTiming(5, 0);
            if (gate == "gcd") h.Actor.StartGlobalCooldown(4, 1);
            var before = h.Actor.Resources["energy"].Current; var id = h.Opportunity().InstanceId;
            Assert.False(h.Executor.TryStartAbility(h.Context, "rogue", gate == "target" ? "absent" : "target", "ambush").Success);
            Assert.Equal(before, h.Actor.Resources["energy"].Current); Assert.Equal(id, h.Opportunity().InstanceId);
        }); h.Run(); Assert.Empty(h.Damage("ambush")); Assert.Equal(0, h.Combat.StealthOpeners);
    }

    [Theory][InlineData(80)][InlineData(1000)]
    public void PendingDecisionReevaluatesAfterInputReadinessCrossesExpiry(int milliseconds)
    {
        var h = new Harness(Request(), 13.5m); h.At(2, () => h.Cast("backstab"));
        h.At(11.95m, () => {
            h.Refill(); h.Actor.ConfigureActionTiming(0, milliseconds / 1000m);
            h.Actor.RegisterActionStarted(h.Context.CurrentTimeSeconds, 0, 0);
            h.Rotation.Entries.RemoveAll(e => e.AbilityKey != "ambush");
            h.Rotation.Entries[0].IsEnabled = true; h.PriorityEnabled = true; h.Decide();
        }); h.Run();
        Assert.Empty(h.Damage("ambush")); Assert.Equal(100m, h.Actor.Resources["energy"].Current);
        Assert.Contains(h.Dispatched, e => e.Type == CombatEventType.RotationDecision &&
            e.EffectKey is null && e.TimeSeconds == 11.95m + milliseconds / 1000m);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void CompletedAmbushConsumesOneOpportunityEvenOnMissAndAwardsOnlyLandedPoints(bool miss)
    {
        var h = new Harness(Request(), 7); h.At(2, () => h.Cast("backstab"));
        h.At(4, () => { h.Refill(); if (miss) h.Request.Tuning.MainHandHitPercent = 0; h.Cast("ambush"); });
        h.At(6, () => { h.Refill(); Assert.False(h.HasOpportunity);
            Assert.False(h.Executor.TryStartAbility(h.Context, "rogue", "target", "ambush").Success); });
        h.Run(); var hit = Assert.Single(h.Damage("ambush"));
        Assert.Equal(miss ? 0m : 400m, hit.Amount); Assert.Equal(miss ? 1 : 2, h.Combat.ComboPointsFor("target"));
        Assert.Equal(1, h.Combat.StealthOpeners);
        Assert.DoesNotContain(h.Context.Timeline, e => e.AbilityKey is "opening-stealth" or "master-of-subtlety");
    }

    [Theory][InlineData(0, 0, 400)][InlineData(100, 0, 800)][InlineData(0, 5500, 200)]
    public void AmbushKeepsLiteralDamageEnergyAndCrossTargetPointOwnership(int crit, int armor, int amount)
    {
        var h = new Harness(Request(), 7); h.At(2, () => h.Cast("backstab"));
        h.At(4, () => { h.Refill(); h.Request.Tuning.PhysicalCritPercent = crit; h.Request.TargetArmor = armor;
            h.Cast("ambush", "target-2"); });
        h.At(4.5m, () => Assert.Equal(40m, h.Actor.Resources["energy"].Current));
        h.At(6, () => { h.Refill(); Assert.False(h.Executor.TryStartAbility(h.Context, "rogue", "target", "ambush").Success);
            Assert.False(h.Executor.TryStartAbility(h.Context, "rogue", "target-2", "ambush").Success); });
        h.Run(); Assert.Equal(235m, Assert.Single(h.Damage("backstab")).Amount);
        Assert.Equal(amount, Assert.Single(h.Damage("ambush")).Amount);
        Assert.Equal(1, h.Combat.ComboPointsFor("target")); Assert.Equal(1, h.Combat.ComboPointsFor("target-2"));
        Assert.False(h.HasOpportunity);
    }

    [Theory][InlineData("garrote")][InlineData("cheap-shot")]
    public void CutthroatDoesNotGrantOtherStealthOnlyAbilities(string ability)
    {
        var h = new Harness(Request(), 6); h.At(2, () => h.Cast("backstab"));
        h.At(4, () => { h.Refill(); Assert.False(h.Executor.TryStartAbility(h.Context, "rogue", "target", ability).Success);
            Assert.True(h.HasOpportunity); Assert.Equal(100m, h.Actor.Resources["energy"].Current); });
        h.Run(); Assert.Empty(h.Damage(ability));
    }

    [Theory][InlineData(false, false)][InlineData(false, true)][InlineData(true, false)]
    public void OpportunityCannotCreateAbilitiesRemovedByLegacyWeaponOrPositionGates(bool daggers, bool behind)
    {
        var r = Request(); r.BothWeaponsDaggers = daggers; r.IsBehindTarget = behind;
        var h = new Harness(r); h.Combat.Process(h.Context, BackstabEvent());
        Assert.True(h.HasOpportunity); Assert.DoesNotContain("ambush", h.Actor.Abilities.Keys);
        Assert.DoesNotContain("backstab", h.Actor.Abilities.Keys);
        Assert.False(h.Executor.TryStartAbility(h.Context, "rogue", "target", "ambush").Success);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void GenuineStealthIndependentlyEnablesAmbushAndPreservesBothAuraConsumption(bool expired)
    {
        var r = Request(); r.Talents.Ranks["master-of-subtlety"] = 3;
        var h = new Harness(r, expired ? 15 : 6); h.At(2, () => h.Cast("backstab"));
        h.At(expired ? 13 : 4, () => { h.Refill(); h.Auras.ApplyAura(h.Context, h.Actor,
            new() { Key = "rogue-stealth", DurationSeconds = 20 }, "rogue", "test-real-stealth", "stealth-state");
            h.Cast("ambush"); }); h.Run();
        Assert.Single(h.Damage("ambush")); Assert.False(h.HasOpportunity);
        Assert.DoesNotContain(h.Actor.ActiveAuras, a => a.Definition.Key == "rogue-stealth");
        Assert.Contains(h.Actor.ActiveAuras, a => a.Definition.Key == "master-of-subtlety-window");
    }

    [Fact]
    public void ForeignAuraGateAndOwnOnlyConsumptionRemainAnExplicitInProcessConvention()
    {
        var h = new Harness(Request(), 6); h.At(2, () => h.Auras.ApplyAura(h.Context, h.Actor,
            new() { Key = "rogue-cutthroat", DurationSeconds = 10 }, "other-rogue", "cutthroat", "constructed-foreign-aura"));
        h.At(3, () => h.Cast("ambush")); h.Run();
        Assert.Single(h.Damage("ambush")); Assert.Equal("other-rogue", h.Opportunity().SourceActorKey);
        Assert.True(h.HasOpportunity); Assert.Equal(1, h.Combat.StealthOpeners);
    }

    [Theory][InlineData("sinister-strike")][InlineData("eviscerate")][InlineData("rupture")]
    public void OtherAttacksAndFinishersDoNotConsumeTheOpportunity(string ability)
    {
        var h = new Harness(Request(), 7); h.At(2, () => h.Cast("backstab"));
        h.At(4, () => { h.Refill(); h.Cast(ability); }); h.Run(); Assert.True(h.HasOpportunity);
    }

    [Fact]
    public void RotationSuppressesVanishOnlyWhileOpportunityIsLiveWithoutCreatingStealthWindows()
    {
        var r = Request(); r.Talents.Ranks["master-of-subtlety"] = 3;
        var h = new Harness(r, 13); h.At(2, () => h.Cast("backstab"));
        h.At(3, () => { Assert.True(h.Rotation.Entries.Single(e => e.AbilityKey == "ambush").IsEnabled);
            Assert.False(h.Rotation.Entries.Single(e => e.AbilityKey == "vanish").IsEnabled); });
        h.At(12.001m, () => { Assert.False(h.Rotation.Entries.Single(e => e.AbilityKey == "ambush").IsEnabled);
            Assert.True(h.Rotation.Entries.Single(e => e.AbilityKey == "vanish").IsEnabled); });
        h.Run(); Assert.DoesNotContain(h.Context.Timeline, e => e.AbilityKey is "opening-stealth" or "master-of-subtlety");
        Assert.Equal(0, h.Combat.StealthOpeners);
    }

    [Fact]
    public void SharedSuccessHasExactDamageResourceTimelineAndOneEnumeratedStaleExpiry()
    {
        var h = new Harness(Request(), 15); Guid opportunity = default;
        h.At(2, () => h.Cast("backstab")); h.At(3, () => opportunity = h.Opportunity().InstanceId);
        h.At(4, () => { h.Refill(); h.Cast("ambush"); }); h.Run();
        Assert.Equal(new[] { (2m, "backstab", 235m), (4m, "ambush", 400m) },
            h.Context.Timeline.Where(e => e.Type == CombatEventType.Damage)
                .Select(e => (e.TimeSeconds, e.AbilityKey!, e.Amount!.Value)));
        Assert.Equal(new[] { (2m, -60m, 40m), (4m, -60m, 40m) },
            h.Context.Timeline.Where(e => e.Type == CombatEventType.ResourceChanged)
                .Select(e => (e.TimeSeconds, e.Amount!.Value, e.ResourceTotalAfter!.Value)));
        Assert.Equal(635m, h.Context.Summary.ActorSummaries["rogue"].DamageDone);
        Assert.Equal(2, h.Combat.ComboPointsFor("target")); Assert.Equal(40m, h.Actor.Resources["energy"].Current);
        var stale = Assert.Single(h.Dispatched, e => e.Type == CombatEventType.AuraExpiration && e.AbilityKey == "cutthroat");
        Assert.Equal(12m, stale.TimeSeconds); Assert.Equal(opportunity, stale.AuraInstanceId);
        Assert.Equal(4m, Assert.Single(h.Context.Timeline,
            e => e.Type == CombatEventType.AuraRemoved && e.AbilityKey == "cutthroat").TimeSeconds);
        Assert.False(h.HasOpportunity);
    }

    // Deliberately limited numerical projection. Complete raw-envelope replay and
    // bijective event-identity checks are provided by the separate HTTP gate.
    private static string NumericalProjection(RogueSimResponse r) => JsonSerializer.Serialize(new {
        r.Specialization, r.DamageDone, r.Dps, r.EndingEnergy, r.EndingComboPoints,
        r.MaximumHealth, r.EndingHealth, r.PoisonProcs, r.FinishersUsed, r.StealthOpeners,
        r.DamageByAbility, r.ModelingNotes, r.Run.ProcessedEventCount, r.Run.CustomMetrics,
        Events = r.Run.Timeline.Select(e => new { e.TimeSeconds, e.Type, e.SourceActorKey,
            e.TargetActorKey, e.AbilityKey, e.EffectKey, e.Amount, e.RawAmount, e.ResultKey,
            e.IsCritical, e.ResourceKey, e.ResourceTotalAfter, e.EffectDeliveryType }) });

    [Theory][InlineData(0)][InlineData(80)][InlineData(1000)]
    public void PositiveRunnerNumericalProjectionReplaysAndZeroMatchesRemovalAtEachLatency(int milliseconds)
    {
        var r = Request(3); r.Tuning.EnergyPerSecond = 60; r.DurationSeconds = 120; r.Seed = 35;
        var before = JsonSerializer.Serialize(r); var previous = SimulationInputTimingScope.AdditionalDelaySeconds;
        try
        {
            SimulationInputTimingScope.AdditionalDelaySeconds = milliseconds / 1000m;
            var first = new RogueSimRunner().Run(r); var second = new RogueSimRunner().Run(r);
            Assert.Equal(NumericalProjection(first), NumericalProjection(second)); Assert.Equal(before, JsonSerializer.Serialize(r));
            Assert.Contains(first.Run.Timeline, e => e.Type == CombatEventType.Damage && e.AbilityKey == "backstab" && e.Amount > 0);
            Assert.Contains(first.Run.Timeline, e => e.Type == CombatEventType.AuraApplied && e.AbilityKey == "cutthroat");
            Assert.Contains(first.Run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "ambush");
            r.Talents.Ranks.Remove("cutthroat"); var removed = new RogueSimRunner().Run(r);
            r.Talents.Ranks["cutthroat"] = 0; Assert.Equal(NumericalProjection(removed), NumericalProjection(new RogueSimRunner().Run(r)));
        }
        finally { SimulationInputTimingScope.AdditionalDelaySeconds = previous; }
    }
}
