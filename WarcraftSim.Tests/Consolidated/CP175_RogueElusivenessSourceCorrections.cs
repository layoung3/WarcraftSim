using System.Reflection;
using Xunit.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Historical decoded Elusiveness subtracts 45/90 seconds from Vanish and Blind.
/// Existing bases, priority, timing, damage and control remain provisional local contracts.
/// These sparse legacy requests are not strict historical builds.</summary>
public sealed class Cp175RogueElusivenessSourceCorrections(ITestOutputHelper output)
{
    private static decimal Cooldown(string ability, int rank) =>
        (ability == "vanish" ? new decimal[] { 300, 300, 255, 210 } : [180, 180, 135, 90])[rank + 1];

    private static RogueSimRequest Request(int rank = 2, string spec = "subtlety")
    {
        var r = RogueSimRequest.CreatePreset(spec);
        r.Talents = new() { AssassinationPointsSpent = 10, CombatPointsSpent = 10,
            SubtletyPointsSpent = 31, Ranks = new(StringComparer.OrdinalIgnoreCase) };
        if (rank >= 0) r.Talents.Ranks["elusiveness"] = rank;
        r.StartInStealth = false; r.Character.BaseStats = new();
        r.BothWeaponsDaggers = true; r.IsBehindTarget = true;
        r.MainHandWeaponType = r.OffHandWeaponType = "dagger";
        r.UseInstantPoison = false; r.MainHandPoison = r.OffHandPoison = "none";
        r.UseUtilityAbilities = false;
        r.Tuning.MainHandWeaponDamage = 100; r.Tuning.OffHandWeaponDamage = 80;
        r.Tuning.MainHandSpeedSeconds = r.Tuning.OffHandSpeedSeconds = 2;
        r.Tuning.MainHandHitPercent = r.Tuning.OffHandHitPercent = 100;
        r.Tuning.AmbushBonusDamage = 150; r.Tuning.PhysicalCritPercent = 0;
        r.Tuning.EnergyPerSecond = 10;
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
        public SimulationContext Context { get; }
        public SimulationActorState Actor { get; }
        public SimulationActorState Other { get; }
        public SimulationActorState Target { get; }
        public RogueCombatProcessor Combat { get; }
        public RogueCombatProcessor OtherCombat { get; }
        public AbilityExecutor Executor { get; }
        public AuraManager Auras { get; } = new();
        public RotationProfile Rotation { get; } = new() { Entries = [
            new() { AbilityKey = "vanish", Priority = 1, IsEnabled = false } ] };
        public List<CombatEvent> Dispatched { get; } = [];
        public bool PriorityEnabled { get; set; }
        public Action<CombatEvent>? OnStarted { get; set; }
        private readonly PriorityRotationExecutor _priority;
        private readonly Dictionary<string, Action> _actions = new();
        private readonly RoguePoisonProcessor _poisons;
        public Harness(RogueSimRequest r, decimal duration = 3, RogueSimRequest? other = null)
        {
            Context = new(new() { Seed = 35, DurationSeconds = duration, CaptureTimeline = true,
                PrimaryActorKey = "rogue", MaximumProcessedEvents = 10000 });
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
                    foreach (var ability in PreparedAbilities(key == "rogue" ? r : other ?? r))
                        actor.AddAbility(ability);
                }
                Context.AddActor(actor);
            }
            Actor = Context.GetActor("rogue")!; Other = Context.GetActor("other-rogue")!;
            Target = Context.GetActor("target")!;
            RogueCombatProcessor? combat = null;
            Func<RogueCombatProcessor?> getCombat = () => combat;
            object Adapter(string name) => Activator.CreateInstance(
                typeof(RogueSimRunner).GetNestedType(name, BindingFlags.NonPublic)!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [r, getCombat], null)!;
            Executor = new(Auras, (ICombatRollResolver)Adapter("RogueRollResolver"),
                (IDamageMitigationResolver)Adapter("RogueArmorMitigation"));
            var auto = new AutoAttackProcessor(Executor);
            // Isolated mechanics fixtures deliberately have no weapon streams.
            // Actual public runner fixtures below retain both real auto streams.
            Combat = combat = new(r, Actor, Rotation, Executor, Auras, auto, []);
            OtherCombat = new(other ?? r, Other, new(), Executor, Auras, auto, []);
            _poisons = new(r, Actor, Auras, Executor);
            _priority = new(Rotation, "rogue", "target", Executor);
        }
        public void At(decimal time, Action action)
        {
            var key = "test-" + _actions.Count; _actions.Add(key, action);
            Context.ScheduleEvent(new() { TimeSeconds = time, Type = CombatEventType.RotationDecision,
                SourceActorKey = "rogue", TargetActorKey = "target", EffectKey = key, IsInternal = true });
        }
        public void Process(SimulationContext context, CombatEvent ev)
        {
            Dispatched.Add(ev);
            if (ev.Type == CombatEventType.AbilityCastStarted) OnStarted?.Invoke(ev);
            // Request validation still requires positive regeneration. Only these
            // isolated actors suspend it, explicitly after each dispatch.
            foreach (var actor in Context.Actors.Values)
                if (actor.Resources.TryGetValue("energy", out var resource)) resource.RegenerationPerSecond = 0;
            if (ev.EffectKey is { } key && _actions.TryGetValue(key, out var action)) action();
            if (PriorityEnabled) _priority.Process(context, ev);
        }
        public void Cast(string ability, string target = "target", string source = "rogue")
        {
            var result = Executor.TryStartAbility(Context, source, target, ability);
            Assert.True(result.Success, $"{source} could not cast {ability} at {Context.CurrentTimeSeconds}: {result.FailureReason}");
        }
        public CombatEvent[] Events(CombatEventType type, string ability) => Context.Timeline.Where(e =>
            e.Type == type && e.AbilityKey == ability).ToArray();
        public void Aura(string key, decimal duration) => Auras.ApplyAura(Context, Actor,
            new() { Key = key, DurationSeconds = duration }, Actor.Key, "test-state", "test-effect");
        public void RefreshRotation() => Combat.Process(Context, new() { Type = CombatEventType.RotationDecision,
            SourceActorKey = "rogue", TargetActorKey = "target", TimeSeconds = Context.CurrentTimeSeconds });
        public void Run() => new SimulationEngine([Executor, Auras, Combat, OtherCombat, _poisons, this]).Run(Context);
    }

    public static IEnumerable<object[]> RankSpecs()
    {
        foreach (var spec in new[] { "assassination", "combat", "subtlety" })
        for (var rank = -1; rank <= 2; rank++) yield return [spec, rank];
    }
    public static IEnumerable<object[]> AbilitiesAndRanks()
    {
        foreach (var ability in new[] { "vanish", "blind" })
        for (var rank = -1; rank <= 2; rank++) yield return [ability, rank];
    }
    public static IEnumerable<object[]> BoundaryCases()
    {
        foreach (var args in AbilitiesAndRanks())
        foreach (var offset in new[] { -.001m, 0m, .001m }) yield return [args[0], args[1], offset];
    }

    [Theory][MemberData(nameof(RankSpecs))]
    public void BothOverloadsHaveLiteralCooldownsAndPreserveEveryOtherDefinitionField(string spec, int rank)
    {
        var r = Request(rank, spec); var before = JsonSerializer.Serialize(r);
        var baseline = PreparedAbilities(Request(-1, spec));
        foreach (var defs in new[] {
            ForeverRogueSpellbook.Create(spec, r.Talents, r.Tuning, 0, true, true),
            ForeverRogueSpellbook.Create(spec, r.Talents, r.Tuning, 0, true, true, true) })
        {
            Assert.Equal(baseline.Select(x => x.Key), defs.Select(x => x.Key));
            foreach (var d in defs)
            {
                var old = baseline.Single(x => x.Key == d.Key);
                var a = JsonSerializer.SerializeToNode(old)!.AsObject();
                var b = JsonSerializer.SerializeToNode(d)!.AsObject();
                if (d.Key is "vanish" or "blind")
                {
                    Assert.Equal(Cooldown(d.Key, rank), d.CooldownSeconds);
                    Assert.Equal(1, d.MaxCharges); Assert.Null(d.ChargeRecoverySeconds);
                    a.Remove("CooldownSeconds"); b.Remove("CooldownSeconds");
                }
                Assert.True(JsonNode.DeepEquals(a, b), d.Key);
            }
        }
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }

    [Theory][MemberData(nameof(RankSpecs))]
    public void FullDefinitionsHaveOnlyTheTwoCooldownDifferences(string spec, int rank)
    {
        var baseline = PreparedAbilities(Request(-1, spec));
        var current = PreparedAbilities(Request(rank, spec));
        Assert.Equal(baseline.Select(a => a.Key), current.Select(a => a.Key));
        foreach (var old in baseline)
        {
            var candidate = current.Single(a => a.Key == old.Key);
            var a = JsonSerializer.SerializeToNode(old)!.AsObject();
            var b = JsonSerializer.SerializeToNode(candidate)!.AsObject();
            if (old.Key is "vanish" or "blind" && rank > 0)
            { a.Remove("CooldownSeconds"); b.Remove("CooldownSeconds"); }
            Assert.True(JsonNode.DeepEquals(a, b), old.Key);
        }
    }

    [Theory][MemberData(nameof(AbilitiesAndRanks))]
    public void SuccessfulStartOwnsChargeAndRecoveryBeforeQueuedCompletion(string ability, int rank)
    {
        var h = new Harness(Request(rank));
        h.At(2.25m, () => {
            var state = h.Actor.Abilities[ability]; Assert.True(state.IsReady(2.25m));
            h.Cast(ability); Assert.Equal(0, state.CurrentCharges);
            Assert.Empty(h.Events(CombatEventType.AbilityCastCompleted, ability));
            Assert.Equal(100m, h.Actor.Resources["energy"].Current);
            Assert.Equal(2.25m + Cooldown(ability, rank), state.GetNextReadyTime(2.25m));
        }); h.Run();
    }

    [Theory][MemberData(nameof(BoundaryCases))]
    public void IndependentActualStartsEnforceInclusiveRecoveryAndFailedAttemptsDoNotShortenIt(string ability, int rank, decimal offset)
    {
        var boundary = 2.25m + Cooldown(ability, rank);
        var h = new Harness(Request(rank), boundary + .01m);
        h.At(2.25m, () => h.Cast(ability));
        h.At(3.5m, () => {
            var before = h.Actor.Abilities[ability].GetNextReadyTime(3.5m);
            var fail = h.Executor.TryStartAbility(h.Context, "rogue", "target", ability);
            Assert.False(fail.Success); Assert.Contains("cooldown", fail.FailureReason);
            Assert.Equal(before, h.Actor.Abilities[ability].GetNextReadyTime(3.5m));
        });
        h.At(boundary + offset, () => {
            var state = h.Actor.Abilities[ability];
            Assert.Equal(offset >= 0, state.IsReady(h.Context.CurrentTimeSeconds));
            Assert.Equal(offset >= 0 ? 1 : 0, state.CurrentCharges);
            var result = h.Executor.TryStartAbility(h.Context, "rogue", "target", ability);
            Assert.Equal(offset >= 0, result.Success);
            Assert.Equal(offset < 0 ? boundary : boundary + offset + Cooldown(ability, rank),
                state.GetNextReadyTime(h.Context.CurrentTimeSeconds));
        }); h.Run();
    }

    [Theory][InlineData(1, 255)][InlineData(2, 210)]
    public void EarlierSecondVanishActuallyEnablesOwned400DamageAmbush(int rank, int seconds)
    {
        const decimal start = 2.25m;
        var h = new Harness(Request(rank), start + seconds + 1);
        Guid firstId = default;
        h.At(start, () => h.Cast("vanish"));
        h.At(start + .1m, () => {
            Assert.True(h.Combat.IsStealthed(h.Context));
            var aura = Assert.Single(h.Actor.ActiveAuras, a => a.Definition.Key == "rogue-stealth");
            Assert.Equal("rogue", aura.SourceActorKey);
            h.Cast("ambush", "target-2");
        });
        h.At(start + .2m, () => {
            var damage = Assert.Single(h.Events(CombatEventType.Damage, "ambush"));
            Assert.Equal(400m, damage.Amount); firstId = damage.AbilityExecutionId!.Value;
            Assert.Equal(40m, h.Actor.Resources["energy"].Current);
            Assert.Equal(1, h.Combat.ComboPointsFor("target-2"));
            Assert.Equal(0, h.Combat.ComboPointsFor("target"));
            Assert.False(h.Combat.IsStealthed(h.Context));
        });
        h.At(start + seconds, () => {
            // Explicit isolated setup only: refill Energy. No cooldown/GCD/CP reset.
            h.Actor.Resources["energy"].SetCurrent(100);
            h.Cast("vanish");
        });
        h.At(start + seconds + .1m, () => {
            Assert.True(h.Combat.IsStealthed(h.Context)); h.Cast("ambush");
        }); h.Run();
        var later = Assert.Single(h.Events(CombatEventType.Damage, "ambush"), e => e.TargetActorKey == "target");
        Assert.Equal(start + seconds + .1m, later.TimeSeconds); Assert.Equal(400m, later.Amount);
        Assert.Equal("rogue", later.SourceActorKey); Assert.NotEqual(firstId, later.AbilityExecutionId);
        var spend = Assert.Single(h.Events(CombatEventType.ResourceChanged, "ambush"), e => e.TimeSeconds == later.TimeSeconds);
        Assert.Equal(-60m, spend.Amount); Assert.Equal(40m, spend.ResourceTotalAfter);
        Assert.Null(spend.AbilityExecutionId); // Ordinary spend events have no execution ID.
        Assert.Equal(later.SourceActorKey, spend.SourceActorKey);
        Assert.Equal(later.AbilityKey, spend.AbilityKey);
        Assert.Equal(later.AbilityExecutionId, Assert.Single(h.Events(CombatEventType.AbilityCastCompleted, "ambush"), e => e.TimeSeconds == later.TimeSeconds).AbilityExecutionId);
        Assert.Equal(1, h.Combat.ComboPointsFor("target")); Assert.Equal(1, h.Combat.ComboPointsFor("target-2"));
        Assert.Equal(0, h.OtherCombat.ComboPointsFor("target"));
        Assert.False(h.Combat.IsStealthed(h.Context)); Assert.Equal(2, h.Combat.StealthOpeners);
        Assert.Equal(new[] { start, start + seconds }, h.Events(CombatEventType.AbilityCastStarted, "vanish").Select(e => e.TimeSeconds));
        Assert.Empty(h.Events(CombatEventType.ResourceChanged, "vanish"));
        Assert.Equal(800m, h.Context.Summary.ActorSummaries["rogue"].DamageDone);
    }

    [Theory][InlineData(0, 300)][InlineData(1, 255)][InlineData(2, 210)]
    public void RealSchedulerDispatchesNextDecisionAtStartOwnedRecovery(int rank, int seconds)
    {
        const decimal start = 2.25m;
        var h = new Harness(Request(rank), 310);
        h.At(start, () => h.Cast("vanish"));
        h.At(start + .1m, () => h.Cast("ambush"));
        h.At(start + 1.2m, () => {
            Assert.False(h.Combat.IsStealthed(h.Context));
            Assert.True(h.Actor.IsInputReady(h.Context.CurrentTimeSeconds));
            h.PriorityEnabled = true;
        });
        // No test event is scheduled near either cooldown boundary. The actual
        // priority executor owns every subsequent decision and the second cast.
        h.Run();
        var decisions = h.Dispatched.Where(e => e.Type == CombatEventType.RotationDecision && e.EffectKey is null).ToArray();
        var casts = h.Events(CombatEventType.AbilityCastStarted, "vanish");
        Assert.Equal(2, casts.Length); Assert.Equal(start, casts[0].TimeSeconds);
        Assert.NotEmpty(decisions);
        Assert.Equal(casts[1].TimeSeconds, decisions[0].TimeSeconds);
        output.WriteLine(JsonSerializer.Serialize(new {
            rank, actualFirstStart = casts[0].TimeSeconds,
            expectedNextDecision = start + seconds,
            actualDecisions = decisions.Select(e => new { e.TimeSeconds, e.SourceActorKey, e.TargetActorKey, e.IsInternal }),
            actualCasts = casts.Select(e => new { e.TimeSeconds, e.AbilityExecutionId, e.SourceActorKey, e.TargetActorKey }),
            firstAmbushDamage = Assert.Single(h.Events(CombatEventType.Damage, "ambush")).Amount
        }));
        Assert.Equal(start + seconds, decisions[0].TimeSeconds);
        Assert.Equal(start + seconds, casts[1].TimeSeconds);
    }

    [Theory][InlineData(-1)][InlineData(0)][InlineData(1)][InlineData(2)]
    public void ZeroEnergyVanishPreservesNormalGcdButStillRequiresInputReady(int rank)
    {
        var h = new Harness(Request(rank));
        h.At(1, () => { h.Actor.ConfigureActionTiming(0, .08m); h.Cast("sinister-strike"); });
        h.At(1.079m, () => Assert.False(h.Executor.TryStartAbility(h.Context, "rogue", "target", "vanish").Success));
        h.At(1.08m, () => {
            h.Actor.Resources["energy"].SetCurrent(0);
            Assert.Equal(2m, h.Actor.GlobalCooldownReadyAtSeconds); h.Cast("vanish");
            Assert.Equal(2m, h.Actor.GlobalCooldownReadyAtSeconds);
        }); h.Run();
        Assert.Empty(h.Events(CombatEventType.ResourceChanged, "vanish"));
        Assert.Equal(0m, h.Actor.Resources["energy"].Current);
        Assert.Equal(1.08m, Assert.Single(h.Events(CombatEventType.AbilityCastStarted, "vanish")).TimeSeconds);
    }

    [Fact]
    public void PublicPriorityRetainsOrdinaryInputGateEvenForOffGcdVanish()
    {
        var h = new Harness(Request());
        h.At(1, () => { h.Actor.ConfigureActionTiming(0, .08m); h.Cast("sinister-strike"); });
        h.At(1.08m, () => {
            Assert.True(h.Actor.IsOffGlobalCooldownInputReady(1.08m)); Assert.False(h.Actor.IsInputReady(1.08m));
            h.PriorityEnabled = true;
        }); h.Run();
        Assert.Equal(2.08m, Assert.Single(h.Events(CombatEventType.AbilityCastStarted, "vanish")).TimeSeconds);
    }

    [Theory][InlineData(0, 30)][InlineData(1, 25)][InlineData(2, 20)]
    public void BlindRetainsDirtyTricksCostGcdAndTenSecondOwnedMarker(int dirtyTricks, int cost)
    {
        var r = Request(); r.Talents.Ranks["dirty-tricks"] = dirtyTricks;
        var h = new Harness(r, 12);
        h.At(1, () => { h.Cast("blind", "target-2"); Assert.Equal(2m, h.Actor.GlobalCooldownReadyAtSeconds);
            Assert.Equal(100m, h.Actor.Resources["energy"].Current); }); h.Run();
        var spend = Assert.Single(h.Events(CombatEventType.ResourceChanged, "blind"));
        Assert.Equal(-(decimal)cost, spend.Amount); Assert.Equal(100m-cost, spend.ResourceTotalAfter);
        var aura = Assert.Single(h.Events(CombatEventType.AuraApplied, "blind"));
        Assert.Equal("rogue", aura.SourceActorKey); Assert.Equal("target-2", aura.TargetActorKey);
        Assert.Equal(1m, aura.TimeSeconds); Assert.Equal(11m, Assert.Single(h.Events(CombatEventType.AuraRemoved, "blind")).TimeSeconds);
        Assert.Empty(h.Events(CombatEventType.Damage, "blind"));
    }

    [Fact]
    public void RealDamageStealthAndLiveCutthroatGateVanishWithoutResettingRecovery()
    {
        var h = new Harness(Request(), 8);
        bool Enabled() { h.RefreshRotation(); return h.Rotation.Entries[0].IsEnabled; }
        h.At(.1m, () => Assert.False(Enabled())); // Not in combat.
        h.At(1, () => h.Cast("sinister-strike"));
        h.At(2.1m, () => { Assert.True(Enabled()); h.Aura("rogue-stealth", .5m); Assert.False(Enabled()); });
        h.At(2.7m, () => { Assert.True(Enabled()); h.Aura("rogue-cutthroat", .5m); Assert.False(Enabled());
            Assert.False(h.Combat.IsStealthed(h.Context)); });
        h.At(3.3m, () => { Assert.True(Enabled()); h.Cast("vanish"); });
        h.At(3.4m, () => { Assert.False(Enabled()); h.Auras.RemoveAuras(h.Context, h.Actor, "rogue-stealth", "rogue");
            h.Aura("rogue-cutthroat", .5m); Assert.False(Enabled()); });
        h.At(4, () => { Assert.True(Enabled()); Assert.False(h.Actor.Abilities["vanish"].IsReady(4));
            var failed = h.Executor.TryStartAbility(h.Context, "rogue", "target", "vanish");
            Assert.False(failed.Success); Assert.Contains("cooldown", failed.FailureReason); });
        h.Run(); Assert.Single(h.Events(CombatEventType.AbilityCastStarted, "vanish"));
    }

    [Fact]
    public void CutthroatOnlyAmbushIsNotAVanishOrStealthCast()
    {
        var h = new Harness(Request());
        h.At(1, () => { h.Aura("rogue-cutthroat", 10); h.Cast("ambush"); }); h.Run();
        Assert.Equal(400m, Assert.Single(h.Events(CombatEventType.Damage, "ambush")).Amount);
        Assert.Empty(h.Events(CombatEventType.AbilityCastStarted, "vanish"));
        Assert.DoesNotContain(h.Context.Timeline, e => e.EffectKey == "stealth-state");
        Assert.False(h.Combat.IsStealthed(h.Context));
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void CompletedPreparationResetsOnlyTheExistingSetAndNextStartsOwnNewRecovery(int rank)
    {
        var r = Request(rank); r.Talents.Ranks["preparation"] = 1;
        r.Talents.Ranks["cold-blood"] = 1; r.Talents.Ranks["ghostly-strike"] = 1;
        r.Talents.AssassinationPointsSpent = 16; r.Talents.CombatPointsSpent = 0;
        var h = new Harness(r, 7);
        string[] resetKeys = ["vanish", "evasion", "sprint", "blind", "cold-blood", "ghostly-strike"];
        h.At(1, () => h.Cast("vanish")); h.At(2, () => h.Cast("blind"));
        h.At(2.1m, () => {
            foreach (var key in resetKeys.Except(["vanish", "blind"]))
                Assert.True(h.Actor.Abilities[key].ConsumeCharge(2.1m)); // Explicit control state for other reset members.
            h.Actor.StartGlobalCooldown(2.1m, 2); h.Aura("test-preserved", 20);
            h.Cast("preparation"); Assert.Equal(0, h.Actor.Abilities["preparation"].CurrentCharges);
        });
        h.At(2.2m, () => {
            foreach (var key in resetKeys) Assert.True(h.Actor.Abilities[key].IsReady(2.2m), key);
            Assert.False(h.Actor.Abilities["preparation"].IsReady(2.2m));
            Assert.Equal(602.1m, h.Actor.Abilities["preparation"].GetNextReadyTime(2.2m));
            Assert.Equal(4.1m, h.Actor.GlobalCooldownReadyAtSeconds); Assert.Equal(70m, h.Actor.Resources["energy"].Current);
            Assert.Contains(h.Actor.ActiveAuras, a => a.Definition.Key == "test-preserved");
            Assert.True(h.Combat.IsStealthed(h.Context)); Assert.Equal(6, h.Combat.PreparationResets);
        });
        h.At(5, () => h.Cast("vanish")); h.At(5.1m, () => h.Cast("blind"));
        h.At(5.2m, () => {
            Assert.Equal(5m+Cooldown("vanish", rank), h.Actor.Abilities["vanish"].GetNextReadyTime(5.2m));
            Assert.Equal(5.1m+Cooldown("blind", rank), h.Actor.Abilities["blind"].GetNextReadyTime(5.2m));
        }); h.Run();
    }

    public static IEnumerable<object[]> FailureCases()
    {
        foreach (var ability in new[] { "vanish", "blind" })
        foreach (var failure in new[] { "dead-source", "missing-source", "dead-target", "missing-target" })
            yield return [ability, failure];
    }
    [Theory][MemberData(nameof(FailureCases))]
    public void RejectedActorsDoNotConsumeACharge(string ability, string failure)
    {
        var h = new Harness(Request());
        h.At(1, () => {
            if (failure == "dead-source") h.Actor.TakeDamage(10000000);
            if (failure == "dead-target") h.Target.TakeDamage(10000000);
            Assert.False(h.Executor.TryStartAbility(h.Context, failure == "missing-source" ? "missing" : "rogue",
                failure == "missing-target" ? "missing" : "target", ability).Success);
            Assert.Equal(1, h.Actor.Abilities[ability].CurrentCharges);
        }); h.Run(); Assert.Empty(h.Events(CombatEventType.AbilityCastStarted, ability));
    }

    [Theory][InlineData("vanish", "cancelled")][InlineData("blind", "cancelled")]
    [InlineData("vanish", "dead-target")][InlineData("blind", "dead-target")]
    [InlineData("vanish", "dead-source")][InlineData("blind", "dead-source")]
    [InlineData("blind", "drained-energy")]
    public void InvalidOrCancelledCompletionDoesNotRefundStartOwnedCharge(string ability, string failure)
    {
        var h = new Harness(Request());
        h.At(1, () => {
            h.Cast(ability);
            if (failure == "cancelled") h.OnStarted = ev => {
                if (ev.AbilityKey == ability) h.Context.CancelAbilityExecution(ev.AbilityExecutionId!.Value, ev.TimeSeconds);
            };
            if (failure == "dead-target") h.Target.TakeDamage(10000000);
            if (failure == "dead-source") h.Actor.TakeDamage(10000000);
            if (failure == "drained-energy") h.Actor.Resources["energy"].SetCurrent(0);
        }); h.Run();
        Assert.Equal(0, h.Actor.Abilities[ability].CurrentCharges);
        Assert.Equal(1m+Cooldown(ability, 2), h.Actor.Abilities[ability].GetNextReadyTime(3));
        Assert.Empty(h.Events(CombatEventType.ResourceChanged, ability));
        // Existing Rogue completion observers are deliberately not repaired here.
        // Vanish observer can still apply Stealth; no successful-completion claim.
        if (ability == "blind") Assert.Empty(h.Events(CombatEventType.AuraApplied, "blind"));
    }

    [Fact]
    public void PreparedGraphsAndTwoActorsOwnIndependentCooldownsAndCallerRanks()
    {
        var r = Request(); var held = PreparedAbilities(r);
        foreach (var rank in new[] { 2, -1, 1, 0, 2 })
        {
            if (rank < 0) r.Talents.Ranks.Remove("elusiveness"); else r.Talents.Ranks["elusiveness"] = rank;
            var before = JsonSerializer.Serialize(r); var fresh = PreparedAbilities(r);
            foreach (var key in new[] { "vanish", "blind" })
            {
                Assert.Equal(Cooldown(key, rank), fresh.Single(a => a.Key == key).CooldownSeconds);
                Assert.Equal(Cooldown(key, 2), held.Single(a => a.Key == key).CooldownSeconds);
            }
            Assert.Equal(before, JsonSerializer.Serialize(r));
        }
        var h = new Harness(Request(2), other: Request(0));
        h.At(1, () => {
            h.Cast("vanish"); Assert.True(h.Other.Abilities["vanish"].IsReady(1));
            h.Cast("vanish", source: "other-rogue");
            Assert.Equal(211m, h.Actor.Abilities["vanish"].GetNextReadyTime(1));
            Assert.Equal(301m, h.Other.Abilities["vanish"].GetNextReadyTime(1));
            Assert.NotSame(h.Actor.Abilities["vanish"].Definition, h.Other.Abilities["vanish"].Definition);
        }); h.Run();
    }

    [Fact]
    public async Task ConcurrentPreparationsRetainRankAndComparerOwnership()
    {
        var requests = Enumerable.Range(0, 20).Select(i => Request(i % 4 - 1)).ToArray();
        var before = requests.Select(r => JsonSerializer.Serialize(r)).ToArray();
        var defs = await Task.WhenAll(requests.Select(r => Task.Run(() => PreparedAbilities(r))));
        for (var i = 0; i < requests.Length; i++)
        {
            Assert.Equal(Cooldown("vanish", i % 4 - 1), defs[i].Single(a => a.Key == "vanish").CooldownSeconds);
            Assert.Equal(before[i], JsonSerializer.Serialize(requests[i]));
            Assert.Same(requests[i].Talents.Ranks.Comparer, RogueSimulationRequestSnapshot.Create(requests[i]).Talents.Ranks.Comparer);
        }
        var caseSensitive = Request(); caseSensitive.Talents.Ranks = new(StringComparer.Ordinal) { ["ELUSIVENESS"] = 2 };
        Assert.Equal(300m, PreparedAbilities(caseSensitive).Single(a => a.Key == "vanish").CooldownSeconds);
        Assert.Same(StringComparer.Ordinal, RogueSimulationRequestSnapshot.Create(caseSensitive).Talents.Ranks.Comparer);
    }

    [Theory][InlineData(-1)][InlineData(3)]
    public void InvalidRanksRemainRejected(int rank)
    {
        var r = Request(); r.Talents.Ranks["elusiveness"] = rank;
        Assert.Throws<ArgumentException>(() => r.Talents.Validate(60, r.Specialization));
        Assert.Throws<ArgumentException>(() => RogueSimRunner.Preview(r));
        Assert.Throws<ArgumentException>(() => new RogueSimRunner().Run(r));
    }

    [Theory][MemberData(nameof(RankSpecs))]
    public void AllSpecFixedPrioritiesReallyUseVanishAndBlindRemainsUtilityOnly(string spec, int rank)
    {
        var r = Request(rank, spec); r.DurationSeconds = 5;
        var previous = SimulationInputTimingScope.AdditionalDelaySeconds;
        try
        {
            SimulationInputTimingScope.AdditionalDelaySeconds = 0;
            var preview = RogueSimRunner.Preview(r);
            Assert.Contains(preview.Rotation.Entries, e => e.AbilityKey == "vanish");
            Assert.DoesNotContain(preview.Rotation.Entries, e => e.AbilityKey == "blind");
            var result = new RogueSimRunner().Run(r);
            Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.AbilityCastStarted && e.AbilityKey == "vanish");
            Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage && e.WeaponHandKey == WeaponHandKeys.MainHand);
            Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.Damage && e.WeaponHandKey == WeaponHandKeys.OffHand);
            Assert.DoesNotContain(result.Run.Timeline, e => e.AbilityKey == "blind");
            r.UseUtilityAbilities = true; r.DurationSeconds = 30;
            Assert.Contains(RogueSimRunner.Preview(r).Rotation.Entries, e => e.AbilityKey == "blind");
            Assert.Contains(new RogueSimRunner().Run(r).Run.Timeline, e => e.Type == CombatEventType.AbilityCastStarted && e.AbilityKey == "blind");
        }
        finally { SimulationInputTimingScope.AdditionalDelaySeconds = previous; }
        Assert.DoesNotContain("elusiveness", ForeverRogueTalentBuild.Default(spec).Ranks.Keys);
        Assert.Equal(2, Assert.Single(ForeverRogueTalentBuild.SupportedTalentMetadata, t => t.Key == "elusiveness").MaxRank);
        if (spec != "subtlety") Assert.DoesNotContain(ForeverHistoricalCasterTalentCatalog.ForSpecialization("rogue", spec), t => t.Key == "elusiveness");
    }
}
