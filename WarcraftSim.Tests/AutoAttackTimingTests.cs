using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Tests;

public sealed class AutoAttackTimingTests
{
    [Fact]
    public void BaseProvider_PreservesDefinitionSwingInterval()
    {
        var context = CreateContext(5m);
        var source = CreateActor("source");
        var definition = CreateAutoAttack(2.6m);

        context.AddActor(source);

        var timing =
            BaseAutoAttackTimingProvider.Instance.Resolve(
                context,
                source,
                definition
            );

        Assert.Equal(2.6m, timing.SwingIntervalSeconds);
    }

    [Theory]
    [InlineData(0, 2.5)]
    [InlineData(25, 2.0)]
    [InlineData(100, 1.25)]
    public void ForeverProvider_AppliesHasteToSwingInterval(
        int hastePercent,
        double expectedSwingIntervalSeconds)
    {
        var context = CreateContext(5m);
        var source = CreateActor("source");
        var definition = CreateAutoAttack(2.5m);

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            hastePercent
        );

        context.AddActor(source);

        var timing =
            new ForeverAutoAttackTimingProvider()
                .Resolve(
                    context,
                    source,
                    definition
                );

        Assert.Equal(
            (decimal)expectedSwingIntervalSeconds,
            timing.SwingIntervalSeconds
        );
    }

    [Fact]
    public void ForeverProvider_NegativeHasteDoesNotSlowSwingBelowBaseSpeed()
    {
        var context = CreateContext(5m);
        var source = CreateActor("source");
        var definition = CreateAutoAttack(2.5m);

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            -50m
        );

        context.AddActor(source);

        var timing =
            new ForeverAutoAttackTimingProvider()
                .Resolve(
                    context,
                    source,
                    definition
                );

        Assert.Equal(2.5m, timing.SwingIntervalSeconds);
    }

    [Fact]
    public void Processor_UsesForeverHasteForFirstAndFollowingSwings()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var definition = CreateAutoAttack(2.5m);

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            25m
        );

        var result =
            Run(
                source,
                target,
                definition,
                durationSeconds: 4.1m,
                new ForeverAutoAttackTimingProvider()
            );

        Assert.Equal(
            [2m, 4m],
            DamageTimes(
                result,
                definition.Key
            )
        );
    }

    [Fact]
    public void ExplicitFirstSwingDelay_OverridesOnlyInitialSwing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var definition = CreateAutoAttack(2.5m);

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            25m
        );

        var result =
            Run(
                source,
                target,
                definition,
                durationSeconds: 4.1m,
                new ForeverAutoAttackTimingProvider(),
                firstSwingDelaySeconds: 0m
            );

        Assert.Equal(
            [0m, 2m, 4m],
            DamageTimes(
                result,
                definition.Key
            )
        );
    }

    [Fact]
    public void Processor_ReResolvesTimingAfterEveryCompletedSwing()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var definition = CreateAutoAttack(2m);

        var timingProvider =
            new SequenceAutoAttackTimingProvider(
                2m,
                1m,
                1m
            );

        var result =
            Run(
                source,
                target,
                definition,
                durationSeconds: 3.1m,
                timingProvider
            );

        Assert.Equal(
            [2m, 3m],
            DamageTimes(
                result,
                definition.Key
            )
        );

        Assert.True(timingProvider.ResolveCount >= 3);
    }

    [Fact]
    public void Processor_RejectsNonPositiveResolvedSwingInterval()
    {
        var source = CreateActor("source");
        var target = CreateActor("target");
        var definition = CreateAutoAttack(2m);
        var context = CreateContext(5m);

        context.AddActor(source);
        context.AddActor(target);

        var processor =
            CreateProcessor(
                new ConstantAutoAttackTimingProvider(0m)
            );

        Assert.Throws<InvalidOperationException>(() =>
            processor.Start(
                context,
                source.Key,
                target.Key,
                definition
            )
        );
    }

    [Fact]
    public void ForeverHaste_ChangesFrequencyWithoutChangingBaseWeaponApCoefficient()
    {
        var definition =
            ForeverAutoAttackFactory.CreatePlayerMelee(
                key: "main-hand",
                name: "Main Hand",
                minimumWeaponDamage: 20m,
                maximumWeaponDamage: 30m,
                weaponSpeedSeconds: 2.8m,
                attackSkillStatKey: "sword-skill"
            );

        var source = CreateActor("source");
        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            40m
        );

        var context = CreateContext(5m);
        context.AddActor(source);

        var timing =
            new ForeverAutoAttackTimingProvider()
                .Resolve(
                    context,
                    source,
                    definition
                );

        Assert.Equal(2m, timing.SwingIntervalSeconds);
        Assert.Equal(
            2.8m / 14m,
            definition.DamageEffect.ScalingCoefficient
        );
    }

    private static SimulationRunResult Run(
        SimulationActorState source,
        SimulationActorState target,
        AutoAttackDefinition definition,
        decimal durationSeconds,
        IAutoAttackTimingProvider timingProvider,
        decimal? firstSwingDelaySeconds = null)
    {
        var context =
            CreateContext(
                durationSeconds
            );

        context.AddActor(source);
        context.AddActor(target);

        var processor =
            CreateProcessor(
                timingProvider
            );

        return new SimulationEngine(
            [
                processor
            ])
            .Run(
                context,
                startedContext =>
                {
                    Assert.True(
                        processor.Start(
                            startedContext,
                            source.Key,
                            target.Key,
                            definition,
                            firstSwingDelaySeconds
                        )
                    );
                }
            );
    }

    private static decimal[] DamageTimes(
        SimulationRunResult result,
        string abilityKey)
    {
        return result.Timeline
            .Where(combatEvent =>
                combatEvent.Type == CombatEventType.Damage &&
                string.Equals(
                    combatEvent.AbilityKey,
                    abilityKey,
                    StringComparison.OrdinalIgnoreCase
                ))
            .Select(combatEvent =>
                combatEvent.TimeSeconds
            )
            .ToArray();
    }

    private static AutoAttackProcessor CreateProcessor(
        IAutoAttackTimingProvider timingProvider)
    {
        var executor =
            new AbilityExecutor(
                new AuraManager(),
                new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey = "auto-attack-timing-tests",
                        Version = "1"
                    }
                )
            );

        return new AutoAttackProcessor(
            executor,
            timingProvider
        );
    }

    private static AutoAttackDefinition CreateAutoAttack(
        decimal swingIntervalSeconds)
    {
        return new AutoAttackDefinition
        {
            Key = "main-hand-auto",
            Name = "Main Hand Auto Attack",
            SwingIntervalSeconds = swingIntervalSeconds,
            DamageEffect =
                new AbilityEffectDefinition
                {
                    Key = "damage",
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy,
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = 1m,
                    MaximumValue = 1m,
                    MitigationType = DamageMitigationTypes.None
                }
        };
    }

    private static SimulationActorState CreateActor(
        string key)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey =
                    key == "source"
                        ? "players"
                        : "enemies",
                Level = 30
            };

        actor.InitializeHealth(100m);

        return actor;
    }

    private static SimulationContext CreateContext(
        decimal durationSeconds)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = durationSeconds,
                PrimaryActorKey = "source",
                CaptureTimeline = true
            }
        );
    }

    private sealed class ConstantAutoAttackTimingProvider :
        IAutoAttackTimingProvider
    {
        private readonly decimal _swingIntervalSeconds;

        public ConstantAutoAttackTimingProvider(
            decimal swingIntervalSeconds)
        {
            _swingIntervalSeconds =
                swingIntervalSeconds;
        }

        public AutoAttackTimingSnapshot Resolve(
            SimulationContext context,
            SimulationActorState source,
            AutoAttackDefinition definition)
        {
            return new AutoAttackTimingSnapshot
            {
                SwingIntervalSeconds =
                    _swingIntervalSeconds
            };
        }
    }

    private sealed class SequenceAutoAttackTimingProvider :
        IAutoAttackTimingProvider
    {
        private readonly Queue<decimal>
            _intervals;

        private decimal _lastInterval;

        public int ResolveCount { get; private set; }

        public SequenceAutoAttackTimingProvider(
            params decimal[] intervals)
        {
            if (intervals.Length == 0)
            {
                throw new ArgumentException(
                    "At least one interval is required.",
                    nameof(intervals)
                );
            }

            _intervals =
                new Queue<decimal>(
                    intervals
                );

            _lastInterval =
                intervals[^1];
        }

        public AutoAttackTimingSnapshot Resolve(
            SimulationContext context,
            SimulationActorState source,
            AutoAttackDefinition definition)
        {
            ResolveCount++;

            if (_intervals.Count > 0)
            {
                _lastInterval =
                    _intervals.Dequeue();
            }

            return new AutoAttackTimingSnapshot
            {
                SwingIntervalSeconds =
                    _lastInterval
            };
        }
    }
}
