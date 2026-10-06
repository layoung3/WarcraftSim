using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class AbilityLifecycleOwnershipTests
{
    [Fact]
    public void TryStartAbility_OwnsCastTrackingAndActionTiming()
    {
        var source =
            CreateActor(
                "source",
                "Source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "Target",
                "enemy"
            );

        source.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0.2m
        );

        source.AddAbility(
            CreateAbility(
                key:
                    "long-cast",

                castTimeSeconds:
                    2m,

                globalCooldownSeconds:
                    1.5m
            )
        );

        var context =
            CreateContext(
                source.Key,
                durationSeconds:
                    3m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateAbilityExecutor();

        var result =
            executor.TryStartAbility(
                context,
                source.Key,
                target.Key,
                "long-cast"
            );

        Assert.True(
            result.Success
        );

        Assert.True(
            source.CurrentCastExecutionId.HasValue
        );

        Assert.Equal(
            "long-cast",
            source.CurrentCastAbilityKey
        );

        Assert.Equal(
            2m,
            source.CastReadyAtSeconds
        );

        Assert.Equal(
            1.5m,
            source.GlobalCooldownReadyAtSeconds
        );

        Assert.Equal(
            2.2m,
            source.InputReadyAtSeconds
        );
    }

    [Fact]
    public void TryStartAbility_EnforcesActionInputLockWithoutRotationExecutor()
    {
        var source =
            CreateActor(
                "source",
                "Source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "Target",
                "enemy"
            );

        source.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0.25m
        );

        source.AddAbility(
            CreateAbility(
                key:
                    "instant",

                castTimeSeconds:
                    0m,

                globalCooldownSeconds:
                    0m,

                isOffGlobalCooldown:
                    true
            )
        );

        var context =
            CreateContext(
                source.Key,
                durationSeconds:
                    1m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateAbilityExecutor();

        var first =
            executor.TryStartAbility(
                context,
                source.Key,
                target.Key,
                "instant"
            );

        var second =
            executor.TryStartAbility(
                context,
                source.Key,
                target.Key,
                "instant"
            );

        Assert.True(
            first.Success
        );

        Assert.False(
            second.Success
        );

        Assert.Equal(
            0.25m,
            source.InputReadyAtSeconds
        );
    }

    [Fact]
    public void AbilityCompletion_ClearsCurrentCastWithoutRotationExecutor()
    {
        var source =
            CreateActor(
                "source",
                "Source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "Target",
                "enemy"
            );

        source.AddAbility(
            CreateAbility(
                key:
                    "one-second-cast",

                castTimeSeconds:
                    1m,

                globalCooldownSeconds:
                    1m
            )
        );

        var context =
            CreateContext(
                source.Key,
                durationSeconds:
                    2m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateAbilityExecutor();

        var engine =
            new SimulationEngine(
                [
                    executor
                ]
            );

        engine.Run(
            context,
            startedContext =>
            {
                var startResult =
                    executor.TryStartAbility(
                        startedContext,
                        source.Key,
                        target.Key,
                        "one-second-cast"
                    );

                Assert.True(
                    startResult.Success
                );

                Assert.True(
                    source.CurrentCastExecutionId.HasValue
                );
            }
        );

        Assert.Null(
            source.CurrentCastExecutionId
        );

        Assert.Null(
            source.CurrentCastAbilityKey
        );
    }

    [Fact]
    public void TryCancelCurrentCast_OwnsCancellationStateAndEvent()
    {
        var source =
            CreateActor(
                "source",
                "Source",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "Target",
                "enemy"
            );

        source.AddAbility(
            CreateAbility(
                key:
                    "long-cast",

                castTimeSeconds:
                    5m,

                globalCooldownSeconds:
                    1.5m
            )
        );

        var context =
            CreateContext(
                source.Key,
                durationSeconds:
                    2m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            CreateAbilityExecutor();

        var driver =
            new CancellationDriver(
                executor,
                source.Key,
                target.Key,
                "long-cast"
            );

        var engine =
            new SimulationEngine(
                [
                    executor,
                    driver
                ]
            );

        var result =
            engine.Run(
                context
            );

        Assert.True(
            driver.CancelSucceeded
        );

        Assert.True(
            driver.StartedExecutionId.HasValue
        );

        Assert.Null(
            source.CurrentCastExecutionId
        );

        Assert.Null(
            source.CurrentCastAbilityKey
        );

        var execution =
            context.GetAbilityExecution(
                driver.StartedExecutionId!.Value
            );

        Assert.NotNull(
            execution
        );

        Assert.True(
            execution!.IsCancelled
        );

        Assert.Equal(
            1m,
            execution.CancelledAtSeconds
        );

        var cancelledEvent =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                    CombatEventType.AbilityCastCancelled
            );

        Assert.Equal(
            source.Key,
            cancelledEvent.SourceActorKey
        );

        Assert.Equal(
            target.Key,
            cancelledEvent.TargetActorKey
        );

        Assert.Equal(
            "long-cast",
            cancelledEvent.AbilityKey
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string name,
        string teamKey)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    name,

                TeamKey =
                    teamKey
            };

        actor.InitializeHealth(
            maximumHealth:
                1000m
        );

        actor.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0m
        );

        return actor;
    }

    private static AbilityDefinition CreateAbility(
        string key,
        decimal castTimeSeconds,
        decimal globalCooldownSeconds,
        bool isOffGlobalCooldown = false)
    {
        return new AbilityDefinition
        {
            Key =
                key,

            Name =
                key,

            CastTimeSeconds =
                castTimeSeconds,

            GlobalCooldownSeconds =
                globalCooldownSeconds,

            IsOffGlobalCooldown =
                isOffGlobalCooldown
        };
    }

    private static SimulationContext CreateContext(
        string primaryActorKey,
        decimal durationSeconds)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    durationSeconds,

                PrimaryActorKey =
                    primaryActorKey,

                CaptureTimeline =
                    true
            }
        );
    }

    private static AbilityExecutor CreateAbilityExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "ability-lifecycle-tests",

                    Version =
                        "1"
                }
            )
        );
    }

    private sealed class CancellationDriver :
        ICombatEventProcessor
    {
        private readonly AbilityExecutor
            _executor;

        private readonly string
            _sourceActorKey;

        private readonly string
            _targetActorKey;

        private readonly string
            _abilityKey;

        public CancellationDriver(
            AbilityExecutor executor,
            string sourceActorKey,
            string targetActorKey,
            string abilityKey)
        {
            _executor =
                executor;

            _sourceActorKey =
                sourceActorKey;

            _targetActorKey =
                targetActorKey;

            _abilityKey =
                abilityKey;
        }

        public Guid? StartedExecutionId { get; private set; }

        public bool CancelSucceeded { get; private set; }

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            if (
                combatEvent.Type ==
                CombatEventType.SimulationStarted
            )
            {
                var result =
                    _executor.TryStartAbility(
                        context,
                        _sourceActorKey,
                        _targetActorKey,
                        _abilityKey
                    );

                Assert.True(
                    result.Success
                );

                StartedExecutionId =
                    context.GetActor(
                            _sourceActorKey
                        )?
                        .CurrentCastExecutionId;

                context.ScheduleEvent(
                    new CombatEvent
                    {
                        TimeSeconds =
                            1m,

                        Type =
                            CombatEventType.RotationDecision,

                        SourceActorKey =
                            _sourceActorKey,

                        IsInternal =
                            true,

                        Description =
                            "Test cancellation trigger."
                    }
                );

                return;
            }

            if (
                combatEvent.Type ==
                    CombatEventType.RotationDecision &&
                string.Equals(
                    combatEvent.SourceActorKey,
                    _sourceActorKey,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                CancelSucceeded =
                    _executor.TryCancelCurrentCast(
                        context,
                        _sourceActorKey
                    );
            }
        }
    }
}
