using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using Xunit;

namespace WarcraftSim.Tests;

/// <summary>
/// CP124: shared boss white swings must behave independently of Warrior,
/// and must not change legacy solo HP preservation.
/// </summary>
public sealed class EncounterRaidMeleeCp124Tests
{
    [Fact]
    public void FixedTargetSolo_ScoresAllDamageWithoutConsumingHealth()
    {
        var run = Setup(duration: 6m, targetMode: EncounterBossMeleeTargetModes.Fixed,
            preserveHealth: true, tankHealth: 125m);
        var result = Execute(run);
        Assert.Equal(3, BossHits(result).Count());
        Assert.Equal(300m, result.Summary.DamageTaken);
        Assert.Equal(125m, run.Tank.CurrentHealth);
        Assert.DoesNotContain(result.Timeline, ev => ev.Type == CombatEventType.ActorDied);
    }

    [Fact]
    public void ThreatTarget_ChangesOnlyWhenContenderPassesConfiguredThreshold()
    {
        var run = Setup(duration: 8m, targetMode: EncounterBossMeleeTargetModes.HighestThreat);
        run.Boss.ThreatTable.SetThreat(run.Tank.Key, 100m);
        run.Boss.ThreatTable.SetThreat(run.Offtank.Key, 99m);
        AddTimedAction(run, 2m, ctx => ctx.GetActor("boss")!.ThreatTable.SetThreat("offtank", 109m));
        AddTimedAction(run, 4m, ctx => ctx.GetActor("boss")!.ThreatTable.SetThreat("offtank", 111m));
        var result = Execute(run);
        Assert.Equal(new[] { "tank", "tank", "offtank", "offtank" },
            BossHits(result).Select(ev => ev.TargetActorKey).ToArray());
    }

    [Fact]
    public void ForcedTarget_OverridesThreatThenReturnsToThreatSelection()
    {
        var run = Setup(duration: 6m, targetMode: EncounterBossMeleeTargetModes.HighestThreat);
        run.Boss.ThreatTable.SetThreat("tank", 200m);
        run.Boss.ThreatTable.SetThreat("offtank", 20m);
        var forced = new ForcedTargetManager();
        run.AdditionalProcessors.Add(forced);
        AddTimedAction(run, 2m, ctx => forced.ApplyForcedTarget(ctx,
            run.Boss, run.Offtank, durationSeconds: 2m));
        var result = Execute(run);
        Assert.Equal(new[] { "tank", "offtank", "tank" },
            BossHits(result).Select(ev => ev.TargetActorKey).ToArray());
    }

    [Fact]
    public void TankDeath_SwitchesToAnotherLivingActorOnNextSwing()
    {
        var run = Setup(duration: 6m, targetMode: EncounterBossMeleeTargetModes.HighestThreat,
            tankHealth: 150m);
        run.Boss.ThreatTable.SetThreat("tank", 200m);
        run.Boss.ThreatTable.SetThreat("offtank", 100m);
        var result = Execute(run);
        Assert.Equal(new[] { "tank", "tank", "offtank" },
            BossHits(result).Select(ev => ev.TargetActorKey).ToArray());
        Assert.False(run.Tank.IsAlive);
        Assert.Single(result.Timeline, ev =>
            ev.Type == CombatEventType.ActorDied && ev.TargetActorKey == "tank");
    }

    [Fact]
    public void ScheduledHealing_PreservesTankAndIsAccountedForSeparately()
    {
        var run = Setup(duration: 4m, targetMode: EncounterBossMeleeTargetModes.Fixed,
            tankHealth: 150m);
        AddTimedAction(run, 2m, ctx =>
        {
            var healed = run.Tank.Heal(100m);
            ctx.EmitEvent(new CombatEvent
            {
                TimeSeconds = ctx.CurrentTimeSeconds,
                Type = CombatEventType.Healing,
                SourceActorKey = "healer", TargetActorKey = "tank",
                AbilityKey = "test-heal", Amount = healed
            });
        });
        var result = Execute(run);
        Assert.Equal(50m, run.Tank.CurrentHealth);
        Assert.True(run.Tank.IsAlive);
        Assert.Equal(200m, result.Summary.DamageTaken);
        Assert.Equal(100m, result.Summary.ActorSummaries["healer"].HealingDone);
        Assert.Equal(100m, result.Summary.ActorSummaries["tank"].HealingReceived);
    }

    [Fact]
    public void Absorb_ShieldsAreConsumedBeforeActualHealth()
    {
        var run = Setup(duration: 5m, targetMode: EncounterBossMeleeTargetModes.Fixed,
            tankHealth: 400m);
        var absorbs = new AbsorbManager();
        // Share the instance with the melee processor to exercise normal
        // absorb consumption and reporting, not a custom tank shortcut.
        run.Absorbs = absorbs;
        var result = Execute(run, ctx => absorbs.ApplyAbsorb(ctx, run.Tank,
            "test-barrier", "Test Barrier", 150m, 10m,
            AbsorbStackingMode.Replace, 1, "healer", "test-barrier", "shield"));
        Assert.Equal(350m, run.Tank.CurrentHealth); // 200 incoming, 150 absorbed
        Assert.Equal(50m, result.Summary.DamageTaken);
        Assert.Equal(150m, result.Timeline.Where(ev =>
            ev.Type == CombatEventType.AbsorbConsumed).Sum(ev => ev.Amount ?? 0m));
    }

