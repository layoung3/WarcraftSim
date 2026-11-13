using System.Reflection;
using System.Text.Json;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Historical 33/67/100% Initiative; successful-opener and draw policy are
/// provisional local conventions, not server certification. No strict Subtlety admission.</summary>
public sealed class Cp175RogueInitiativeSourceCorrections
{
    private static RogueSimRequest Request(int? rank = 3, string spec = "subtlety")
    {
        var r = RogueSimRequest.CreatePreset(spec);
        r.Talents = new() { AssassinationPointsSpent = 20, CombatPointsSpent = 15,
            SubtletyPointsSpent = 16, Ranks = new(StringComparer.OrdinalIgnoreCase) };
        if (rank.HasValue) r.Talents.Ranks["initiative"] = rank.Value;
        r.StartInStealth = true; r.Character.BaseStats = new(); r.BothWeaponsDaggers = true;
        r.IsBehindTarget = true; r.MainHandWeaponType = r.OffHandWeaponType = "dagger";
        r.UseInstantPoison = false; r.MainHandPoison = r.OffHandPoison = "none";
        r.Tuning.MainHandWeaponDamage = 100; r.Tuning.OffHandWeaponDamage = 80;
        r.Tuning.MainHandHitPercent = r.Tuning.OffHandHitPercent = 100;
        r.Tuning.PhysicalCritPercent = 0; r.TargetArmor = r.TargetDodgePercent = r.TargetParryPercent = 0;
        r.TargetMaximumHealth = 10000000; r.DurationSeconds = 2; r.CaptureTimeline = true;
        return r;
    }

    // Old numerical tests deliberately bypass ONLY unsupported-key admission:
    // Prepare validates the identical request with Initiative temporarily absent,
    // then restore that key before constructing the real combat processor.
    // No execution/result/aura ID is invented to make a proc eligible.
    private static IReadOnlyList<AbilityDefinition> PrepareWithoutInitiativeAdmission(RogueSimRequest r)
    {
        var present = r.Talents.Ranks.Remove("initiative", out var rank);
        try
        {
            var p = typeof(RogueSimRunner).GetMethod("Prepare", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [r])!;
            return (IReadOnlyList<AbilityDefinition>)p.GetType().GetProperty("Abilities")!.GetValue(p)!;
        }
        finally { if (present) r.Talents.Ranks["initiative"] = rank; }
    }

    private sealed class CountingRandom(params double[] values) : Random
    {
        public int Draws { get; private set; }
        public List<int> PointsAtDraw { get; } = [];
        public Func<int>? Points { get; set; }
        public override double NextDouble()
        {
            PointsAtDraw.Add(Points?.Invoke() ?? -1);
            var index = Draws++;
            return values.Length == 0 ? 0 : values[Math.Min(index, values.Length - 1)];
        }
    }

    private sealed class Rolls : ICombatRollResolver, IDamageMitigationResolver
    {
        public CombatRollResult Result { get; set; } = CombatRollResult.Hit();
        public Func<SimulationContext, SimulationActorState, SimulationActorState, AbilityDefinition, AbilityEffectDefinition, CombatRollResult>? Override { get; set; }
        public CombatRollResult Resolve(SimulationContext c, SimulationActorState s,
            SimulationActorState t, AbilityDefinition a, AbilityEffectDefinition e) => Override?.Invoke(c, s, t, a, e) ?? Result;
        public DamageMitigationResult Resolve(SimulationContext c, SimulationActorState s, SimulationActorState t,
            AbilityDefinition a, AbilityEffectDefinition e, decimal raw) => DamageMitigationResult.Unmitigated(raw);
    }

    private sealed record Application(Guid Aura, Guid Execution, string Source, string Target,
        string Ability, string Effect, string AuraKey, decimal Time);

