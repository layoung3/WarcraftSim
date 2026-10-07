using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Characters;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;

namespace WarcraftSim.Tests;

public sealed class ForeverAbilityTimingTests
{
    [Fact]
    public void CastHasteApplicationIsExplicitlyMarkedProvisional()
    {
        Assert.False(
            ForeverAbilityTimingProvider
                .CastHasteApplicationVerified
        );
    }

    [Fact]
    public void TwentyPercentHasteReducesThreeSecondCastToTwoPointFiveSeconds()
    {
        var source =
            CreateActor(
                "source"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            20m
        );

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    CreateAbility(
                        castTimeSeconds:
                            3m
                    )
                );

        Assert.Equal(
            2.5m,
            timing.CastTimeSeconds
        );
    }

    [Fact]
    public void ZeroHastePreservesAbilityTiming()
    {
        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    CreateActor(
                        "source"
                    ),
                    CreateAbility(
                        castTimeSeconds:
                            2.5m,

                        globalCooldownSeconds:
                            1.25m
                    )
                );

        Assert.Equal(
            2.5m,
            timing.CastTimeSeconds
        );

        Assert.Equal(
            1.25m,
            timing.GlobalCooldownSeconds
        );
    }

    [Fact]
    public void HasteDoesNotGloballyReduceAbilityGlobalCooldown()
    {
        var source =
            CreateActor(
                "source"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            50m
        );

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    CreateAbility(
                        castTimeSeconds:
                            3m,

                        globalCooldownSeconds:
                            1.5m
                    )
                );

        Assert.Equal(
            2m,
            timing.CastTimeSeconds
        );

        Assert.Equal(
            1.5m,
            timing.GlobalCooldownSeconds
        );
    }

    [Fact]
    public void HasteLeavesChannelTimingUnchangedUntilForeverBehaviorIsVerified()
    {
        var source =
            CreateActor(
                "source"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            25m
        );

        var ability =
            CreateAbility(
                castTimeSeconds:
                    0m
            );

        ability.ChannelDurationSeconds =
            6m;

        ability.ChannelTickIntervalSeconds =
            1m;

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    ability
                );

        Assert.Equal(
            6m,
            timing.ChannelDurationSeconds
        );

        Assert.Equal(
            1m,
            timing.ChannelTickIntervalSeconds
        );
    }

    [Fact]
    public void InstantCastRemainsInstantUnderHaste()
    {
        var source =
            CreateActor(
                "source"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            100m
        );

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    CreateAbility(
                        castTimeSeconds:
                            0m
                    )
                );

        Assert.Equal(
            0m,
            timing.CastTimeSeconds
        );
    }

    [Fact]
    public void NegativeHasteStatDoesNotSpeedCast()
    {
        var source =
            CreateActor(
                "source"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            -25m
        );

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    CreateAbility(
                        castTimeSeconds:
                            3m
                    )
                );

        Assert.Equal(
            3m,
            timing.CastTimeSeconds
        );
    }

    [Fact]
    public void AbilityExecutorUsesResolvedForeverCastAndActionTiming()
    {
        var source =
            CreateActor(
                "source"
            );

        var target =
            CreateActor(
                "target"
            );

        source.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0.2m
        );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            20m
        );

        source.AddAbility(
            CreateAbility(
                castTimeSeconds:
                    3m,

                globalCooldownSeconds:
                    1.5m
            )
        );

        var context =
            CreateContext();

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
                "test-cast"
            );

        Assert.True(
            result.Success
        );

        Assert.Equal(
            2.5m,
            source.CastReadyAtSeconds
        );

        Assert.Equal(
            1.5m,
            source.GlobalCooldownReadyAtSeconds
        );

        Assert.Equal(
            2.7m,
            source.InputReadyAtSeconds
        );
    }

    [Fact]
    public void AbilityExecutionSnapshotsResolvedTimingAtCastStart()
    {
        var source =
            CreateActor(
                "source"
            );

        var target =
            CreateActor(
                "target"
            );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            20m
        );

        source.AddAbility(
            CreateAbility(
                castTimeSeconds:
                    3m
            )
        );

        var context =
            CreateContext();

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
                "test-cast"
            );

        Assert.True(
            result.Success
        );

        var execution =
            context.GetAbilityExecution(
                source.CurrentCastExecutionId!.Value
            );

        Assert.NotNull(
            execution
        );

        source.Stats.Set(
            ForeverCombatStatKeys.HastePercent,
            0m
        );

        Assert.Equal(
            2.5m,
            execution!.Timing.CastTimeSeconds
        );
    }

    [Fact]
    public void HasteRatingPipelineFeedsForeverCastTimingProvider()
    {
        var profile =
            new CharacterProfile
            {
                Name =
                    "Mage",

                ClassKey =
                    ForeverCharacterClassKeys.Mage,

                Level =
                    30
            };

        profile.BaseStats.Set(
            ForeverCombatStatKeys.HasteRating,
            200m
        );

        var result =
            ForeverCharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            5000m
                )
                .Calculate(
                    profile
                );

        var source =
            CreateActor(
                "source"
            );

        source.Stats =
            result.EffectiveStats;

        var timing =
            new ForeverAbilityTimingProvider()
                .Resolve(
                    CreateContext(),
                    source,
                    CreateAbility(
                        castTimeSeconds:
                            3m
                    )
                );

        Assert.Equal(
            20m,
            source.Stats.Get(
                ForeverCombatStatKeys.HastePercent
            )
        );

        Assert.Equal(
            2.5m,
            timing.CastTimeSeconds
        );
    }

    private static AbilityDefinition CreateAbility(
        decimal castTimeSeconds,
        decimal globalCooldownSeconds = 1.5m)
    {
        return new AbilityDefinition
        {
            Key =
                "test-cast",

            Name =
                "Test Cast",

            CastTimeSeconds =
                castTimeSeconds,

            GlobalCooldownSeconds =
                globalCooldownSeconds
        };
    }

    private static SimulationActorState CreateActor(
        string key)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    key == "target"
                        ? "enemy"
                        : "raid",

                Level =
                    30
            };

        actor.InitializeHealth(
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

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    10m,

                PrimaryActorKey =
                    "source",

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
                        "forever-ability-timing-tests",

                    Version =
                        "1"
                }
            ),
            abilityTimingProvider:
                new ForeverAbilityTimingProvider()
        );
    }
}
