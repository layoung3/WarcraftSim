using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

public sealed class SpellCoefficientDistributionTests
{
    private const string SpellPower =
        "spell-power";

    [Theory]
    [InlineData(6.0, 2.0, false, 2)]
    [InlineData(6.0, 2.0, true, 3)]
    [InlineData(5.0, 2.0, false, 2)]
    [InlineData(1.0, 2.0, false, 0)]
    public void PeriodicScheduler_CountsActualScheduledTicks(
        double durationSeconds,
        double tickIntervalSeconds,
        bool includeExpirationBoundaryTick,
        int expectedTickCount)
    {
        var result =
            PeriodicEffectScheduler.GetScheduledTickCount(
                (decimal)durationSeconds,
                (decimal)tickIntervalSeconds,
                includeExpirationBoundaryTick
            );

        Assert.Equal(
            expectedTickCount,
            result
        );
    }

    [Fact]
    public void PeriodicScaling_DefaultModeRemainsPerOccurrence()
    {
        var source =
            CreateActor(
                "caster",
                "raid",
                spellPower:
                    120m
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            CreatePeriodicDamageAbility(
                "legacy-dot",
                AbilityEffectScalingCoefficientModes.PerOccurrence
            )
        );

        RunAbility(
            source,
            target,
            durationSeconds:
                7m
        );

        Assert.Equal(
            4760m,
            target.CurrentHealth
        );
    }

    [Fact]
    public void PeriodicScaling_TotalCoefficientIsDistributedAcrossTicks()
    {
        var source =
            CreateActor(
                "caster",
                "raid",
                spellPower:
                    120m
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            CreatePeriodicDamageAbility(
                "total-dot",
                AbilityEffectScalingCoefficientModes.TotalAcrossOccurrences
            )
        );

        RunAbility(
            source,
            target,
            durationSeconds:
                7m
        );

        Assert.Equal(
            4880m,
            target.CurrentHealth
        );
    }