    // Pre-product first-principles witness, intentionally separate from production.
    // Copies immutable scalars at executor-first completion, never retains AuraInstance.
    private sealed class CorrelationWitness : ICombatEventProcessor
    {
        public Dictionary<Guid, Application> Pending { get; } = [];
        public List<Application> Captured { get; } = [];
        public List<Application> Matched { get; } = [];
        private readonly HashSet<Guid> _observed = [];
        private readonly HashSet<Guid> _bound = [];
        public void Process(SimulationContext c, CombatEvent ev)
        {
            foreach (var stale in Pending.Values.Where(p => p.Time < ev.TimeSeconds).ToArray()) Pending.Remove(stale.Aura);
            if (ev.Type == CombatEventType.SimulationEnded) { Pending.Clear(); return; }
            if (ev.Type == CombatEventType.AuraApplied && ev.AuraInstanceId is Guid id &&
                Pending.TryGetValue(id, out var p) && p.Source == ev.SourceActorKey && p.Target == ev.TargetActorKey &&
                p.Ability == ev.AbilityKey && p.Effect == ev.EffectKey && p.Time == ev.TimeSeconds)
            { Pending.Remove(id); Matched.Add(p); return; }
            if (ev.Type != CombatEventType.AbilityCastCompleted || ev.AbilityExecutionId is not Guid executionId ||
                c.GetAbilityExecution(executionId) is not { } execution ||
                execution.SourceActorKey != ev.SourceActorKey || execution.TargetActorKey != ev.TargetActorKey ||
                execution.AbilityKey != ev.AbilityKey || !_observed.Add(executionId)) return;
            if (ev.AbilityKey is not ("garrote" or "cheap-shot")) return;
            var source = c.GetActor(execution.SourceActorKey)!;
            var target = c.GetActor(execution.TargetActorKey)!;
            var ability = source.Abilities[execution.AbilityKey].Definition;
            if (ability.IsChanneled || ability.Effects.Count != 1) return;
            var effect = ability.Effects[0];
            var garrote = ability.Key == "garrote";
            if (effect.Key != (garrote ? "garrote-tick" : "cheap-shot-control") ||
                effect.AuraKey != (garrote ? "garrote" : "cheap-shot-stun") ||
                effect.EffectType != (garrote ? AbilityEffectTypes.PeriodicDamage : AbilityEffectTypes.ApplyAura) ||
                effect.TargetType != AbilityTargetTypes.Enemy || effect.AuraStackingMode != AuraStackingMode.Refresh ||
                effect.TravelTimeSeconds != 0 ||
                effect.ApplyOnChannelTick || !string.IsNullOrEmpty(effect.DependsOnEffectKey) ||
                effect.DurationSeconds is not > 0 || (garrote && effect.TickIntervalSeconds is not > 0) ||
                !execution.EffectResultsByTarget.TryGetValue((effect.Key, target.Key), out var result) || !result.Landed) return;
            var matching = target.ActiveAuras.Where(a => a.Definition.Key == effect.AuraKey &&
                a.SourceActorKey == source.Key && a.TargetActorKey == target.Key && a.AbilityKey == ability.Key &&
                a.EffectKey == effect.Key && a.AppliedAtSeconds == ev.TimeSeconds && a.InstanceId != Guid.Empty).Take(2).ToArray();
            if (matching.Length != 1) return;
            var aura = matching[0];
            if (!_bound.Add(aura.InstanceId)) return;
            var copy = new Application(aura.InstanceId, execution.Id, source.Key, target.Key,
                ability.Key, effect.Key, effect.AuraKey!, aura.AppliedAtSeconds);
            Pending.Add(copy.Aura, copy); Captured.Add(copy);
        }
    }

    private sealed class Harness : ICombatEventProcessor
    {
        public RogueSimRequest Request { get; }
        public SimulationContext Context { get; }
        public SimulationActorState Actor => Context.GetActor("rogue")!;
        public SimulationActorState Target => Context.GetActor("target")!;
        public AuraManager Auras { get; } = new();
        public AbilityExecutor Executor { get; }
        public RogueCombatProcessor Combat { get; }
        public RogueCombatProcessor Other { get; }
        public RoguePoisonProcessor Poison { get; }
        public Rolls Rolls { get; } = new();
        public CountingRandom Random { get; }
        public CorrelationWitness Witness { get; } = new();
        public List<CombatEvent> Events { get; } = [];
        public Action<CombatEvent>? AfterEvent { get; set; }
        public Action<CombatEvent>? BeforeCombat { get; set; }
        private readonly Dictionary<string, Action> _actions = [];
        public Harness(int? rank = 3, params double[] draws) : this(Request(rank), draws) { }
        public Harness(RogueSimRequest request, params double[] draws)
        {
            Request = request; var abilities = PrepareWithoutInitiativeAdmission(request);
            Context = new(new() { DurationSeconds = 2, Seed = 35, CaptureTimeline = true,
                PrimaryActorKey = "rogue", MaximumProcessedEvents = 10000 });
            Random = new(draws);
            typeof(SimulationContext).GetField("<Random>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Context, Random);
            foreach (var key in new[] { "rogue", "other-rogue", "target", "target-2" })
            {
                var friendly = key.Contains("rogue", StringComparison.Ordinal);
                var actor = new SimulationActorState { Key = key, Name = key, TeamKey = friendly ? "raid" : "enemy",
                    Level = friendly ? 60 : 63, MaximumHealth = 10000000 };
                actor.InitializeHealth(10000000); Context.AddActor(actor);
                if (!friendly) continue;
                actor.AddResource(new("energy", 100, 100, 0));
                foreach (var ability in abilities) actor.AddAbility(ability);
            }
            Executor = new(Auras, Rolls, Rolls);
            var auto = new AutoAttackProcessor(Executor);
            Combat = new(request, Actor, new RotationProfile(), Executor, Auras, auto, []);
            Other = new(request, Context.GetActor("other-rogue")!, new RotationProfile(), Executor, Auras, auto, []);
            Poison = new(request, Actor, Auras, Executor);
            Random.Points = () => Combat.ComboPointsFor("target");
        }
        public void At(decimal time, Action action)
        {
            var key = "fixture-" + _actions.Count; _actions.Add(key, action);
            Context.ScheduleEvent(new() { Type = CombatEventType.RotationDecision, TimeSeconds = time,
                SourceActorKey = "rogue", EffectKey = key, IsInternal = true });
        }
        public void Stealth(string source = "rogue") => Auras.ApplyAura(Context, Context.GetActor(source)!,
            new() { Key = "rogue-stealth", Name = "fixture stealth", DurationSeconds = 100 }, source, "vanish", "vanish-stealth");
        public Guid Cast(string ability, string target = "target", string source = "rogue")
        {
            Assert.True(Executor.TryStartAbility(Context, source, target, ability).Success);
            return Context.AbilityExecutions.Values.Single(e => e.SourceActorKey == source && !e.CastCompletionProcessed && !e.IsCancelled).Id;
        }
        // Controlled overlap: genuine TryStart/executor resolution with normal effects,
        // explicitly dispatched completion before queued AuraApplied. No synthetic IDs.
        public CombatEvent ResolveNow(string ability, string target = "target", string source = "rogue")
        {
            var actor = Context.GetActor(source)!; actor.Resources["energy"].SetCurrent(100); Stealth(source);
            actor.Abilities[ability].Definition.IsOffGlobalCooldown = true;
            var id = Cast(ability, target, source);
            var ev = new CombatEvent { Type = CombatEventType.AbilityCastCompleted, TimeSeconds = Context.CurrentTimeSeconds,
                SourceActorKey = source, TargetActorKey = target, AbilityKey = ability, AbilityExecutionId = id };
            Executor.Process(Context, ev); Witness.Process(Context, ev); Combat.Process(Context, ev); Other.Process(Context, ev);
            return ev;
        }
        public void Process(SimulationContext c, CombatEvent ev)
        {
            Events.Add(ev); Actor.Resources["energy"].RegenerationPerSecond = 0;
            if (ev.EffectKey is { } key && _actions.TryGetValue(key, out var action)) action();
            AfterEvent?.Invoke(ev);
        }
        private sealed class Hook(Harness h) : ICombatEventProcessor
        { public void Process(SimulationContext c, CombatEvent e) => h.BeforeCombat?.Invoke(e); }
        public void Run() => new SimulationEngine([Executor, Auras, Witness, new Hook(this), Combat, Other, Poison, this]).Run(Context);
        public CombatEvent Damage => Assert.Single(Events, e => e.Type == CombatEventType.Damage && e.AbilityKey == "ambush");
        public void SeedPoints(int points) => typeof(RogueCombatProcessor).GetMethod("AddPoints", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Combat, ["target", points]);
    }