    [Fact]
    public void ParryHaste_AppliesToAnyPlayerClassAndStaleSwingIsDiscarded()
    {
        var run = Setup(duration: 6m, targetMode: EncounterBossMeleeTargetModes.Fixed);
        run.Settings.FirstSwingSeconds = 3m;
        // A parry from a non-Warrior player must accelerate the boss's attack.
        AddTimedAction(run, 0.5m, ctx => ctx.EmitEvent(new CombatEvent
        {
            TimeSeconds = ctx.CurrentTimeSeconds,
            Type = CombatEventType.Damage, SourceActorKey = "offtank",
            TargetActorKey = "boss", AbilityKey = "offtank-auto",
            ResultKey = CombatResultTypes.Parry, Amount = 0m
        }));
        var result = Execute(run);
        Assert.Equal(new[] { 2.2m, 4.2m },
            BossHits(result).Select(ev => ev.TimeSeconds).ToArray());
    }

    [Fact]
    public void NoLivingTargets_SkipsSwingWithoutCrashing()
    {
        var run = Setup(duration: 4m, targetMode: EncounterBossMeleeTargetModes.HighestThreat);
        run.Tank.InitializeHealth(0m);
        run.Offtank.InitializeHealth(0m);
        run.Healer.InitializeHealth(0m);
        var result = Execute(run);
        Assert.DoesNotContain(result.Timeline, ev =>
            ev.Type == CombatEventType.Damage && ev.AbilityKey == "boss-main-hand-auto");
    }

    [Fact]
    public void RecoveredLivingTarget_IsSelectedAtNextSwing()
    {
        var run = Setup(duration: 6m, targetMode: EncounterBossMeleeTargetModes.HighestThreat);
        run.Tank.InitializeHealth(0m);
        run.Offtank.InitializeHealth(0m);
        run.Healer.InitializeHealth(0m);
        AddTimedAction(run, 2m, _ => run.Tank.InitializeHealth(300m));
        var result = Execute(run);
        Assert.Equal(new[] { 3m, 5m }, BossHits(result).Select(ev => ev.TimeSeconds));
        Assert.All(BossHits(result), ev => Assert.Equal("tank", ev.TargetActorKey));
    }

    private static IEnumerable<CombatEvent> BossHits(SimulationRunResult result) =>
        result.Timeline.Where(ev =>
            ev.Type == CombatEventType.Damage && ev.AbilityKey == "boss-main-hand-auto");

    private static TestRun Setup(decimal duration, string targetMode,
        bool preserveHealth = false, decimal tankHealth = 2000m)
    {
        var settings = new EncounterBossMeleeDefinition
        {
            BossActorKey = "boss", AbilityKey = "boss-main-hand-auto",
            TargetMode = targetMode, PreferredTargetActorKey = "tank",
            EligibleTargetTeamKey = "raid", PreserveTargetHealth = preserveHealth,
            FirstSwingSeconds = 1m, SwingSpeedSeconds = 2m,
            MinimumDamage = 100m, MaximumDamage = 100m,
            EnableBossParryHaste = true
        };
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = 123, DurationSeconds = duration,
            PrimaryActorKey = "tank", CaptureTimeline = true,
            MaximumProcessedEvents = 2000
        });
        SimulationActorState Make(string key, string team, decimal hp)
        {
            var actor = new SimulationActorState
            {
                Key = key, Name = key, TeamKey = team, Level = 60
            };
            actor.InitializeHealth(hp);
            context.AddActor(actor);
            return actor;
        }
        var boss = Make("boss", "enemy", 100000m);
        var tank = Make("tank", "raid", tankHealth);
        var offtank = Make("offtank", "raid", 2000m);
        var healer = Make("healer", "raid", 2000m);
        return new TestRun(context, settings, boss, tank, offtank, healer);
    }

    private static void AddTimedAction(TestRun run, decimal at, Action<SimulationContext> action)
    {
        var effect = "test-action-" + run.Actions.Count;
        run.Actions.Add((effect, at, action));
    }

    private static SimulationRunResult Execute(TestRun run,
        Action<SimulationContext>? onStart = null)
    {
        var swing = new EncounterBossMeleeProcessor(run.Settings,
            new AlwaysHitRoller(), new NoMitigation(), run.Absorbs);
        var timed = new TimedActions(run.Actions);
        var processors = new List<ICombatEventProcessor> { swing, timed };
        processors.AddRange(run.AdditionalProcessors);
        return new SimulationEngine(processors).Run(run.Context, ctx =>
        {
            swing.ScheduleFirst(ctx);
            timed.Schedule(ctx);
            onStart?.Invoke(ctx);
        });
    }

    private sealed record TestRun(SimulationContext Context,
        EncounterBossMeleeDefinition Settings, SimulationActorState Boss,
        SimulationActorState Tank, SimulationActorState Offtank,
        SimulationActorState Healer)
    {
        public AbsorbManager? Absorbs { get; set; }
        public List<ICombatEventProcessor> AdditionalProcessors { get; } = [];
        public List<(string Key, decimal At, Action<SimulationContext> Callback)> Actions { get; } = [];
    }

    private sealed class TimedActions(
        List<(string Key, decimal At, Action<SimulationContext> Callback)> actions)
        : ICombatEventProcessor
    {
        public void Schedule(SimulationContext ctx)
        {
            foreach (var (key, at, _) in actions)
                ctx.ScheduleEvent(new CombatEvent
                {
                    TimeSeconds = at, Type = CombatEventType.RotationDecision,
                    EffectKey = key, IsInternal = true, SourceActorKey = "healer"
                });
        }
        public void Process(SimulationContext context, CombatEvent combatEvent)
        {
            if (combatEvent.Type != CombatEventType.RotationDecision ||
                !combatEvent.IsInternal) return;
            foreach (var (key, _, action) in actions)
                if (combatEvent.EffectKey == key) action(context);
        }
    }

    private sealed class AlwaysHitRoller : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect) => CombatRollResult.Hit();
    }

    private sealed class NoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect,
            decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }
}