    [Fact]
    public void PeriodicScaling_DistributesOnlyScalingAndKeepsBaseValuePerTick()
    {
        var source =
            CreateActor(
                "caster",
                "raid",
                spellPower:
                    120m
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        var ability =
            CreatePeriodicDamageAbility(
                "base-plus-scaling-dot",
                AbilityEffectScalingCoefficientModes.TotalAcrossOccurrences
            );

        ability.Effects.Single().MinimumValue =
            10m;

        ability.Effects.Single().MaximumValue =
            10m;

        source.AddAbility(
            ability
        );

        RunAbility(
            source,
            target,
            durationSeconds:
                7m
        );

        Assert.Equal(
            4860m,
            target.CurrentHealth
        );
    }

    [Fact]
    public void PeriodicHealing_TotalCoefficientIsDistributedAcrossTicks()
    {
        var source =
            CreateActor(
                "healer",
                "raid",
                spellPower:
                    120m
            );

        var target =
            CreateActor(
                "ally",
                "raid",
                startingHealth:
                    1000m
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "total-hot",
                Name = "total-hot",
                IsOffGlobalCooldown = true,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "total-hot-effect",
                        EffectType =
                            AbilityEffectTypes.PeriodicHealing,
                        TargetType =
                            AbilityTargetTypes.Friendly,
                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,
                        CanMiss = false,
                        CanCrit = false,
                        DurationSeconds = 6m,
                        TickIntervalSeconds = 2m,
                        ScalingStatKey = SpellPower,
                        ScalingCoefficient = 1m,
                        ScalingCoefficientMode =
                            AbilityEffectScalingCoefficientModes.TotalAcrossOccurrences
                    }
                ]
            }
        );

        RunAbility(
            source,
            target,
            durationSeconds:
                7m
        );

        Assert.Equal(
            1120m,
            target.CurrentHealth
        );
    }

    [Fact]
    public void ChannelScaling_TotalCoefficientIsDistributedAcrossChannelTicks()
    {
        var source =
            CreateActor(
                "caster",
                "raid",
                spellPower:
                    120m
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "beam",
                Name = "beam",
                IsOffGlobalCooldown = true,
                ChannelDurationSeconds = 3m,
                ChannelTickIntervalSeconds = 1m,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "beam-tick",
                        EffectType =
                            AbilityEffectTypes.DirectDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,
                        MitigationType =
                            DamageMitigationTypes.None,
                        CanMiss = false,
                        CanCrit = false,
                        ApplyOnChannelTick = true,
                        ScalingStatKey = SpellPower,
                        ScalingCoefficient = 1m,
                        ScalingCoefficientMode =
                            AbilityEffectScalingCoefficientModes.TotalAcrossOccurrences
                    }
                ]
            }
        );

        var result =
            RunAbility(
                source,
                target,
                durationSeconds:
                    4m
            );

        Assert.Equal(
            4880m,
            target.CurrentHealth
        );

        Assert.Equal(
            3,
            result.Timeline.Count(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            )
        );
    }

    [Fact]
    public void DirectScaling_TotalAcrossOccurrencesModeUsesSingleOccurrence()
    {
        var source =
            CreateActor(
                "caster",
                "raid",
                spellPower:
                    120m
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "bolt",
                Name = "bolt",
                IsOffGlobalCooldown = true,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "bolt-hit",
                        EffectType =
                            AbilityEffectTypes.DirectDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,
                        MitigationType =
                            DamageMitigationTypes.None,
                        CanMiss = false,
                        CanCrit = false,
                        ScalingStatKey = SpellPower,
                        ScalingCoefficient = 1m,
                        ScalingCoefficientMode =
                            AbilityEffectScalingCoefficientModes.TotalAcrossOccurrences
                    }
                ]
            }
        );

        RunAbility(
            source,
            target,
            durationSeconds:
                1m
        );

        Assert.Equal(
            4880m,
            target.CurrentHealth
        );
    }

    [Fact]
    public void Validation_RejectsUnknownScalingCoefficientMode()
    {
        var source =
            CreateActor(
                "caster",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "invalid-scaling-mode",
                Name = "invalid-scaling-mode",
                IsOffGlobalCooldown = true,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "invalid-effect",
                        EffectType =
                            AbilityEffectTypes.DirectDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,
                        CanMiss = false,
                        CanCrit = false,
                        ScalingCoefficientMode =
                            "unknown-mode"
                    }
                ]
            }
        );

        var context =
            CreateContext(
                source.Key,
                1m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var exception =
            Assert.Throws<SimulationDefinitionValidationException>(
                () =>
                    new SimulationEngine()
                        .Run(context)
            );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "invalid-effect",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                error.Contains(
                    "scaling coefficient mode",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static AbilityDefinition CreatePeriodicDamageAbility(
        string key,
        string scalingCoefficientMode)
    {
        return new AbilityDefinition
        {
            Key = key,
            Name = key,
            IsOffGlobalCooldown = true,
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = $"{key}-effect",
                    EffectType =
                        AbilityEffectTypes.PeriodicDamage,
                    TargetType =
                        AbilityTargetTypes.Enemy,
                    ResolutionType =
                        CombatResolutionTypes.AlwaysHits,
                    MitigationType =
                        DamageMitigationTypes.None,
                    CanMiss = false,
                    CanCrit = false,
                    DurationSeconds = 6m,
                    TickIntervalSeconds = 2m,
                    ScalingStatKey = SpellPower,
                    ScalingCoefficient = 1m,
                    ScalingCoefficientMode =
                        scalingCoefficientMode
                }
            ]
        };
    }

    private static SimulationRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState target,
        decimal durationSeconds)
    {
        var context =
            CreateContext(
                source.Key,
                durationSeconds
            );

        context.AddActor(
            source
        );

        if (!string.Equals(
                source.Key,
                target.Key,
                StringComparison.OrdinalIgnoreCase))
        {
            context.AddActor(
                target
            );
        }

        var auraManager =
            new AuraManager();

        var executor =
            new AbilityExecutor(
                auraManager,
                new SimpleCombatRollResolver(),
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey =
                            "spell-coefficient-distribution-tests",
                        Version =
                            "1"
                    }
                )
            );

        return new SimulationEngine(
            [
                executor,
                auraManager
            ])
            .Run(
                context,
                startedContext =>
                {
                    Assert.True(
                        executor.TryStartAbility(
                            startedContext,
                            source.Key,
                            target.Key,
                            source.Abilities.Values
                                .Single()
                                .Definition.Key
                        ).Success
                    );
                }
            );
    }

    private static SimulationContext CreateContext(
        string primaryActorKey,
        decimal durationSeconds)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds =
                    durationSeconds,
                PrimaryActorKey =
                    primaryActorKey,
                CaptureTimeline = true
            }
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        decimal spellPower = 0m,
        decimal? startingHealth = null)
    {
        var actor =
            new SimulationActorState
            {
                Key = key,
                Name = key,
                TeamKey = teamKey,
                Level = 30
            };

        actor.InitializeHealth(
            maximumHealth:
                5000m,
            startingHealth:
                startingHealth
        );

        actor.Stats.Set(
            SpellPower,
            spellPower
        );

        actor.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,
            inputDelaySeconds:
                0m
        );

        return actor;
    }
}