    public static IEnumerable<object[]> AdmissionRanks()
    { foreach (var spec in new[] { "assassination", "combat", "subtlety" }) for (var rank = 0; rank <= 3; rank++) yield return [spec, rank]; }
    [Theory][MemberData(nameof(AdmissionRanks))]
    public void Admission_LegacyAcceptsSourceRanks(string spec, int rank)
    { var r = Request(rank, spec); r.Talents.Validate(60, spec); Assert.NotNull(RogueSimRunner.Preview(r)); }
    [Theory][InlineData(-1)][InlineData(4)]
    public void Control_InvalidRanksRemainRejected(int rank) => Assert.Throws<ArgumentException>(() => Request(rank).Talents.Validate(60, "subtlety"));

    public static IEnumerable<object[]> ProcCases()
    {
        foreach (var ability in new[] { "ambush", "garrote", "cheap-shot" })
        foreach (var rank in new int?[] { null, 0, 1, 2, 3 })
        foreach (var draw in new[] { .329999, .33, .669999, .67, .999999 })
        {
            var bonus = rank switch { 1 => draw < .33, 2 => draw < .67, 3 => true, _ => false };
            yield return [ability, rank!, draw, (ability == "cheap-shot" ? 2 : 1) + (bonus ? 1 : 0), rank > 0 ? 1 : 0];
        }
    }
    [Theory][MemberData(nameof(ProcCases))]
    public void Numerical_RealExecutorExactRanksAndDrawBoundaries(string ability, int? rank, double draw, int points, int draws)
    {
        var h = new Harness(rank, draw); h.At(.1m, () => h.Cast(ability)); h.Run();
        Assert.Single(h.Context.AbilityExecutions); Assert.Equal(points, h.Combat.ComboPointsFor("target"));
        Assert.Equal(draws, h.Random.Draws);
        if (ability != "ambush") { Assert.Single(h.Witness.Captured); Assert.Equal(h.Witness.Captured, h.Witness.Matched); }
    }
    [Theory][InlineData("miss")][InlineData("dodge")][InlineData("parry")]
    public void Control_RealAvoidedAmbushHasNoBonusOrDraw(string outcome)
    {
        var h = new Harness(); h.Rolls.Result = CombatRollResult.Avoided(outcome);
        h.At(.1m, () => h.Cast("ambush")); h.Run();
        Assert.False(h.Context.AbilityExecutions.Values.Single().EffectResultsByTarget[("ambush-hit", "target")].Landed);
        Assert.Equal(0, h.Combat.ComboPointsFor("target")); Assert.Equal(0, h.Random.Draws);
    }
    [Theory][InlineData("zero", null, 0)][InlineData("zero", 0, 0)][InlineData("zero", 3, 1)]
    [InlineData("block", null, 0)][InlineData("block", 0, 0)][InlineData("block", 3, 1)]
    [InlineData("absorb", null, 0)][InlineData("absorb", 0, 0)][InlineData("absorb", 3, 1)]
    public void Numerical_RealLandedZeroDamageKeepsBaseZeroAndAddsOnlyInitiative(string mode, int? rank, int expected)
    {
        var r = Request(rank); r.Talents.Ranks["seal-fate"] = 5; r.UseInstantPoison = true;
        r.MainHandPoison = "instant"; var h = new Harness(r, .1);
        h.Rolls.Result = CombatRollResult.Critical(2);
        if (mode == "zero") h.Actor.Abilities["ambush"].Definition.Effects[0].MinimumValue =
            h.Actor.Abilities["ambush"].Definition.Effects[0].MaximumValue = 0;
        if (mode == "block") h.Rolls.Result = CombatRollResult.Block(100000);
        h.At(.1m, () => {
            if (mode == "absorb") new AbsorbManager().ApplyAbsorb(h.Context, h.Target, "shield", "Shield", 100000, 10,
                AbsorbStackingMode.Refresh, 1, "target", "shield", "shield");
            h.Cast("ambush");
        });
        h.Run(); Assert.Equal(0m, h.Damage.Amount);
        Assert.True(h.Context.AbilityExecutions.Values.Single().EffectResultsByTarget[("ambush-hit", "target")].Landed);
        if (mode != "zero") Assert.True(h.Damage.RawAmount > 0);
        Assert.Equal(expected, h.Combat.ComboPointsFor("target")); Assert.Equal(expected, h.Random.Draws);
        Assert.DoesNotContain(h.Events, e => e.AbilityKey == "instant-poison");
    }

