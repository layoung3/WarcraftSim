using System.Reflection;
using WarcraftSim.Api.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests.Consolidated;

public sealed class Cp175RogueThousandCutsLifetime
{
    private static RogueSimRequest Request(int rank = 1)
    {
        var r = RogueSimRequest.CreatePreset("subtlety");
        r.Talents = new() { SubtletyPointsSpent = 31, AssassinationPointsSpent = 20,
            Ranks = new() { ["hemorrhage"] = 1, ["thousand-cuts"] = rank } };
        r.StartInStealth = false; r.Character.BaseStats = new();
        r.MainHandPoison = r.OffHandPoison = "none"; r.UseInstantPoison = false;
        r.Tuning.MainHandHitPercent = r.Tuning.OffHandHitPercent = 100;
        r.Tuning.PhysicalCritPercent = 0; r.Tuning.EnergyPerSecond = 1;
        r.TargetMaximumHealth = 1000000; r.CaptureTimeline = true;
        return r;
    }

    private sealed class Harness : ICombatEventProcessor
    {
        public RogueSimRequest RequestValue { get; }
        public SimulationContext Context { get; }
        public SimulationActorState Actor => Context.GetActor("rogue")!;
        public ResourceState Energy => Actor.Resources["energy"];
        public RogueCombatProcessor Combat { get; }
        public AbilityExecutor Executor { get; }
        public AuraManager Auras { get; } = new();
        public List<CombatEvent> Dispatched { get; } = [];
        private readonly Dictionary<string, Action> _actions = [];
        public Harness(decimal duration = 25, int rank = 1, bool policy = true, Action<SimulationActorState>? configure = null)
        {
            var r = Request(rank); RequestValue = r;
            Context = new(new() { Seed = 1, DurationSeconds = duration, CaptureTimeline = true,
                PrimaryActorKey = "rogue" });
            var prepared = typeof(RogueSimRunner).GetMethod("Prepare", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [r])!;
            var abilities = (IReadOnlyList<AbilityDefinition>)prepared.GetType().GetProperty("Abilities")!.GetValue(prepared)!;
            foreach (var key in new[] { "rogue", "other", "target", "target-2" })
            {
                var actor = new SimulationActorState { Key = key, Name = key,
                    TeamKey = key.StartsWith("target") ? "enemy" : "raid", Level = 60, MaximumHealth = 1000000 };
                actor.InitializeHealth(1000000); Context.AddActor(actor);
            }
            Actor.AddResource(new ResourceState("energy", 100, 100, 0));
            foreach (var ability in abilities) Actor.AddAbility(ability);
            configure?.Invoke(Actor);
            RogueCombatProcessor? combat = null;
            Func<RogueCombatProcessor?> getCombat = () => combat;
            object Adapter(string name) => Activator.CreateInstance(typeof(RogueSimRunner).GetNestedType(name, BindingFlags.NonPublic)!,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [r, getCombat], null)!;
            Executor = new(Auras, (ICombatRollResolver)Adapter("RogueRollResolver"),
                (IDamageMitigationResolver)Adapter("RogueArmorMitigation"), resourceCostPolicy: policy ? () => combat : null);
            Combat = combat = new(r, Actor, new RotationProfile(), Executor, Auras, new AutoAttackProcessor(Executor), []);
        }
        public void At(decimal time, Action action)
        {
            var key = "action-" + _actions.Count; _actions.Add(key, action);
            Context.ScheduleEvent(new() { TimeSeconds = time, Type = CombatEventType.HealthChanged,
                SourceActorKey = "test", EffectKey = key, IsInternal = true });
        }
        public void Tick(decimal time, string source = "rogue", string target = "target", decimal amount = 1,
            bool periodic = true, string ability = "rupture") => Context.ScheduleEvent(new() {
                TimeSeconds = time, Type = CombatEventType.Damage, SourceActorKey = source, TargetActorKey = target,
                AbilityKey = ability, Amount = amount, EffectDeliveryType = periodic ? CombatEffectDeliveryType.Periodic : CombatEffectDeliveryType.Direct, ResultKey = CombatResultTypes.Hit });
        public void Process(SimulationContext context, CombatEvent ev)
        { Dispatched.Add(ev); Energy.RegenerationPerSecond = 0; if (ev.EffectKey is { } key && _actions.TryGetValue(key, out var action)) action(); }
        public AbilityUseResult Cast(string ability) => Executor.TryStartAbility(Context, "rogue", "target", ability);
        public decimal Price(string ability) => Actor.Abilities[ability].Definition.ResourceCosts[0].Amount;
        public void Run() => new SimulationEngine([Executor, Auras, Combat, this]).Run(Context);
        public CombatEvent[] Spends(string ability) => Context.Timeline.Where(e => e.Type == CombatEventType.ResourceChanged &&
            e.AbilityKey == ability && e.ResourceKey == "energy" && e.Amount < 0).ToArray();
    }

