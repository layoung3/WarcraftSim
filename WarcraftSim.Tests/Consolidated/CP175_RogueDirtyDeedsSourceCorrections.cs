using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Tests.Consolidated;

/// <summary>Historical decoded tooltip: Dirty Deeds subtracts 10/20 Energy.
/// Base costs, hit rules, control and lifecycle are preserved local conventions,
/// not current-client certification or strict talent admission.</summary>
public sealed class Cp175RogueDirtyDeedsSourceCorrections
{
    private static readonly decimal[] GarroteCosts = [50, 50, 40, 30];
    private static readonly decimal[] CheapShotCosts = [60, 60, 50, 40];
    private static decimal Cost(string ability, int rank) =>
        (ability == "garrote" ? GarroteCosts : CheapShotCosts)[rank + 1];

    private static RogueSimRequest Request(int rank = 2, string spec = "subtlety")
    {
        var r = RogueSimRequest.CreatePreset(spec);
        r.Talents = new() { AssassinationPointsSpent = 10, CombatPointsSpent = 10,
            SubtletyPointsSpent = 31, Ranks = new(StringComparer.OrdinalIgnoreCase) };
        if (rank >= 0) r.Talents.Ranks["dirty-deeds"] = rank;
        r.StartInStealth = true; r.Character.BaseStats = new();
        r.BothWeaponsDaggers = false; r.IsBehindTarget = true;
        r.MainHandWeaponType = r.OffHandWeaponType = "sword";
        r.UseInstantPoison = false; r.MainHandPoison = r.OffHandPoison = "none";
        r.Tuning.MainHandWeaponDamage = 100; r.Tuning.OffHandWeaponDamage = 80;
        r.Tuning.MainHandSpeedSeconds = r.Tuning.OffHandSpeedSeconds = 2;
        r.Tuning.MainHandHitPercent = r.Tuning.OffHandHitPercent = 100;
        r.Tuning.PhysicalCritPercent = 0; r.Tuning.EnergyPerSecond = 10;
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
            new() { AbilityKey = "garrote", Priority = 1 },
            new() { AbilityKey = "cheap-shot", Priority = 2 } ] };
        public List<CombatEvent> Dispatched { get; } = [];
        public SimulationRunResult? Result { get; private set; }
        private readonly Dictionary<string, Action> _actions = new();
        private readonly RoguePoisonProcessor _poisons;
        public Harness(RogueSimRequest r, decimal duration = 2, RogueSimRequest? other = null, int seed = 35)
        {
            Context = new(new() { Seed = seed, DurationSeconds = duration, CaptureTimeline = true,
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
            // No weapon stream or priority executor in isolated cost fixtures.
            Combat = combat = new(r, Actor, Rotation, Executor, Auras, auto, []);
            OtherCombat = new(other ?? r, Other, new(), Executor, Auras, auto, []);
            _poisons = new(r, Actor, Auras, Executor);
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
            // The validated request keeps positive regeneration. Suspend the actor's
            // rate after every dispatch to isolate actual queued-completion spending.
            foreach (var actor in Context.Actors.Values)
                if (actor.Resources.TryGetValue("energy", out var resource)) resource.RegenerationPerSecond = 0;
            if (ev.EffectKey is { } key && _actions.TryGetValue(key, out var action)) action();
        }
        public void Cast(string ability, string target = "target", string source = "rogue") =>
            Assert.True(Executor.TryStartAbility(Context, source, target, ability).Success,
                $"{source} could not cast {ability} at {Context.CurrentTimeSeconds}");
        public CombatEvent[] Events(CombatEventType type, string ability) => Context.Timeline.Where(e =>
            e.Type == type && e.AbilityKey == ability).ToArray();
        public void Stealth(SimulationActorState? actor = null, string aura = "rogue-stealth", decimal duration = 30) =>
            Auras.ApplyAura(Context, actor ?? Actor, new() { Key = aura, DurationSeconds = duration },
                (actor ?? Actor).Key, "test-stealth", "test-state");
        public void Run() => Result = new SimulationEngine([Executor, Auras, Combat, OtherCombat, _poisons, this]).Run(Context);
    }

    public static IEnumerable<object[]> RankSpecs()
    {
        foreach (var spec in new[] { "assassination", "combat", "subtlety" })
        for (var rank = -1; rank <= 2; rank++) yield return [spec, rank];
    }
    public static IEnumerable<object[]> Openers()
    {
        foreach (var ability in new[] { "garrote", "cheap-shot" })
        for (var rank = -1; rank <= 2; rank++) yield return [ability, rank];
    }
    public static IEnumerable<object[]> Thresholds()
    {
        foreach (var ability in new[] { "garrote", "cheap-shot" })
        for (var rank = 0; rank <= 2; rank++)
        foreach (var point in new[] { "below", "exact", "above", "former-base" })
            yield return [ability, rank, point];
    }

    [Theory][MemberData(nameof(RankSpecs))]
    public void BothSpellbookSignaturesHaveLiteralHistoricalCosts(string spec, int rank)
    {
        var r = Request(rank, spec); var before = JsonSerializer.Serialize(r);
        foreach (var definitions in new[] {
            ForeverRogueSpellbook.Create(spec, r.Talents, r.Tuning, 0, false, true),
            ForeverRogueSpellbook.Create(spec, r.Talents, r.Tuning, 0, false, true, false) })
        foreach (var ability in new[] { "garrote", "cheap-shot" })
        {
            var d = Assert.Single(definitions, d => d.Key == ability);
            var cost = Assert.Single(d.ResourceCosts);
            Assert.Equal("energy", cost.ResourceKey); Assert.False(cost.IsPercentOfMaximum);
            Assert.Equal(Cost(ability, rank), cost.Amount);
        }
        Assert.Equal(before, JsonSerializer.Serialize(r));
    }

    [Theory][MemberData(nameof(RankSpecs))]
    public void AllDefinitionFieldsExceptTheTwoCostsRemainIdentical(string spec, int rank)
    {
        var baseline = PreparedAbilities(Request(0, spec)); var candidate = PreparedAbilities(Request(rank, spec));
        Assert.Equal(baseline.Select(x => x.Key), candidate.Select(x => x.Key));
        foreach (var old in baseline)
        {
            var current = Assert.Single(candidate, x => x.Key == old.Key);
            var a = JsonSerializer.SerializeToNode(old)!.AsObject();
            var b = JsonSerializer.SerializeToNode(current)!.AsObject();
            if (old.Key is "garrote" or "cheap-shot") { a.Remove("ResourceCosts"); b.Remove("ResourceCosts"); }
            Assert.True(JsonNode.DeepEquals(a, b), old.Key);
        }
    }

    [Theory][MemberData(nameof(Openers))]
    public void QueuedCompletionSpendsExactlyOnceAndRetainsEffects(string ability, int rank)
    {
        var h = new Harness(Request(rank), 20);
        h.At(1, () => { h.Cast(ability); Assert.Equal(100m, h.Actor.Resources["energy"].Current);
            Assert.Empty(h.Events(CombatEventType.ResourceChanged, ability)); });
        h.Run(); var cost = Cost(ability, rank);
        var spend = Assert.Single(h.Events(CombatEventType.ResourceChanged, ability));
        Assert.Equal(1m, spend.TimeSeconds); Assert.Equal(-cost, spend.Amount);
        Assert.Equal(100m - cost, spend.ResourceTotalAfter);
        Assert.Equal($"rogue spent {cost:0.##} energy.", spend.Description);
        Assert.Equal(100m - cost, h.Actor.Resources["energy"].Current);
        Assert.Equal(1, h.Combat.StealthOpeners); Assert.False(h.Combat.IsStealthed(h.Context));
        Assert.Equal(ability == "garrote" ? 1 : 2, h.Combat.ComboPointsFor("target"));
        Assert.Equal(0, h.Combat.ComboPointsFor("target-2"));
        var applied = Assert.Single(h.Events(CombatEventType.AuraApplied, ability));
        var removed = Assert.Single(h.Events(CombatEventType.AuraRemoved, ability));
        Assert.Equal(1m, applied.TimeSeconds); Assert.Equal(ability == "garrote" ? 19m : 5m, removed.TimeSeconds);
        Assert.Equal("target", applied.TargetActorKey);
        var damage = h.Events(CombatEventType.Damage, ability);
        if (ability == "garrote")
        {
            Assert.Equal(new decimal[] { 4, 7, 10, 13, 16, 19 }, damage.Select(x => x.TimeSeconds));
            Assert.All(damage, e => { Assert.Equal(65m, e.Amount); Assert.True(e.IsPeriodic); });
            Assert.Equal(390m, damage.Sum(x => x.Amount));
        }
        else Assert.Empty(damage);
    }

    [Theory][MemberData(nameof(Thresholds))]
    public void IndependentActorExactBelowAboveAndFormerBaseAffordability(string ability, int rank, string point)
    {
        var cost = Cost(ability, rank);
        var energy = point switch { "below" => cost - .01m, "above" => cost + .01m,
            "former-base" => ability == "garrote" ? 50m : 60m, _ => cost };
        var h = new Harness(Request(rank));
        h.At(1, () => {
            h.Actor.Resources["energy"].SetCurrent(energy);
            var ready = ResourceAvailabilityCalculator.GetNextAffordableTime(h.Actor,
                h.Actor.Abilities[ability].Definition, h.Context.CurrentTimeSeconds);
            if (point == "below") Assert.Null(ready); else Assert.Equal(1m, ready);
            Assert.Equal(point != "below", h.Executor.TryStartAbility(h.Context, "rogue", "target", ability).Success);
            Assert.Equal(energy, h.Actor.Resources["energy"].Current);
        });
        h.Run();
        if (point == "below")
        {
            Assert.Equal(energy, h.Actor.Resources["energy"].Current);
            Assert.Empty(h.Events(CombatEventType.ResourceChanged, ability));
            Assert.Empty(h.Events(CombatEventType.AuraApplied, ability));
            Assert.Empty(h.Events(CombatEventType.AbilityCastCompleted, ability));
            Assert.Equal(0, h.Combat.ComboPointsFor("target")); Assert.Equal(0, h.Combat.StealthOpeners);
        }
        else
        {
            Assert.Equal(energy - cost, h.Actor.Resources["energy"].Current);
            Assert.Equal(-cost, Assert.Single(h.Events(CombatEventType.ResourceChanged, ability)).Amount);
        }
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void FailedGarrotePaysCorrectedCostWithoutQuickRecoveryRefund(int rank)
    {
        var r = Request(rank); r.Talents.Ranks["quick-recovery"] = 2; r.Tuning.MainHandHitPercent = 0;
        var h = new Harness(r, 20); h.At(1, () => h.Cast("garrote")); h.Run();
        Assert.Equal(-GarroteCosts[rank + 1], Assert.Single(h.Events(CombatEventType.ResourceChanged, "garrote")).Amount);
        Assert.Equal(100m - GarroteCosts[rank + 1], h.Actor.Resources["energy"].Current);
        Assert.Empty(h.Events(CombatEventType.AuraApplied, "garrote")); Assert.Equal(0, h.Combat.ComboPointsFor("target"));
        Assert.Equal(1, h.Combat.StealthOpeners); Assert.False(h.Combat.IsStealthed(h.Context));
        Assert.Equal(0m, h.Combat.QuickRecoveryEnergyRestored);
        Assert.Empty(h.Events(CombatEventType.ResourceChanged, "quick-recovery"));
        r.DurationSeconds = .1m;
        var result = new RogueSimRunner().Run(r);
        Assert.Equal(0m, result.Run.CustomMetrics["rogue-quick-recovery-energy"]);
        Assert.Contains(result.Run.Timeline, e => e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == "garrote" && e.Amount == -GarroteCosts[rank + 1]);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void PositionAndWeaponGatesRemainIndependentOfDiscount(int rank)
    {
        foreach (var behind in new[] { false, true })
        foreach (var daggers in new[] { false, true })
        {
            var r = Request(rank); r.IsBehindTarget = behind; r.BothWeaponsDaggers = daggers;
            r.MainHandWeaponType = r.OffHandWeaponType = daggers ? "dagger" : "sword";
            var a = PreparedAbilities(r);
            Assert.Equal(behind || rank > 0, a.Any(x => x.Key == "garrote"));
            Assert.Equal(behind && daggers, a.Any(x => x.Key == "backstab"));
            Assert.Equal(behind && daggers, a.Any(x => x.Key == "ambush"));
            Assert.Contains(a, x => x.Key == "cheap-shot");
        }
    }

    public static IEnumerable<object[]> StartGates()
    {
        foreach (var ability in new[] { "garrote", "cheap-shot" })
        foreach (var gate in new[] { "missing-stealth", "expired-stealth", "cutthroat-only", "dead-source",
            "missing-source", "dead-target", "missing-target", "missing-energy", "gcd", "input-lock" })
            yield return [ability, gate];
    }
    [Theory][MemberData(nameof(StartGates))]
    public void StartStillRequiresLiveStealthActorsResourcesAndReadiness(string ability, string gate)
    {
        var r = Request(); r.StartInStealth = false;
        var h = new Harness(r); h.At(1, () => {
            if (gate is not ("missing-stealth" or "expired-stealth" or "cutthroat-only")) h.Stealth();
            if (gate == "cutthroat-only") h.Stealth(aura: "rogue-cutthroat");
            if (gate == "dead-source") h.Actor.TakeDamage(10000000);
            if (gate == "dead-target") h.Target.TakeDamage(10000000);
            if (gate == "missing-energy") h.Actor.Resources.Remove("energy");
            if (gate == "gcd") h.Actor.StartGlobalCooldown(1, 10);
            if (gate == "input-lock") h.Actor.ConfigureActionTiming(10, 0);
            Assert.False(h.Executor.TryStartAbility(h.Context, gate == "missing-source" ? "absent" : "rogue",
                gate == "missing-target" ? "absent" : "target", ability).Success);
            // The gate itself is checked with no pool. Restore before unrelated
            // queued aura observers, which require the runner-owned Energy pool.
            if (gate == "missing-energy") h.Actor.AddResource(new ResourceState("energy", 100, 100, 0));
        });
        if (gate == "expired-stealth") h.At(.1m, () => h.Stealth(duration: .5m));
        h.Run(); Assert.Empty(h.Events(CombatEventType.ResourceChanged, ability));
        Assert.Empty(h.Events(CombatEventType.AuraApplied, ability));
        Assert.Equal(0, h.Combat.StealthOpeners); Assert.Equal(0, h.Combat.ComboPointsFor("target"));
    }

    [Theory][InlineData("garrote")][InlineData("cheap-shot")]
    public void CompletionDoesNotIntroduceANewStealthRecheck(string ability)
    {
        var h = new Harness(Request()); h.At(1, () => { h.Cast(ability);
            h.Auras.RemoveAuras(h.Context, h.Actor, "rogue-stealth", "rogue"); }); h.Run();
        Assert.Equal(-Cost(ability, 2), Assert.Single(h.Events(CombatEventType.ResourceChanged, ability)).Amount);
        Assert.Single(h.Events(CombatEventType.AuraApplied, ability));
        Assert.Equal(ability == "garrote" ? 1 : 2, h.Combat.ComboPointsFor("target"));
    }

    public static IEnumerable<object[]> CompletionGates()
    {
        foreach (var ability in new[] { "garrote", "cheap-shot" })
        foreach (var gate in new[] { "dead-source", "missing-source", "dead-target", "missing-target", "drained-energy" })
            yield return [ability, gate];
    }
    [Theory][MemberData(nameof(CompletionGates))]
    public void CompletionRechecksActorsAndResourcesBeforeSpending(string ability, string gate)
    {
        var h = new Harness(Request()); h.At(1, () => {
            h.Cast(ability);
            if (gate == "dead-source") h.Actor.TakeDamage(10000000);
            if (gate == "dead-target") h.Target.TakeDamage(10000000);
            if (gate == "missing-source") h.Context.Actors.Remove("rogue");
            if (gate == "missing-target") h.Context.Actors.Remove("target");
            if (gate == "drained-energy") h.Actor.Resources["energy"].SetCurrent(0);
        }); h.Run();
        Assert.Empty(h.Events(CombatEventType.ResourceChanged, ability));
        Assert.Empty(h.Events(CombatEventType.AuraApplied, ability));
        // Existing Rogue observer sees the queued completion independently of the
        // executor's early return. This test does not certify successful-opener metrics.
    }

    [Theory][MemberData(nameof(Openers))]
    public void ThousandCutsRotationRefreshCannotUndoOrDuplicateDirtyDeeds(string ability, int rank)
    {
        var r = Request(rank); r.Talents.Ranks["thousand-cuts"] = 1;
        var h = new Harness(r); h.At(1, () => {
            for (var i = 0; i < 5; i++) h.Combat.Process(h.Context, new() { Type = CombatEventType.Damage,
                SourceActorKey = "rogue", TargetActorKey = "target", AbilityKey = "rupture", Amount = 1,
                EffectDeliveryType = CombatEffectDeliveryType.Periodic });
            Assert.Equal(5, h.Combat.ThousandCutsStacks);
            h.Combat.Process(h.Context, new() { Type = CombatEventType.RotationDecision, SourceActorKey = "rogue" });
            Assert.Equal(Cost(ability, rank), h.Actor.Abilities[ability].Definition.ResourceCosts[0].Amount);
            h.Cast(ability);
        }); h.Run(); Assert.Equal(-Cost(ability, rank), Assert.Single(h.Events(CombatEventType.ResourceChanged, ability)).Amount);
    }

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void DamageAndDirtyTricksModifiersRemainSeparateFromTheCost(int rank)
    {
        var r = Request(rank); r.Talents.Ranks["opportunity"] = 2;
        r.Talents.Ranks["improved-garrote"] = 2; r.Talents.Ranks["dirty-tricks"] = 2;
        var h = new Harness(r, 20); h.At(1, () => h.Cast("garrote")); h.Run();
        Assert.Equal(6, h.Events(CombatEventType.Damage, "garrote").Length);
        Assert.All(h.Events(CombatEventType.Damage, "garrote"), e => Assert.Equal(78.65m, e.Amount));
        Assert.Equal(45m, h.Actor.Abilities["sap"].Definition.ResourceCosts[0].Amount);
        Assert.Equal(20m, h.Actor.Abilities["blind"].Definition.ResourceCosts[0].Amount);
        Assert.Equal(-GarroteCosts[rank + 1], Assert.Single(h.Events(CombatEventType.ResourceChanged, "garrote")).Amount);
    }

    [Fact]
    public void FreshPreparationsDoNotCacheRanksOrRepriceExistingGraphsOrMutateCallers()
    {
        var r = Request(); var retained = PreparedAbilities(r); var runner = new RogueSimRunner();
        var previous = SimulationInputTimingScope.AdditionalDelaySeconds;
        try
        {
            SimulationInputTimingScope.AdditionalDelaySeconds = 0;
            foreach (var rank in new[] { 2, 0, 1, -1 })
            {
                if (rank < 0) r.Talents.Ranks.Remove("dirty-deeds"); else r.Talents.Ranks["dirty-deeds"] = rank;
                r.DurationSeconds = .1m; var before = JsonSerializer.Serialize(r);
                var fresh = PreparedAbilities(r); Assert.NotSame(retained, fresh);
                foreach (var ability in new[] { "garrote", "cheap-shot" })
                {
                    Assert.Equal(Cost(ability, rank), fresh.Single(x => x.Key == ability).ResourceCosts[0].Amount);
                    Assert.Equal(Cost(ability, 2), retained.Single(x => x.Key == ability).ResourceCosts[0].Amount);
                }
                var run = runner.Run(r);
                Assert.Equal(-GarroteCosts[rank + 1], Assert.Single(run.Run.Timeline,
                    x => x.Type == CombatEventType.ResourceChanged && x.AbilityKey == "garrote").Amount);
                Assert.Equal(before, JsonSerializer.Serialize(r));
            }
        }
        finally { SimulationInputTimingScope.AdditionalDelaySeconds = previous; }
    }

    [Theory][InlineData("garrote")][InlineData("cheap-shot")]
    public void ChosenCasterOwnsItsDefinitionAndTargetComboPoints(string ability)
    {
        var h = new Harness(Request(2), other: Request(0));
        h.At(1, () => { h.Cast(ability, "target-2"); h.Cast(ability, "target", "other-rogue"); }); h.Run();
        var spends = h.Events(CombatEventType.ResourceChanged, ability);
        Assert.Equal(-Cost(ability, 2), Assert.Single(spends, x => x.SourceActorKey == "rogue").Amount);
        Assert.Equal(-Cost(ability, 0), Assert.Single(spends, x => x.SourceActorKey == "other-rogue").Amount);
        Assert.Equal(ability == "garrote" ? 1 : 2, h.Combat.ComboPointsFor("target-2"));
        Assert.Equal(0, h.Combat.ComboPointsFor("target"));
        Assert.Equal(ability == "garrote" ? 1 : 2, h.OtherCombat.ComboPointsFor("target"));
        Assert.Equal(0, h.OtherCombat.ComboPointsFor("target-2"));
        Assert.NotSame(h.Actor.Abilities[ability].Definition, h.Other.Abilities[ability].Definition);
    }

    [Theory][InlineData("garrote")][InlineData("cheap-shot")]
    public void EqualCastRngContinuationDoesNotGainATalentProcDraw(string ability)
    {
        string? effects = null; double? next = null;
        foreach (var rank in new[] { 0, 1, 2 })
        {
            var r = Request(rank); r.Tuning.PhysicalCritPercent = 37; r.Tuning.MainHandHitPercent = 91;
            r.TargetDodgePercent = 4; r.TargetArmor = 3000;
            var h = new Harness(r, 20); h.At(1, () => h.Cast(ability)); h.Run();
            var projection = JsonSerializer.Serialize(h.Context.Timeline.Where(e =>
                e.Type is CombatEventType.Damage or CombatEventType.AuraApplied or CombatEventType.AuraRemoved)
                .Select(e => new { e.TimeSeconds, e.Type, e.SourceActorKey, e.TargetActorKey, e.AbilityKey,
                    e.EffectKey, e.Amount, e.RawAmount, e.ResultKey, e.IsCritical, e.EffectDeliveryType }));
            var continuation = h.Context.Random.NextDouble();
            if (effects is not null) { Assert.Equal(effects, projection); Assert.Equal(next, continuation); }
            effects = projection; next = continuation;
        }
    }

    [Theory][MemberData(nameof(RankSpecs))]
    public void ActualFixedRunnerUsesGarroteAndNeverInventsCheapShotPriority(string spec, int rank)
    {
        var r = Request(rank, spec); r.DurationSeconds = .1m; r.UseUtilityAbilities = true;
        var previous = SimulationInputTimingScope.AdditionalDelaySeconds;
        try
        {
            SimulationInputTimingScope.AdditionalDelaySeconds = 0;
            var utilityPreview = RogueSimRunner.Preview(r);
            Assert.DoesNotContain(utilityPreview.Rotation.Entries, x => x.AbilityKey == "cheap-shot");
            r.UseUtilityAbilities = false;
            var preview = RogueSimRunner.Preview(r); var run = new RogueSimRunner().Run(r);
            Assert.DoesNotContain(preview.Rotation.Entries, x => x.AbilityKey == "cheap-shot");
            Assert.DoesNotContain(preview.Rotation.Entries, x => x.AbilityKey == "ambush");
            Assert.Contains(run.Run.Timeline, x => x.Type == CombatEventType.AuraApplied && x.AbilityKey == "garrote");
            Assert.Equal(-GarroteCosts[rank + 1], Assert.Single(run.Run.Timeline,
                x => x.Type == CombatEventType.ResourceChanged && x.AbilityKey == "garrote").Amount);
        }
        finally { SimulationInputTimingScope.AdditionalDelaySeconds = previous; }
    }

    [Theory][MemberData(nameof(RankSpecs))]
    public void LegacyValidationComparersAndDefaultStrictBoundariesAreUnchanged(string spec, int rank)
    {
        var r = Request(rank, spec); r.Talents.Validate(60, spec);
        var copy = RogueSimulationRequestSnapshot.Create(r);
        Assert.Same(r.Talents.Ranks.Comparer, copy.Talents.Ranks.Comparer); Assert.NotSame(r.Talents.Ranks, copy.Talents.Ranks);
        Assert.Equal(Math.Max(0, rank), copy.Talents.Rank("DIRTY-DEEDS"));
        Assert.DoesNotContain("dirty-deeds", ForeverRogueTalentBuild.Default(spec).Ranks.Keys);
        Assert.Equal(2, Assert.Single(ForeverRogueTalentBuild.SupportedTalentMetadata, x => x.Key == "dirty-deeds").MaxRank);
        if (spec != "subtlety") Assert.DoesNotContain(ForeverHistoricalCasterTalentCatalog.ForSpecialization("rogue", spec),
            x => x.Key == "dirty-deeds");
    }

    [Theory][InlineData(-1)][InlineData(3)]
    public void InvalidRanksRemainRejected(int rank)
    {
        var r = Request(); r.Talents.Ranks["dirty-deeds"] = rank;
        Assert.Throws<ArgumentException>(() => r.Talents.Validate(60, r.Specialization));
        Assert.Throws<ArgumentException>(() => RogueSimRunner.Preview(r));
        Assert.Throws<ArgumentException>(() => new RogueSimRunner().Run(r));
    }

    [Fact]
    public void CaseSensitiveCallerDictionaryIsNotSilentlyNormalized()
    {
        var r = Request(); r.Talents.Ranks = new(StringComparer.Ordinal) { ["DIRTY-DEEDS"] = 2 };
        var copy = RogueSimulationRequestSnapshot.Create(r);
        Assert.Same(StringComparer.Ordinal, copy.Talents.Ranks.Comparer);
        Assert.Equal(0, copy.Talents.Rank("dirty-deeds")); Assert.Equal(2, copy.Talents.Rank("DIRTY-DEEDS"));
        Assert.Equal(50m, PreparedAbilities(r).Single(x => x.Key == "garrote").ResourceCosts[0].Amount);
    }
}