    [Theory][InlineData(0, 2)][InlineData(3, 5)][InlineData(4, 5)][InlineData(5, 5)]
    public void Numerical_CapStillConsumesExactlyOneDraw(int start, int expected)
    {
        var h = new Harness(); h.SeedPoints(start); h.At(.1m, () => h.Cast("ambush")); h.Run();
        Assert.Equal(expected, h.Combat.ComboPointsFor("target")); Assert.Equal(1, h.Random.Draws);
        Assert.Equal(new[] { Math.Min(5, start + 1) }, h.Random.PointsAtDraw);
    }

    [Theory][InlineData("garrote")][InlineData("cheap-shot")]
    public void Witness_RealQueuedCompletionCopiesExactImmutableApplication(string ability)
    {
        var h = new Harness(0); h.At(.1m, () => h.Cast(ability)); h.Run();
        var p = Assert.Single(h.Witness.Captured); Assert.Equal(h.Witness.Captured, h.Witness.Matched);
        var completion = Assert.Single(h.Events, e => e.Type == CombatEventType.AbilityCastCompleted);
        var applied = Assert.Single(h.Events, e => e.Type == CombatEventType.AuraApplied && e.AbilityKey == ability);
        Assert.Equal(completion.AbilityExecutionId, p.Execution); Assert.Equal(applied.AuraInstanceId, p.Aura);
        Assert.Equal(.1m, p.Time); Assert.Null(applied.AbilityExecutionId); Assert.Equal("rogue", p.Source);
        Assert.Equal("target", p.Target); Assert.Equal(ability, p.Ability); Assert.NotEqual(Guid.Empty, p.Aura);
    }

    [Theory][InlineData("garrote")][InlineData("cheap-shot")]
    public void Witness_SameTimeRefreshesKeepTwoTuplesAndDuplicateCannotRebind(string ability)
    {
        var h = new Harness(0); AuraInstance? reference = null; Guid original = default;
        h.At(.1m, () => {
            var first = h.ResolveNow(ability); var p = Assert.Single(h.Witness.Captured); original = p.Aura;
            reference = Assert.Single(h.Target.ActiveAuras); h.ResolveNow(ability);
            h.Witness.Process(h.Context, first); // replay first completion after second refresh
            Assert.Equal(2, h.Witness.Captured.Count); Assert.Equal(2, h.Witness.Pending.Count);
            Assert.Same(reference, Assert.Single(h.Target.ActiveAuras)); Assert.NotEqual(original, reference.InstanceId);
            Assert.Equal(original, h.Witness.Captured[0].Aura);
        });
        h.Run(); Assert.Equal(2, h.Witness.Matched.Count);
        Assert.Equal(2, h.Witness.Matched.Select(x => x.Execution).Distinct().Count());
        Assert.Equal(2, h.Witness.Matched.Select(x => x.Aura).Distinct().Count()); Assert.Empty(h.Witness.Pending);
    }

    [Theory][InlineData("source")][InlineData("target")][InlineData("ability")][InlineData("effect")][InlineData("time")][InlineData("guid")]
    public void Witness_WrongNotificationCannotStealCandidate(string wrong)
    {
        var h = new Harness(0); h.At(.1m, () => {
            h.ResolveNow("garrote"); var p = Assert.Single(h.Witness.Captured);
            h.Witness.Process(h.Context, new() { Type = CombatEventType.AuraApplied,
                TimeSeconds = wrong == "time" ? 0 : p.Time, AuraInstanceId = wrong == "guid" ? Guid.NewGuid() : p.Aura,
                SourceActorKey = wrong == "source" ? "other-rogue" : p.Source,
                TargetActorKey = wrong == "target" ? "target-2" : p.Target,
                AbilityKey = wrong == "ability" ? "cheap-shot" : p.Ability,
                EffectKey = wrong == "effect" ? "wrong" : p.Effect });
            Assert.Single(h.Witness.Pending); Assert.Empty(h.Witness.Matched);
        }); h.Run(); Assert.Single(h.Witness.Matched);
    }

    [Theory][InlineData("garrote")][InlineData("cheap-shot")]
    public void Witness_SuccessThenRemovalDeathCancellationStillMatches(string ability)
    {
        var h = new Harness(0); h.At(.1m, () => {
            var e = h.ResolveNow(ability); h.Auras.RemoveAuras(h.Context, h.Target,
                ability == "garrote" ? "garrote" : "cheap-shot-stun", "rogue");
            h.Actor.TakeDamage(10000000); h.Target.TakeDamage(10000000);
            h.Context.CancelAbilityExecution(e.AbilityExecutionId!.Value, h.Context.CurrentTimeSeconds);
        }); h.Run(); Assert.Single(h.Witness.Matched);
    }

