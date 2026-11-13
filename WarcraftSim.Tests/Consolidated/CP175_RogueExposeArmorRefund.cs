using System.Reflection;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Historical pinned rogue.json says refunds 1/2 after five points spent.
/// Independent literal oracles, not current-client certification. Explicit 0..5
/// remains an absolute override, including previously exported default 5.</summary>
public sealed class Cp175RogueExposeArmorRefund
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PublicOverrideTypeAndSerializedDefaultAreExplicitlyNullable()
    {
        Assert.Equal(typeof(int?), typeof(ForeverRogueTuning)
            .GetProperty(nameof(ForeverRogueTuning.ImprovedExposeArmorFivePointRefund))!.PropertyType);
        var tuning = new ForeverRogueTuning();
        Assert.Null(tuning.ImprovedExposeArmorFivePointRefund);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(tuning, Web));
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("improvedExposeArmorFivePointRefund").ValueKind);
    }

    private static RogueSimRequest Request(int rank, string refund = "missing")
    {
        var r = RogueSimRequest.CreatePreset("assassination");
        r.Talents = new() { AssassinationPointsSpent = 21,
            Ranks = new() { ["improved-expose-armor"] = rank } };
        r.Tuning = JsonSerializer.Deserialize<ForeverRogueTuning>(refund == "missing" ? "{}" :
            "{\"improvedExposeArmorFivePointRefund\":" + refund + "}", Web)!;
        r.StartInStealth = false; r.UseInstantPoison = false; r.UseExposeArmor = true;
        r.Character.BaseStats = new(); r.Tuning.PhysicalCritPercent = 0;
        r.Tuning.MainHandHitPercent = 100; r.Tuning.OffHandHitPercent = 100;
        r.TargetArmor = 5500; r.TargetMaximumHealth = 10000000;
        r.CaptureTimeline = true; r.DurationSeconds = 120;
        return r;
    }

    // Reuse the real runner's adapters without changing production visibility.
    private sealed class Harness
    {
        public SimulationContext Context { get; }
        public SimulationActorState Actor { get; }
        public RogueCombatProcessor Combat { get; }
        public AbilityExecutor Executor { get; }
        public AuraManager Auras { get; } = new();
        public Harness(RogueSimRequest r, int seed = 1)
        {
            Context = new(new() { Seed = seed, DurationSeconds = .5m,
                CaptureTimeline = true, PrimaryActorKey = "rogue" });
            Actor = new() { Key = "rogue", TeamKey = "raid", Level = 60, MaximumHealth = 4000 };
            Actor.InitializeHealth(4000); Actor.AddResource(new ResourceState("energy", 100, 100));
            Context.AddActor(Actor);
            foreach (var key in new[] { "target", "target-2" })
            {
                var target = new SimulationActorState { Key = key, TeamKey = "enemy", Level = 63,
                    MaximumHealth = 100000 };
                target.InitializeHealth(100000); Context.AddActor(target);
            }
            RogueCombatProcessor? combat = null;
            Func<RogueCombatProcessor?> getCombat = () => combat;
            object Adapter(string name) => Activator.CreateInstance(
                typeof(RogueSimRunner).GetNestedType(name, BindingFlags.NonPublic)!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [r, getCombat], null)!;
            Executor = new(Auras, (ICombatRollResolver)Adapter("RogueRollResolver"),
                (IDamageMitigationResolver)Adapter("RogueArmorMitigation"));
            Combat = combat = new(r, Actor, new RotationProfile(), Executor, Auras,
                new AutoAttackProcessor(Executor), []);
            foreach (var ability in ForeverRogueSpellbook.Create(r.Specialization, r.Talents,
                r.Tuning, 0, true)) Actor.AddAbility(ability);
        }
        public void Points(int count, string target = "target", string source = "rogue")
        {
            for (var i = 0; i < count; i++) Combat.Process(Context, new() {
                Type = CombatEventType.Damage, SourceActorKey = source,
                TargetActorKey = target, AbilityKey = "sinister-strike",
                AbilityExecutionId = Guid.NewGuid(), Amount = 100, ResultKey = CombatResultTypes.Hit });
        }
        public void Delivery(CombatEventType type, Guid? id, string target = "target", string source = "rogue",
            string ability = "expose-armor") => Combat.Process(Context, new() {
                Type = type, SourceActorKey = source, TargetActorKey = target,
                AbilityKey = ability, AbilityExecutionId = id });
        public SimulationRunResult Cast(string key = "expose-armor", string target = "target", ICombatEventProcessor? observer = null) =>
            new SimulationEngine(observer is null ? [Executor, Auras, Combat] : [Executor, Auras, Combat, observer]).Run(Context, ctx =>
                Assert.True(Executor.TryStartAbility(ctx, "rogue", target, key).Success));
    }

    public static IEnumerable<object[]> RefundCases()
    {
        foreach (var refund in new[] { "missing", "null", "0", "1", "2", "3", "4", "5" })
            for (var rank = 0; rank <= 2; rank++)
                for (var spent = 0; spent <= 5; spent++)
                    yield return [rank, spent, refund,
                        rank == 0 || spent != 5 ? 0 : refund is "missing" or "null" ?
                            new[] { 0, 1, 2 }[rank] : int.Parse(refund)];
    }

    [Theory][MemberData(nameof(RefundCases))]
    public void RefundMatrixUsesOnlyPositiveRankAndFiveActuallySpentPoints(int rank, int spent, string refund, int expected)
    {
        var h = new Harness(Request(rank, refund)); h.Points(spent);
        h.Points(2, "target-2"); h.Points(1, source: "other-rogue");
        var run = h.Cast();
        Assert.Equal(expected, h.Combat.ComboPointsFor("target"));
        Assert.Equal(2, h.Combat.ComboPointsFor("target-2"));
        Assert.Equal(spent, h.Combat.ExposeArmorPoints);
        Assert.Equal(spent == 0 ? 0 : 1, h.Combat.FinishersUsed);
        // Literal existing cost oracle 25/20/15, independent of refund override.
        var cost = new[] { 25m, 20m, 15m }[rank];
        Assert.Contains(run.Timeline, e => e.Type == CombatEventType.ResourceChanged &&
            e.ResourceKey == "energy" && e.Amount == -cost);
        Assert.Equal(100m - cost + 5m, h.Actor.Resources["energy"].Current);
        Assert.Contains(run.Timeline, e => e.Type == CombatEventType.AuraApplied &&
            e.AbilityKey == "expose-armor" && e.SourceActorKey == "rogue" && e.TargetActorKey == "target");
        // Redelivering both cast and aura must not spend/refund another time.
        var cast = Assert.Single(run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "expose-armor");
        h.Delivery(CombatEventType.AbilityCastCompleted, cast.AbilityExecutionId);
        h.Delivery(CombatEventType.AuraApplied, cast.AbilityExecutionId);
        h.Delivery(CombatEventType.AuraApplied, cast.AbilityExecutionId);
        Assert.Equal(expected, h.Combat.ComboPointsFor("target"));
        Assert.Equal(spent == 0 ? 0 : 1, h.Combat.FinishersUsed);
    }

    [Theory][InlineData("missing")][InlineData("null")][InlineData("0")][InlineData("5")]
    public void ForeignSourceWrongTargetAndMissingExecutionCannotClaimARefund(string refund)
    {
        var h = new Harness(Request(2, refund)); h.Points(5); h.Points(4, "target-2");
        var id = Guid.NewGuid();
        h.Delivery(CombatEventType.AbilityCastCompleted, id, source: "other");
        h.Delivery(CombatEventType.AuraApplied, id, source: "other");
        h.Delivery(CombatEventType.AbilityCastCompleted, null);
        h.Delivery(CombatEventType.AuraApplied, null);
        h.Delivery(CombatEventType.AbilityCastCompleted, id);
        h.Delivery(CombatEventType.AuraApplied, id, target: "target-2");
        Assert.Equal(5, h.Combat.ComboPointsFor("target"));
        Assert.Equal(4, h.Combat.ComboPointsFor("target-2")); Assert.Equal(0, h.Combat.FinishersUsed);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void MissCostsEnergyButDoesNotSpendOrRefundPoints(int rank)
    {
        var r = Request(rank); r.Tuning.MainHandHitPercent = 0;
        var h = new Harness(r); h.Points(5); var run = h.Cast();
        Assert.Equal(5, h.Combat.ComboPointsFor("target"));
        Assert.Equal(0, h.Combat.ExposeArmorPoints); Assert.Equal(0, h.Combat.FinishersUsed);
        Assert.DoesNotContain(run.Timeline, e => e.Type == CombatEventType.AuraApplied && e.AbilityKey == "expose-armor");
        // Missed aura applications do not emit a separate damage/miss event.
        Assert.Contains(run.Timeline, e => e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "expose-armor");
        Assert.Contains(run.Timeline, e => e.Type == CombatEventType.ResourceChanged && e.Amount == -new[] { 25m, 20m, 15m }[rank]);
    }

    private sealed class OnApplied(Action<SimulationContext> action) : ICombatEventProcessor
    {
        public void Process(SimulationContext context, CombatEvent ev)
        {
            if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "expose-armor") action(context);
        }
    }

    [Theory][InlineData(1,"missing")][InlineData(2,"missing")]
    [InlineData(2,"0")][InlineData(2,"5")]
    public void ArmorUsesFiveSpentNotRefundedPointsAndOnlyTheExposedTarget(int rank, string refund)
    {
        var h = new Harness(Request(rank, refund)); h.Points(5);
        var run = h.Cast(observer: new OnApplied(ctx => {
            foreach (var key in new[] { "target", "target-2" })
                h.Executor.ExecuteBackgroundDirectDamage(ctx, h.Actor, ctx.GetActor(key)!,
                    "armor-probe", "Controlled physical hit", new() {
                        Key = "probe", EffectType = AbilityEffectTypes.DirectDamage,
                        TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
                        ResolutionType = CombatResolutionTypes.AlwaysHits, CanMiss = false,
                        CanCrit = false, MinimumValue = 100m, MaximumValue = 100m });
        }));
        Assert.Equal(5, h.Combat.ExposeArmorPoints);
        var exposed = Assert.Single(run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "armor-probe" && e.TargetActorKey == "target");
        var other = Assert.Single(run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.AbilityKey == "armor-probe" && e.TargetActorKey == "target-2");
        // Provisional armor formula is held fixed: 5500 - 5*250 = 4250 armor.
        Assert.Equal(100m, exposed.RawAmount);
        Assert.Equal(100m - 100m * (4250m / 9750m), exposed.Amount);
        Assert.Equal(50m, other.Amount);
        Assert.Empty(h.Context.GetActor("target-2")!.ActiveAuras);
    }

    [Theory][InlineData("missing",true,3)][InlineData("missing",false,2)]
    [InlineData("0",true,1)][InlineData("0",false,0)][InlineData("5",true,5)][InlineData("5",false,5)]
    public void RuthlessnessRollIsSeparateAndRefundPlusProcIsCapped(string refund, bool proc, int expected)
    {
        var seed = Enumerable.Range(1, 1000).First(s => (new Random(s).NextDouble() < .2) == proc);
        var r = Request(2, refund); r.Talents.Ranks["ruthlessness"] = 1;
        var h = new Harness(r, seed); h.Points(5); var id = Guid.NewGuid();
        h.Delivery(CombatEventType.AbilityCastCompleted, id); h.Delivery(CombatEventType.AuraApplied, id);
        Assert.Equal(expected, h.Combat.ComboPointsFor("target"));
        var probe = new Random(seed); probe.NextDouble();
        Assert.Equal(probe.NextDouble(), h.Context.Random.NextDouble());
    }

    [Theory][InlineData("missing")][InlineData("null")][InlineData("0")][InlineData("1")][InlineData("2")][InlineData("5")]
    public async Task SerializerSnapshotPreviewAndConcurrentRunsPreserveCallerOverride(string refund)
    {
        var r = Request(2, refund); r.DurationSeconds = 12;
        var expected = refund is "missing" or "null" ? (int?)null : int.Parse(refund);
        Assert.Equal(expected, (int?)r.Tuning.ImprovedExposeArmorFivePointRefund);
        var before = JsonSerializer.Serialize(r, Web);
        var roundtrip = JsonSerializer.Deserialize<RogueSimRequest>(before, Web)!;
        Assert.Equal(expected, (int?)roundtrip.Tuning.ImprovedExposeArmorFivePointRefund);
        var snapshot = RogueSimulationRequestSnapshot.Create(r);
        Assert.NotSame(r.Tuning, snapshot.Tuning);
        Assert.Equal(expected, (int?)snapshot.Tuning.ImprovedExposeArmorFivePointRefund);
        snapshot.Tuning.ImprovedExposeArmorFivePointRefund = 3;
        Assert.Equal(before, JsonSerializer.Serialize(r, Web));
        var expectedRun = Stable(new RogueSimRunner().Run(r));
        var expectedPreview = JsonSerializer.Serialize(RogueSimRunner.Preview(r));
        var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() => {
            Assert.Equal(expectedPreview, JsonSerializer.Serialize(RogueSimRunner.Preview(r)));
            return Stable(new RogueSimRunner().Run(r));
        }));
        Assert.All(await Task.WhenAll(tasks), run => Assert.Equal(expectedRun, run));
        Assert.Equal(before, JsonSerializer.Serialize(r, Web));
    }

    [Theory][InlineData(-1)][InlineData(6)]
    public void InvalidAbsoluteValuesFailPreviewAndRunWithoutMutation(int value)
    {
        var r = Request(1, value.ToString()); var before = JsonSerializer.Serialize(r);
        Assert.Throws<ArgumentException>(() => RogueSimRunner.Preview(r));
        Assert.Throws<ArgumentException>(() => new RogueSimRunner().Run(r));
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }

    [Theory][InlineData("1.5")][InlineData("\"1.5\"")]
    [InlineData("\"wrong\"")][InlineData("true")][InlineData("[]")][InlineData("{}")]
    public void WrongJsonTypesAreRejectedEvenWithWebSerializerDefaults(string json) =>
        Assert.Throws<JsonException>(() => Request(1, json));

    [Theory][InlineData("\"0\"",0)][InlineData("\"5\"",5)]
    public void WebNumericStringsKeepExistingAbsoluteOverrideCompatibility(string json, int expected)
    {
        var r = Request(2, json);
        Assert.Equal(expected, (int?)r.Tuning.ImprovedExposeArmorFivePointRefund);
        var h = new Harness(r); h.Points(5); h.Cast();
        Assert.Equal(expected, h.Combat.ComboPointsFor("target"));
    }

    [Theory][InlineData("eviscerate")][InlineData("rupture")][InlineData("slice-and-dice")]
    public void ExposeArmorOverrideDoesNotRefundOtherFinishers(string ability)
    {
        var h = new Harness(Request(2, "5")); h.Points(5); h.Cast(ability);
        Assert.Equal(0, h.Combat.ComboPointsFor("target"));
        Assert.Equal(1, h.Combat.FinishersUsed); Assert.Equal(0, h.Combat.ExposeArmorPoints);
    }

    private static string Stable(RogueSimResponse r) => JsonSerializer.Serialize(new {
        r.DamageDone, r.Dps, r.EndingEnergy, r.EndingComboPoints, r.FinishersUsed,
        r.DamageByAbility, r.Run.ProcessedEventCount, r.Run.CustomMetrics,
        Timeline = r.Run.Timeline.Select(e => new { e.TimeSeconds, e.Type,
            e.SourceActorKey, e.TargetActorKey, e.AbilityKey, e.EffectKey, e.Amount,
            e.RawAmount, e.MitigatedAmount, e.ResultKey, e.ResourceKey, e.ResourceTotalAfter }) });

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void ActualRotationDefaultEqualsLiteralRankOverrideAndReplays(int rank)
    {
        var r = Request(rank); r.Talents.Ranks["mutilate"] = 1;
        r.Talents.Ranks["seal-fate"] = 5; r.Tuning.PhysicalCritPercent = 100;
        r.Tuning.EnergyPerSecond = 60;
        var sourced = new RogueSimRunner().Run(r);
        var literal = RogueSimulationRequestSnapshot.Create(r);
        literal.Tuning.ImprovedExposeArmorFivePointRefund = new[] { 0, 1, 2 }[rank];
        Assert.Equal(Stable(new RogueSimRunner().Run(literal)), Stable(sourced));
        Assert.Equal(Stable(sourced), Stable(new RogueSimRunner().Run(r)));
        Assert.Equal(5m, sourced.Run.CustomMetrics["rogue-expose-armor-points"]);
        Assert.Contains(sourced.Run.Timeline, e => e.Type == CombatEventType.AuraApplied && e.AbilityKey == "expose-armor");
        Assert.Contains(sourced.Run.Timeline, e => e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == "expose-armor" && e.Amount == -new[] { 25m, 20m, 15m }[rank]);
        Assert.Contains(sourced.Run.Timeline, e => e.Type == CombatEventType.Damage &&
            e.RawAmount > e.Amount && e.MitigatedAmount > 0);
        literal.Tuning.ImprovedExposeArmorFivePointRefund = 5;
        var legacy = new RogueSimRunner().Run(literal);
        if (rank > 0) Assert.NotEqual(Stable(legacy), Stable(sourced));
        else Assert.Equal(Stable(legacy), Stable(sourced));
    }
}