    [Theory]
    [InlineData("backstab", 60)][InlineData("hemorrhage", 35)]
    public void EveryStackHasImmediateLivePriceAndActualSpend(string ability, int baseline)
    {
        for (var stacks = 0; stacks <= 6; stacks++)
        {
            var h = new Harness(9); for (var i = 0; i < stacks; i++) h.Tick(i + 1);
            var expected = baseline - 3 * Math.Min(stacks, 5);
            h.At(7, () => { Assert.Equal(expected, h.Price(ability)); Assert.True(h.Cast(ability).Success); });
            h.At(8, () => { Assert.Equal(baseline, h.Price(ability)); Assert.Equal(0, h.Combat.ThousandCutsStacks); });
            h.Run(); Assert.Equal(-expected, Assert.Single(h.Spends(ability)).Amount);
            Assert.Equal(100 - expected, h.Energy.Current);
        }
    }

    [Theory]
    [InlineData("backstab", "10.999", 57)][InlineData("backstab", "11", 60)][InlineData("backstab", "11.001", 60)]
    [InlineData("hemorrhage", "10.999", 32)][InlineData("hemorrhage", "11", 35)][InlineData("hemorrhage", "11.001", 35)]
    public void ExpiryIsHalfOpenBeforeRealSpendWithoutRotation(string ability, string time, int expected)
    {
        var h = new Harness(); h.Tick(1);
        h.At(decimal.Parse(time, System.Globalization.CultureInfo.InvariantCulture), () => Assert.True(h.Cast(ability).Success));
        h.Run(); Assert.Equal(-expected, Assert.Single(h.Spends(ability)).Amount);
        Assert.Equal(100 - expected, h.Energy.Current);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void SharedRefreshIncludingCapSurvivesOldExpiration(bool cap)
    {
        var h = new Harness(); h.Tick(1); if (cap) for (var i = 2; i <= 5; i++) h.Tick(i);
        h.Tick(9);
        h.At(18.999m, () => { Assert.Equal(cap ? 5 : 2, h.Combat.ThousandCutsStacks);
            Assert.Equal(cap ? 45 : 54, h.Price("backstab")); });
        h.At(19.001m, () => { Assert.Equal(0, h.Combat.ThousandCutsStacks); Assert.Equal(60, h.Price("backstab")); });
        h.Run();
    }

    [Fact]
    public void BoundaryTickStartsOneStackAndOldExpiryCannotClearIt()
    {
        var h = new Harness(); for (var i = 1; i <= 5; i++) h.Tick(i);
        h.Tick(15); h.At(15.001m, () => { Assert.Equal(1, h.Combat.ThousandCutsStacks); Assert.Equal(57, h.Price("backstab")); });
        h.Run();
    }

    [Theory][InlineData("backstab", 57, 60)][InlineData("hemorrhage", 32, 35)]
    public void ConsumptionImmediatelyRestoresNextCastAndOldExpiryDoesNotClearRegrant(string ability, int discounted, int full)
    {
        var h = new Harness(); h.Tick(1); h.At(2, () => Assert.True(h.Cast(ability).Success));
        h.At(3, () => { Assert.Equal(full, h.Price(ability)); h.Energy.SetCurrent(100); Assert.True(h.Cast(ability).Success); });
        h.Tick(9); h.At(11.001m, () => { Assert.Equal(1, h.Combat.ThousandCutsStacks); Assert.Equal(discounted, h.Price(ability)); });
        h.Run(); Assert.Equal(new decimal?[] { -discounted, -full }, h.Spends(ability).Select(e => e.Amount));
    }

    [Theory][InlineData(0)][InlineData(1)]
    public void OwnershipAndSelectedPositivePeriodicPolicyRemain(int rank)
    {
        var h = new Harness(5, rank); h.Tick(1, source: "other"); h.Tick(1, amount: 0);
        h.Tick(1, periodic: false); h.Tick(1, ability: "garrote");
        h.At(2, () => Assert.Equal(0, h.Combat.ThousandCutsStacks));
        h.Tick(3); h.Tick(3, target: "target-2");
        h.At(4, () => Assert.Equal(rank == 0 ? 0 : 2, h.Combat.ThousandCutsStacks)); h.Run();
    }

    [Theory]
    [InlineData(55, 100, "7")][InlineData(51, 100, "14")][InlineData(50, 100, "15")]
    [InlineData(0, 100, "65")][InlineData(50, 59, null)]
    public void ForecastAcrossExpiryQuotesFromNowWithoutMutatingLiveState(int balance, int maximum, string? expected)
    {
        var h = new Harness(7); h.Tick(1);
        h.At(5, () => {
            h.Energy.Maximum = maximum; h.Energy.SetCurrent(balance); h.Energy.RegenerationPerSecond = 1;
            var aura = Assert.Single(h.Actor.ActiveAuras, a => a.Definition.Key == "rogue-thousand-cuts");
            var id = aura.InstanceId; var expiry = aura.ExpiresAtSeconds;
            var ready = h.Executor.GetNextAffordableTime(h.Context, h.Actor, h.Actor.Abilities["backstab"].Definition);
            Assert.Equal(expected is null ? null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), ready);
            Assert.Equal(balance, h.Energy.Current); Assert.Equal(5, h.Energy.LastUpdatedAtSeconds);
            Assert.Equal(57, h.Price("backstab")); Assert.Equal(1, h.Combat.ThousandCutsStacks);
            Assert.Equal(id, aura.InstanceId); Assert.Equal(expiry, aura.ExpiresAtSeconds);
        }); h.Run();
    }