    [Theory][InlineData("garrote")][InlineData("cheap-shot")]
    public void Witness_FailedSpendThenSuccessDoesNotStealLaterApplication(string ability)
    {
        var h = new Harness(0); CombatEvent? failed = null;
        h.BeforeCombat = e => { if (e.Type == CombatEventType.AbilityCastCompleted && failed is null) failed = e; };
        h.At(.1m, () => { h.Cast(ability); h.Actor.Resources["energy"].SetCurrent(0); });
        h.At(1.2m, () => { Assert.NotNull(failed); Assert.Empty(h.Witness.Captured);
            h.ResolveNow(ability); h.Witness.Process(h.Context, failed!); Assert.Single(h.Witness.Captured); });
        h.Run(); Assert.Single(h.Witness.Matched);
        Assert.Empty(h.Context.AbilityExecutions[failed!.AbilityExecutionId!.Value].EffectResultsByTarget);
    }

    [Theory][InlineData("duration")][InlineData("tick")]
    public void Witness_MalformedLandedNoApplicationCannotCaptureEarlierAura(string malformed)
    {
        var h = new Harness(0); h.At(.1m, () => {
            h.ResolveNow("garrote"); var old = Assert.Single(h.Witness.Captured);
            var effect = h.Actor.Abilities["garrote"].Definition.Effects[0];
            if (malformed == "duration") effect.DurationSeconds = 0; else effect.TickIntervalSeconds = 0;
            var failed = h.ResolveNow("garrote");
            Assert.True(h.Context.AbilityExecutions[failed.AbilityExecutionId!.Value].EffectResultsByTarget[("garrote-tick", "target")].Landed);
            Assert.Single(h.Witness.Captured); Assert.Equal(old.Aura, Assert.Single(h.Target.ActiveAuras).InstanceId);
        }); h.Run(); Assert.Single(h.Witness.Matched);
    }

    private static CombatEvent Copy(CombatEvent e) => JsonSerializer.Deserialize<CombatEvent>(JsonSerializer.Serialize(e))!;

    [Theory][InlineData(0, 1, 1)][InlineData(1, 1, 1)][InlineData(3, 2, 1)]
    public void Numerical_DuplicateAmbushReservesFailedAndSuccessfulAttempt(int rank, int expected, int draws)
    {
        var h = new Harness(rank, .99); h.At(.1m, () => h.Cast("ambush"));
        h.AfterEvent = e => { if (e.Type == CombatEventType.Damage && e.AbilityKey == "ambush")
            { h.Combat.Process(h.Context, e); h.Combat.Process(h.Context, e); } };
        h.Run(); Assert.Equal(expected, h.Combat.ComboPointsFor("target"));
        Assert.Equal(rank == 0 ? 0 : draws, h.Random.Draws);
    }

    [Theory][InlineData("source")][InlineData("target")][InlineData("ability")][InlineData("effect")]
    [InlineData("execution")][InlineData("periodic")][InlineData("channel")][InlineData("time")]
    public void Control_WrongAmbushIdentityCannotConsumeRealAttempt(string wrong)
    {
        var h = new Harness(); h.At(.1m, () => h.Cast("ambush"));
        h.BeforeCombat = e => {
            if (e.Type != CombatEventType.Damage || e.AbilityKey != "ambush") return;
            var bad = Copy(e); bad.Amount = 0;
            switch (wrong) {
                case "source": bad.SourceActorKey = "other-rogue"; break;
                case "target": bad.TargetActorKey = "target-2"; break;
                case "ability": bad.AbilityKey = "sinister-strike"; break;
                case "effect": bad.EffectKey = "other"; break;
                case "execution": bad.AbilityExecutionId = Guid.NewGuid(); break;
                case "time": bad.TimeSeconds = 0; break;
                case "periodic": bad.EffectDeliveryType = CombatEffectDeliveryType.Periodic; break;
                case "channel": bad.EffectDeliveryType = CombatEffectDeliveryType.ChannelTick; break;
            }
            h.Combat.Process(h.Context, bad); Assert.Equal(0, h.Random.Draws);
            Assert.Equal(0, h.Combat.ComboPointsFor("target"));
        };
        h.Run(); Assert.Equal(2, h.Combat.ComboPointsFor("target")); Assert.Equal(1, h.Random.Draws);
    }

    [Theory][InlineData("ambush", 0)][InlineData("garrote", 0)][InlineData("cheap-shot", 0)]
    [InlineData("ambush", 3)][InlineData("garrote", 3)][InlineData("cheap-shot", 3)]
    public void Numerical_TwoTargetsAndRoguesKeepIndependentOwnership(string ability, int rank)
    {
        var h = new Harness(rank); h.At(.1m, () => {
            foreach (var source in new[] { "rogue", "other-rogue" })
            foreach (var target in new[] { "target", "target-2" }) h.ResolveNow(ability, target, source);
        }); h.Run(); var expected = (ability == "cheap-shot" ? 2 : 1) + (rank > 0 ? 1 : 0);
        Assert.Equal(expected, h.Combat.ComboPointsFor("target")); Assert.Equal(expected, h.Combat.ComboPointsFor("target-2"));
        Assert.Equal(expected, h.Other.ComboPointsFor("target")); Assert.Equal(expected, h.Other.ComboPointsFor("target-2"));
        Assert.Equal(rank > 0 ? 4 : 0, h.Random.Draws);
        if (ability != "ambush") { Assert.Equal(4, h.Witness.Matched.Count); Assert.Equal(4, h.Witness.Matched.Select(p => p.Aura).Distinct().Count()); }
    }

    [Theory][InlineData(0, 3, 0)][InlineData(1, 3, 2)][InlineData(3, 5, 2)]
    public void Numerical_DuplicateGarrotePreservesBaseButCannotConsumeLaterExecution(int rank, int expected, int draws)
    {
        var h = new Harness(rank, .99); var firstSeen = false;
        h.At(.1m, () => h.ResolveNow("garrote"));
        h.AfterEvent = e => {
            if (firstSeen || e.Type != CombatEventType.AuraApplied || e.AbilityKey != "garrote") return;
            firstSeen = true; h.ResolveNow("garrote");
            var before = h.Combat.ComboPointsFor("target"); var beforeDraws = h.Random.Draws;
            h.Combat.Process(h.Context, e); // Known existing base duplication is intentionally preserved.
            Assert.Equal(Math.Min(5, before + 1), h.Combat.ComboPointsFor("target")); Assert.Equal(beforeDraws, h.Random.Draws);
        };
        h.Run(); Assert.Equal(expected, h.Combat.ComboPointsFor("target")); Assert.Equal(draws, h.Random.Draws);
    }

    [Theory][InlineData(0)][InlineData(3)]
    public void Control_FailedSpendCheapShotRetainsTwoBasePointsWithoutInitiative(int rank)
    {
        var h = new Harness(rank); h.At(.1m, () => { h.Cast("cheap-shot"); h.Actor.Resources["energy"].SetCurrent(0); });
        h.Run(); Assert.Equal(2, h.Combat.ComboPointsFor("target")); Assert.Equal(0, h.Random.Draws);
        Assert.Empty(h.Witness.Captured); Assert.DoesNotContain(h.Events, e => e.AbilityKey == "cheap-shot" && e.Type == CombatEventType.AuraApplied);
    }

    [Theory][InlineData("miss")][InlineData("dodge")][InlineData("parry")]
    public void Numerical_FailedGarroteThenSuccessHasOnlyOneInitiativeAttempt(string result)
    {
        var h = new Harness(); h.Rolls.Result = CombatRollResult.Avoided(result);
        h.At(.1m, () => h.Cast("garrote"));
        h.At(1.2m, () => { Assert.Equal(0, h.Random.Draws); Assert.Equal(0, h.Combat.ComboPointsFor("target"));
            h.Rolls.Result = CombatRollResult.Hit(); h.ResolveNow("garrote"); });
        h.Run(); Assert.Equal(2, h.Combat.ComboPointsFor("target")); Assert.Equal(1, h.Random.Draws); Assert.Single(h.Witness.Matched);
    }

    [Theory][InlineData("death-source")][InlineData("death-target")][InlineData("cancel")][InlineData("spend")]
    public void Control_PreResolutionAmbushFailureHasNoBonusOrDraw(string failure)
    {
        var h = new Harness(); h.At(.1m, () => {
            var id = h.Cast("ambush");
            switch (failure) {
                case "death-source": h.Actor.TakeDamage(10000000); break;
                case "death-target": h.Target.TakeDamage(10000000); break;
                case "cancel": h.Context.CancelAbilityExecution(id, h.Context.CurrentTimeSeconds); break;
                case "spend": h.Actor.Resources["energy"].SetCurrent(0); break;
            }
        }); h.Run(); Assert.Equal(0, h.Combat.ComboPointsFor("target")); Assert.Equal(0, h.Random.Draws);
        Assert.Empty(Assert.Single(h.Context.AbilityExecutions).Value.EffectResultsByTarget);
    }

    [Theory][InlineData("lethal")][InlineData("source-death")][InlineData("cancel")]
    public void Numerical_ResolvedAmbushSurvivesDeathAndCancellation(string after)
    {
        var h = new Harness(); if (after == "lethal") h.Target.InitializeHealth(1);
        h.BeforeCombat = e => {
            if (e.Type != CombatEventType.Damage || e.AbilityKey != "ambush") return;
            if (after == "source-death") h.Actor.TakeDamage(10000000);
            if (after == "cancel") h.Context.CancelAbilityExecution(e.AbilityExecutionId!.Value, h.Context.CurrentTimeSeconds);
        };
        h.At(.1m, () => h.Cast("ambush")); h.Run();
        Assert.Equal(2, h.Combat.ComboPointsFor("target")); Assert.Equal(1, h.Random.Draws);
    }

    [Theory][InlineData("garrote", 2)][InlineData("cheap-shot", 3)]
    public void Numerical_SuccessThenAuraRemovalStillEarnsIncrementalBonus(string ability, int expected)
    {
        var h = new Harness(); h.At(.1m, () => {
            var e = h.ResolveNow(ability); h.Auras.RemoveAuras(h.Context, h.Target,
                ability == "garrote" ? "garrote" : "cheap-shot-stun", "rogue");
            h.Actor.TakeDamage(10000000); h.Target.TakeDamage(10000000);
            h.Context.CancelAbilityExecution(e.AbilityExecutionId!.Value, h.Context.CurrentTimeSeconds);
        }); h.Run(); Assert.Equal(expected, h.Combat.ComboPointsFor("target")); Assert.Equal(1, h.Random.Draws);
    }

    [Fact]
    public void Numerical_PositiveAmbushDrawsAfterBaseAndSealFateBeforePoison()
    {
        var r = Request(3); r.Talents.Ranks["seal-fate"] = 5; r.MainHandPoison = "instant"; r.UseInstantPoison = true;
        var h = new Harness(r, .1, .1, .99); h.Rolls.Result = CombatRollResult.Critical(2);
        h.At(.1m, () => h.Cast("ambush")); h.Run();
        Assert.Equal(3, h.Combat.ComboPointsFor("target")); Assert.Equal(new[] { 1, 2, 3 }, h.Random.PointsAtDraw);
        Assert.Equal(3, h.Random.Draws); Assert.Equal(0, h.Poison.Applications);
    }