    [Theory][InlineData(false, 100)][InlineData(true, 100)][InlineData(false, 57)][InlineData(true, 57)]
    public void QueuedCompletionAtExpiryUsesFullCostInEitherInsertionOrder(bool completionFirst, int energy)
    {
        var h = new Harness(13);
        h.Actor.Abilities["backstab"].Definition.CastTimeSeconds = completionFirst ? 11 : 9;
        h.At(completionFirst ? 0 : 2, () => Assert.True(h.Cast("backstab").Success));
        h.Tick(1); h.At(10, () => h.Energy.SetCurrent(energy)); h.Run();
        if (energy == 100) { Assert.Equal(-60m, Assert.Single(h.Spends("backstab")).Amount); Assert.Equal(40, h.Energy.Current); }
        else { Assert.Empty(h.Spends("backstab")); Assert.Equal(57, h.Energy.Current); }
        Assert.Equal(60, h.Price("backstab")); Assert.Equal(0, h.Combat.ThousandCutsStacks);
    }

    [Theory][InlineData("backstab", 57, 60)][InlineData("hemorrhage", 32, 35)]
    public void DirectStartAtDiscountIsAffordableOnlyWhileLive(string ability, int discounted, int full)
    {
        foreach (var time in new[] { 10.999m, 11m, 11.001m })
        {
            var h = new Harness(13); h.Tick(1);
            h.At(time, () => { h.Energy.SetCurrent(discounted); Assert.Equal(time < 11, h.Cast(ability).Success); });
            h.Run(); Assert.Equal(time < 11 ? 1 : 0, h.Spends(ability).Length);
            Assert.Equal(full, h.Price(ability));
        }
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void TickAtExpiryStartsFreshRegardlessOfExpirationInsertion(bool tickFirst)
    {
        var h = new Harness(17); for (var i = 1; i <= 5; i++) h.Tick(i);
        if (tickFirst) h.Tick(15); else h.At(14, () => h.Tick(15));
        h.At(16, () => { Assert.Equal(1, h.Combat.ThousandCutsStacks); Assert.Equal(57, h.Price("backstab")); });
        h.Run();
    }

    [Fact]
    public void CancelledExecutionKeepsBenefitUntilItsExpiry()
    {
        var h = new Harness(13); h.Tick(1);
        h.Actor.Abilities["backstab"].Definition.CastTimeSeconds = 3;
        h.At(2, () => Assert.True(h.Cast("backstab").Success));
        h.At(3, () => h.Context.CancelAbilityExecution(Assert.Single(h.Context.AbilityExecutions).Key, 3));
        h.At(6, () => { Assert.Equal(1, h.Combat.ThousandCutsStacks); Assert.Equal(57, h.Price("backstab")); });
        h.At(12, () => Assert.Equal(0, h.Combat.ThousandCutsStacks)); h.Run();
        Assert.Empty(h.Spends("backstab")); Assert.Equal(100, h.Energy.Current);
    }

    [Theory][InlineData("missing-execution")][InlineData("dead-target")][InlineData("failed-spend")]
    public void RetainedCompletionPolicyStillConsumesWithoutExecutedEffects(string failure)
    {
        var h = new Harness(7); h.Tick(1);
        h.At(2, () => {
            if (failure == "missing-execution")
                h.Context.ScheduleEvent(new() { TimeSeconds = 3, Type = CombatEventType.AbilityCastCompleted,
                    SourceActorKey = "rogue", TargetActorKey = "target", AbilityKey = "backstab", AbilityExecutionId = Guid.NewGuid() });
            else { h.Actor.Abilities["backstab"].Definition.CastTimeSeconds = 2; Assert.True(h.Cast("backstab").Success); }
        });
        h.At(3, () => { if (failure == "failed-spend") h.Energy.SetCurrent(0);
            if (failure == "dead-target") h.Context.GetActor("target")!.TakeDamage(1000000); });
        h.At(5, () => { Assert.Equal(0, h.Combat.ThousandCutsStacks); Assert.Equal(60, h.Price("backstab")); });
        h.Run(); Assert.Empty(h.Spends("backstab"));
    }

    [Theory][InlineData("hit")][InlineData("critical")][InlineData("miss")][InlineData("dodge")][InlineData("parry")][InlineData("zero")]
    public void CompletionConsumptionDoesNotAcquireLandedOrPositiveDamageGate(string outcome)
    {
        var h = new Harness(5); h.Tick(1); var r = h.RequestValue;
        r.IsBehindTarget = false;
        if (outcome == "miss") r.Tuning.MainHandHitPercent = 0;
        if (outcome == "dodge") r.TargetDodgePercent = 100;
        if (outcome == "parry") r.TargetParryPercent = 100;
        if (outcome == "critical") r.Tuning.PhysicalCritPercent = 100;
        if (outcome == "zero") { var effect = h.Actor.Abilities["hemorrhage"].Definition.Effects[0]; effect.MinimumValue = effect.MaximumValue = 0; }
        h.At(2, () => Assert.True(h.Cast("hemorrhage").Success)); h.Run();
        Assert.Equal(-32m, Assert.Single(h.Spends("hemorrhage")).Amount);
        Assert.Equal(0, h.Combat.ThousandCutsStacks); Assert.Equal(35, h.Price("hemorrhage"));
        var damage = Assert.Single(h.Context.Timeline, e => e.Type == CombatEventType.Damage && e.AbilityKey == "hemorrhage");
        if (outcome == "zero") Assert.Equal(0m, damage.Amount);
        else if (outcome == "critical") Assert.True(damage.IsCritical);
        else Assert.Equal(outcome, damage.ResultKey);
    }

    [Fact]
    public void OnlyBoundFlatEnergyComponentChangesAndAggregateSpendRemainsAtomic()
    {
        var h = new Harness(5, configure: actor => {
            actor.AddResource(new ResourceState("mana", 50, 50));
            var ability = actor.Abilities["backstab"].Definition;
            ability.ResourceCosts.Insert(0, new() { ResourceKey = "mana", Amount = 10 });
            ability.ResourceCosts.Add(new() { ResourceKey = "energy", Amount = 5 });
            ability.ResourceCosts.Add(new() { ResourceKey = "energy", Amount = 10, IsPercentOfMaximum = true });
            ability.AdditionalResourceConsumptions.Add(new() { ResourceKey = "mana", MaximumAmount = 3 });
        });
        h.Tick(1); h.At(2, () => {
            var costs = h.Actor.Abilities["backstab"].Definition.ResourceCosts;
            Assert.Equal(new decimal[] { 10, 57, 5, 10 }, costs.Select(c => c.Amount));
            h.Energy.SetCurrent(71); Assert.False(h.Cast("backstab").Success); Assert.Equal(50, h.Actor.Resources["mana"].Current);
            h.Energy.SetCurrent(72); Assert.True(h.Cast("backstab").Success);
        }); h.Run(); Assert.Equal(-72m, Assert.Single(h.Spends("backstab")).Amount);
        Assert.Equal(0, h.Energy.Current); Assert.Equal(37, h.Actor.Resources["mana"].Current);
        Assert.Equal(new decimal[] { 10, 60, 5, 10 }, h.Actor.Abilities["backstab"].Definition.ResourceCosts.Select(c => c.Amount));
    }

    [Fact]
    public void ReplacedComponentIsNotMistakenForBoundSpellbookCost()
    {
        var h = new Harness(5); var ability = h.Actor.Abilities["backstab"].Definition;
        ability.ResourceCosts[0] = new() { ResourceKey = "energy", Amount = 23 };
        h.Tick(1); h.At(2, () => { Assert.Equal(23, h.Price("backstab")); Assert.True(h.Cast("backstab").Success); });
        h.Run(); Assert.Equal(-23m, Assert.Single(h.Spends("backstab")).Amount);
    }

    [Fact]
    public void RealRuptureTicksGrantThenExpireWithoutAnyLaterTickOrRotation()
    {
        var h = new Harness(30);
        for (var i = 0; i < 4; i++) h.At(i, () => { h.Energy.SetCurrent(100); Assert.True(h.Cast("sinister-strike").Success); });
        h.At(4, () => { h.Energy.SetCurrent(100); Assert.True(h.Cast("rupture").Success); });
        h.At(7, () => { Assert.Equal(1, h.Combat.ThousandCutsStacks); Assert.Equal(57, h.Price("backstab")); });
        h.At(25, () => { Assert.Equal(0, h.Combat.ThousandCutsStacks); Assert.Equal(60, h.Price("backstab")); });
        h.Run(); Assert.Contains(h.Context.Timeline, e => e.Type == CombatEventType.Damage && e.AbilityKey == "rupture" && e.IsPeriodic);
    }

    [Fact]
    public void SeparatePreparedGraphsAndAbsentSpellRemainIndependent()
    {
        var first = new Harness(5); var second = new Harness(5, 0); var third = new Harness(5);
        third.Actor.Abilities.Remove("hemorrhage");
        first.Tick(1); third.Tick(1);
        first.At(2, () => { Assert.Equal(57, first.Price("backstab")); Assert.Equal(60, second.Price("backstab"));
            Assert.Equal(60, third.Price("backstab")); });
        first.Run(); second.Run(); third.Run(); Assert.Equal(57, third.Price("backstab"));
    }

    [Fact]
    public void NullPolicyRetainsOrdinaryCalculatorResults()
    {
        var h = new Harness(5, 0, policy: false); h.At(2, () => {
            h.Energy.SetCurrent(50); h.Energy.RegenerationPerSecond = 2;
            var ability = h.Actor.Abilities["backstab"].Definition;
            Assert.Equal(ResourceAvailabilityCalculator.GetNextAffordableTime(h.Actor, ability, 2),
                h.Executor.GetNextAffordableTime(h.Context, h.Actor, ability));
        }); h.Run();
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void AmbiguousOrInitiallyCustomComponentsAreNotSelected(bool ambiguous)
    {
        var h = new Harness(5, configure: actor => {
            var costs = actor.Abilities["backstab"].Definition.ResourceCosts;
            if (ambiguous) costs.Add(new() { ResourceKey = "energy", Amount = 60 }); else costs[0].Amount = 23;
        }); h.Tick(1);
        h.At(2, () => {
            Assert.All(h.Actor.Abilities["backstab"].Definition.ResourceCosts, c => Assert.Equal(ambiguous ? 60 : 23, c.Amount));
            Assert.Equal(!ambiguous, h.Cast("backstab").Success);
        }); h.Run();
        if (ambiguous) Assert.Empty(h.Spends("backstab")); else Assert.Equal(-23m, Assert.Single(h.Spends("backstab")).Amount);
    }

    [Fact]
    public void ForeignActorUsingSharedExecutorDoesNotReceiveOrConsumeOwnersDiscount()
    {
        var h = new Harness(5); var foreign = h.Context.GetActor("other")!;
        foreign.AddResource(new ResourceState("energy", 100, 100));
        foreign.AddAbility(new() { Key = "backstab", Name = "Foreign Backstab", GlobalCooldownSeconds = 1,
            ResourceCosts = [new() { ResourceKey = "energy", Amount = 60 }],
            Effects = [new() { Key = "foreign-hit", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, ResolutionType = CombatResolutionTypes.AlwaysHits,
                SchoolKey = "physical", MinimumValue = 1, MaximumValue = 1 }] });
        h.Tick(1); h.At(2, () => Assert.True(h.Executor.TryStartAbility(h.Context, "other", "target", "backstab").Success));
        h.At(3, () => { Assert.Equal(1, h.Combat.ThousandCutsStacks); Assert.Equal(57, h.Price("backstab")); });
        h.Run(); Assert.Equal(40, foreign.Resources["energy"].Current);
    }

    [Fact]
    public void OrdinaryPrioritySchedulingUsesFullCostForecastAcrossExpiry()
    {
        var h = new Harness(17); h.Tick(1);
        var priority = new PriorityRotationExecutor(new() { Entries = [new() { AbilityKey = "backstab" }] }, "rogue", "target", h.Executor);
        h.At(5, () => {
            h.Energy.SetCurrent(50); h.Energy.RegenerationPerSecond = 1;
            priority.Process(h.Context, new() { TimeSeconds = 5, Type = CombatEventType.RotationDecision, SourceActorKey = "rogue" });
            Assert.Equal(50, h.Energy.Current); Assert.Equal(5, h.Energy.LastUpdatedAtSeconds); Assert.Equal(57, h.Price("backstab"));
        }); h.Run();
        Assert.Contains(h.Dispatched, e => e.Type == CombatEventType.RotationDecision && e.TimeSeconds == 15);
        Assert.DoesNotContain(h.Dispatched, e => e.Type == CombatEventType.RotationDecision && e.TimeSeconds == 12);
    }

    [Fact]
    public void InterruptPriorityReadinessUsesCurrentCostPolicyBeforeCancelling()
    {
        var h = new Harness(7); h.Tick(1);
        h.Actor.Abilities["sinister-strike"].Definition.CastTimeSeconds = 20;
        var priority = new PriorityRotationExecutor(new() { Entries = [new() { AbilityKey = "backstab", InterruptCurrentCast = true }] }, "rogue", "target", h.Executor);
        h.At(2, () => Assert.True(h.Cast("sinister-strike").Success));
        h.At(3, () => {
            h.Energy.SetCurrent(57); h.Actor.Abilities["backstab"].Definition.ResourceCosts[0].Amount = 60;
            priority.Process(h.Context, new() { TimeSeconds = 3, Type = CombatEventType.RotationDecision, SourceActorKey = "rogue" });
            Assert.True(Assert.Single(h.Context.AbilityExecutions.Values, e => e.AbilityKey == "sinister-strike").IsCancelled);
        }); h.Run(); Assert.Equal(-57m, Assert.Single(h.Spends("backstab")).Amount);
    }

    [Fact]
    public void RepeatedConsumeRegrantAndStaleExpirationsNeverDriftCosts()
    {
        var h = new Harness(40);
        for (var cycle = 0; cycle < 10; cycle++)
        {
            var time = cycle * 3 + 1; h.Tick(time);
            h.At(time + 1, () => { h.Energy.SetCurrent(100); Assert.Equal(57, h.Price("backstab")); Assert.True(h.Cast("backstab").Success); });
            h.At(time + 1.5m, () => { Assert.Equal(60, h.Price("backstab")); Assert.Equal(35, h.Price("hemorrhage")); });
        }
        h.Run(); Assert.Equal(10, h.Spends("backstab").Length);
        Assert.All(h.Spends("backstab"), e => Assert.Equal(-57m, e.Amount)); Assert.Equal(60, h.Price("backstab"));
    }

    [Theory][InlineData("backstab", 57, 60)][InlineData("hemorrhage", 32, 35)]
    public void DirectExecutorBoundaryNeedsNoRogueObserverOrExpirationDispatch(string ability, int discounted, int full)
    {
        var h = new Harness(); h.Tick(1);
        // Queue the boundary before Gain schedules its expiry. Advance the context,
        // but intentionally do not dispatch any observer at that boundary.
        h.Context.ScheduleEvent(new() { TimeSeconds = 11, Type = CombatEventType.HealthChanged, SourceActorKey = "test" });
        Assert.True(h.Context.TryGetNextEvent(out var tick)); h.Combat.Process(h.Context, tick!);
        Assert.Equal(discounted, h.Price(ability));
        while (h.Context.CurrentTimeSeconds < 11) Assert.True(h.Context.TryGetNextEvent(out _));
        h.Energy.SetCurrent(discounted); Assert.False(h.Cast(ability).Success);
        Assert.Equal(full, h.Price(ability)); Assert.Equal(0, h.Combat.ThousandCutsStacks);
    }
}