    [Fact]
    public void Numerical_QueuedAuraAttemptFollowsInterveningSameTimeWork()
    {
        var h = new Harness(1, .99, .1); h.AfterEvent = e => {
            if (e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "garrote") {
                Assert.Equal(0, h.Random.Draws); Assert.Equal(0, h.Combat.ComboPointsFor("target"));
                Assert.Equal(.99, h.Context.Random.NextDouble());
            }
        };
        h.At(.1m, () => h.Cast("garrote")); h.Run();
        Assert.Equal(2, h.Combat.ComboPointsFor("target")); Assert.Equal(new[] { 0, 1 }, h.Random.PointsAtDraw);
    }

    [Theory][InlineData(0, 160)][InlineData(3, 320)]
    public void Numerical_ActualFinisherConsumesOneVersusTwoPoints(int rank, int expectedDamage)
    {
        var h = new Harness(rank); h.Rolls.Override = (c, source, target, ability, effect) => new() {
            Landed = true, ResultKey = CombatResultTypes.Hit,
            AmountMultiplier = h.Combat.DamageMultiplier(ability.Key, CombatEffectDeliveryType.Direct, c, target) };
        h.At(.1m, () => h.Cast("ambush"));
        h.At(1.2m, () => { h.Actor.Resources["energy"].SetCurrent(100); h.Cast("eviscerate"); });
        h.Run(); Assert.Equal(expectedDamage, Assert.Single(h.Events, e => e.Type == CombatEventType.Damage && e.AbilityKey == "eviscerate").Amount);
        Assert.Equal(0, h.Combat.ComboPointsFor("target")); Assert.Equal(1, h.Combat.FinishersUsed);
    }

    [Theory][InlineData("vanish")][InlineData("cutthroat")]
    public void Numerical_ExistingOpportunityAdmitsLaterAmbush(string opportunity)
    {
        var r = Request(); if (opportunity == "cutthroat") { r.StartInStealth = false; r.Talents.Ranks["cutthroat"] = 5; }
        var h = new Harness(r, 0);
        if (opportunity == "cutthroat") {
            h.At(.1m, () => h.Cast("backstab"));
            h.At(1.2m, () => { h.Actor.Resources["energy"].SetCurrent(100); h.Cast("ambush"); });
        } else {
            h.At(.1m, () => h.Cast("ambush"));
            h.At(1.2m, () => { h.Actor.Resources["energy"].SetCurrent(100); h.Cast("vanish"); });
            h.AfterEvent = e => { if (e.Type == CombatEventType.AbilityCastCompleted && e.AbilityKey == "vanish") h.Cast("ambush"); };
        }
        h.Run(); Assert.Equal(opportunity == "vanish" ? 4 : 3, h.Combat.ComboPointsFor("target")); Assert.Equal(2, h.Random.Draws);
    }

    [Fact]
    public void Control_UnmatchedPendingIsDiscardedWithoutDrawOnTimeAdvance()
    {
        var h = new Harness(); CombatEvent? actual = null;
        h.BeforeCombat = e => {
            if (e.Type == CombatEventType.AuraApplied && e.AbilityKey == "garrote") {
                actual = Copy(e); e.SourceActorKey = "unmatched"; // controlled dropped notification, not public behavior
            }
        };
        h.At(.1m, () => h.Cast("garrote"));
        h.At(.2m, () => {
            Assert.NotNull(actual); Assert.Equal(0, h.Random.Draws);
            h.Combat.Process(h.Context, actual!); Assert.Equal(0, h.Random.Draws);
            Assert.Equal(1, h.Combat.ComboPointsFor("target")); // preserved Garrote base, no Initiative
        }); h.Run(); Assert.Equal(0, h.Random.Draws);
    }

    public static IEnumerable<object[]> AuraIdentityCases()
    { foreach (var wrong in new[] { "source", "target", "ability", "effect", "time", "guid" })
      foreach (var rank in new[] { 0, 3 }) yield return [wrong, rank]; }
    [Theory][MemberData(nameof(AuraIdentityCases))]
    public void Numerical_WrongAuraNotificationCannotStealActualAttempt(string wrong, int rank)
    {
        var h = new Harness(rank); var extraTargetBase = 0;
        h.BeforeCombat = e => {
            if (e.Type != CombatEventType.AuraApplied || e.AbilityKey != "garrote") return;
            var bad = Copy(e);
            switch (wrong) {
                case "source": bad.SourceActorKey = "other-rogue"; break;
                case "target": bad.TargetActorKey = "target-2"; break;
                case "ability": bad.AbilityKey = "cheap-shot"; break;
                case "effect": bad.EffectKey = "wrong"; break;
                case "time": bad.TimeSeconds = 0; break;
                case "guid": bad.AuraInstanceId = Guid.NewGuid(); break;
            }
            h.Combat.Process(h.Context, bad);
            extraTargetBase = wrong is "effect" or "time" or "guid" ? 1 : 0;
            Assert.Equal(extraTargetBase, h.Combat.ComboPointsFor("target"));
            Assert.Equal(wrong == "target" ? 1 : 0, h.Combat.ComboPointsFor("target-2"));
            Assert.Equal(0, h.Random.Draws);
        };
        h.At(.1m, () => h.Cast("garrote")); h.Run();
        Assert.Equal(extraTargetBase + 1 + (rank > 0 ? 1 : 0), h.Combat.ComboPointsFor("target"));
        Assert.Equal(rank > 0 ? 1 : 0, h.Random.Draws);
    }

    [Theory][InlineData("garrote")][InlineData("cheap-shot")]
    public void Witness_FirstFailedCompletionCannotBeReinspectedAfterSameTimeSuccess(string ability)
    {
        var h = new Harness(0); h.At(.1m, () => {
            h.Actor.Abilities[ability].Definition.IsOffGlobalCooldown = true;
            var id = h.Cast(ability); h.Actor.Resources["energy"].SetCurrent(0);
            var failed = new CombatEvent { Type = CombatEventType.AbilityCastCompleted, TimeSeconds = .1m,
                SourceActorKey = "rogue", TargetActorKey = "target", AbilityKey = ability, AbilityExecutionId = id };
            h.Executor.Process(h.Context, failed); h.Witness.Process(h.Context, failed);
            Assert.Empty(h.Witness.Captured); h.ResolveNow(ability);
            h.Witness.Process(h.Context, failed); Assert.Single(h.Witness.Captured);
            Assert.NotEqual(id, h.Witness.Captured[0].Execution);
        }); h.Run(); Assert.Single(h.Witness.Matched);
    }

    [Fact]
    public void Control_SameTimeSimulationEndedClearsProductPendingBeforeNotification()
    {
        var h = new Harness(); h.BeforeCombat = e => {
            if (e.Type != CombatEventType.AuraApplied || e.AbilityKey != "garrote") return;
            Assert.Equal(.1m, h.Context.CurrentTimeSeconds); Assert.Equal(0, h.Random.Draws);
            h.Combat.Process(h.Context, new() { Type = CombatEventType.SimulationEnded, TimeSeconds = .1m });
            // The very same genuine notification then continues into Combat.
            // Its preserved base point is expected; Initiative must be cleared.
        };
        h.At(.1m, () => h.Cast("garrote")); h.Run();
        Assert.Equal(1, h.Combat.ComboPointsFor("target")); Assert.Equal(0, h.Random.Draws);
    }

    [Fact]
    public void Witness_SameTimeSimulationEndedClearsUnconsumedTuple()
    {
        var h = new Harness(0); h.At(.1m, () => {
            h.ResolveNow("garrote"); Assert.Single(h.Witness.Pending); Assert.Empty(h.Witness.Matched);
            h.Witness.Process(h.Context, new() { Type = CombatEventType.SimulationEnded, TimeSeconds = .1m });
            Assert.Empty(h.Witness.Pending);
        }); h.Run(); Assert.Single(h.Witness.Captured); Assert.Empty(h.Witness.Matched);
    }

    [Theory][InlineData(3)][InlineData(4)][InlineData(5)]
    public void Numerical_SealFateAndInitiativeConsumeOrderedDrawsEvenAtCap(int start)
    {
        var r = Request(); r.Talents.Ranks["seal-fate"] = 5; r.MainHandPoison = "instant"; r.UseInstantPoison = true;
        var h = new Harness(r, .1, .99, .99); h.Rolls.Result = CombatRollResult.Critical(2); h.SeedPoints(start);
        h.At(.1m, () => h.Cast("ambush")); h.Run();
        Assert.Equal(5, h.Combat.ComboPointsFor("target")); Assert.Equal(0, h.Poison.Applications);
        Assert.Equal(new[] { Math.Min(5, start + 1), 5, 5 }, h.Random.PointsAtDraw);
        Assert.Equal(3, h.Random.Draws);
    }

    [Fact]
    public void Numerical_CaseVariantActorIdentityUsesCanonicalTargetResult()
    {
        var h = new Harness(); h.At(.1m, () => h.Cast("ambush", "TARGET", "ROGUE")); h.Run();
        Assert.Equal(2, h.Combat.ComboPointsFor("target")); Assert.Equal(1, h.Random.Draws);
    }

    [Theory][InlineData("garrote", 1)][InlineData("cheap-shot", 2)]
    public void Control_UnsupportedIndependentAuraShapeHasNoInitiative(string ability, int basePoints)
    {
        var h = new Harness(); h.Actor.Abilities[ability].Definition.Effects[0].AuraStackingMode = AuraStackingMode.Independent;
        h.At(.1m, () => h.Cast(ability)); h.Run();
        Assert.Equal(basePoints, h.Combat.ComboPointsFor("target")); Assert.Equal(0, h.Random.Draws);
        Assert.Empty(h.Witness.Captured);
    }

    [Theory][InlineData("garrote", 3)][InlineData("cheap-shot", 2)]
    public void Control_AmbiguousOwnedSameTimeAurasCannotBindExecution(string ability, int basePoints)
    {
        var h = new Harness(); h.At(.1m, () => {
            var effect = h.Actor.Abilities[ability].Definition.Effects[0];
            // Deliberately unsupported preexisting duplicate state. These are
            // actual AuraManager applications, not fabricated execution records.
            for (var i = 0; i < 2; i++) h.Auras.ApplyAura(h.Context, h.Target,
                new() { Key = effect.AuraKey!, Name = "ambiguous fixture", DurationSeconds = 10,
                    StackingMode = AuraStackingMode.Independent }, "rogue", ability, effect.Key);
            h.Cast(ability);
        }); h.Run();
        Assert.Equal(basePoints, h.Combat.ComboPointsFor("target")); Assert.Equal(0, h.Random.Draws);
        Assert.Empty(h.Witness.Captured);
    }
}
