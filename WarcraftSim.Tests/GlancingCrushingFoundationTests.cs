using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

public sealed class GlancingCrushingFoundationTests
{
    [Fact]
    public void CombatRollResult_GlancingAndCrushingAreLandedScaledResults()
    {
        var glancing =
            CombatRollResult.Glancing(
                0.7m
            );

        Assert.True(
            glancing.Landed
        );

        Assert.True(
            glancing.IsGlancing
        );

        Assert.False(
            glancing.IsCritical
        );

        Assert.Equal(
            CombatResultTypes.Glancing,
            glancing.ResultKey
        );

        Assert.Equal(
            0.7m,
            glancing.AmountMultiplier
        );

        var crushing =
            CombatRollResult.Crushing(
                1.5m
            );

        Assert.True(
            crushing.Landed
        );

        Assert.True(
            crushing.IsCrushing
        );

        Assert.False(
            crushing.IsCritical
        );

        Assert.Equal(
            CombatResultTypes.Crushing,
            crushing.ResultKey
        );

        Assert.Equal(
            1.5m,
            crushing.AmountMultiplier
        );
    }

    [Fact]
    public void SingleRollTable_GlancingOccupiesSpaceBeforeBlock()
    {
        var resolver =
            CreateResolver(
                glancingChancePercent:
                    100m,
                blockChancePercent:
                    100m,
                glancingMultiplier:
                    0.8m
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                CreateActor(
                    "attacker",
                    "raid"
                ),
                CreateActor(
                    "target",
                    "enemy"
                ),
                CreateAbility(),
                CreateEffect(
                    canGlance:
                        true,
                    canBeBlocked:
                        true
                )
            );

        Assert.Equal(
            CombatResultTypes.Glancing,
            result.ResultKey
        );

        Assert.Equal(
            0.8m,
            result.AmountMultiplier
        );
    }

    [Fact]
    public void SingleRollTable_CriticalOccupiesSpaceBeforeCrushing()
    {
        var resolver =
            CreateResolver(
                criticalChancePercent:
                    100m,
                crushingChancePercent:
                    100m,
                crushingMultiplier:
                    1.5m
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                CreateActor(
                    "attacker",
                    "enemy"
                ),
                CreateActor(
                    "target",
                    "raid"
                ),
                CreateAbility(),
                CreateEffect(
                    canCrit:
                        true,
                    canCrush:
                        true
                )
            );

        Assert.Equal(
            CombatResultTypes.Critical,
            result.ResultKey
        );

        Assert.True(
            result.IsCritical
        );

        Assert.False(
            result.IsCrushing
        );
    }

    [Fact]
    public void SingleRollTable_CrushingResolvesWhenEarlierOutcomesDoNotFillTable()
    {
        var resolver =
            CreateResolver(
                crushingChancePercent:
                    100m,
                crushingMultiplier:
                    1.5m
            );

        var result =
            resolver.Resolve(
                CreateContext(),
                CreateActor(
                    "attacker",
                    "enemy"
                ),
                CreateActor(
                    "target",
                    "raid"
                ),
                CreateAbility(),
                CreateEffect(
                    canCrush:
                        true
                )
            );

        Assert.True(
            result.IsCrushing
        );

        Assert.Equal(
            CombatResultTypes.Crushing,
            result.ResultKey
        );

        Assert.Equal(
            1.5m,
            result.AmountMultiplier
        );
    }

    [Fact]
    public void GlancingDamageMultiplierIsAppliedBeforeMitigation()
    {
        var attacker =
            CreateActor(
                "attacker",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        attacker.AddAbility(
            CreateDamageAbility(
                "white-swing",
                100m,
                canGlance:
                    true
            )
        );

        var result =
            RunAbility(
                attacker,
                target,
                new SimpleCombatRollResolver(
                    useSingleRollTable:
                        true,
                    glancingChancePercent:
                        100m,
                    minimumGlancingDamageMultiplier:
                        0.7m,
                    maximumGlancingDamageMultiplier:
                        0.7m
                )
            );

        Assert.Equal(
            4930m,
            target.CurrentHealth
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            );

        Assert.Equal(
            CombatResultTypes.Glancing,
            damage.ResultKey
        );

        Assert.Equal(
            70m,
            damage.RawAmount
        );

        Assert.Equal(
            70m,
            damage.Amount
        );
    }

    [Fact]
    public void CrushingDamageMultiplierIsAppliedBeforeMitigation()
    {
        var attacker =
            CreateActor(
                "boss",
                "enemy"
            );

        var target =
            CreateActor(
                "tank",
                "raid"
            );

        attacker.AddAbility(
            CreateDamageAbility(
                "boss-swing",
                100m,
                canCrush:
                    true
            )
        );

        var result =
            RunAbility(
                attacker,
                target,
                new SimpleCombatRollResolver(
                    useSingleRollTable:
                        true,
                    crushingChancePercent:
                        100m,
                    crushingDamageMultiplier:
                        1.5m
                )
            );

        Assert.Equal(
            4850m,
            target.CurrentHealth
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            );

        Assert.Equal(
            CombatResultTypes.Crushing,
            damage.ResultKey
        );

        Assert.Equal(
            150m,
            damage.RawAmount
        );

        Assert.Equal(
            150m,
            damage.Amount
        );
    }

    [Fact]
    public void GlancingAndCrushingDependencyConditionsCanTriggerFollowups()
    {
        var glancingSource =
            CreateActor(
                "glancing-source",
                "raid",
                resourceKey:
                    "rage",
                maximum:
                    100m,
                current:
                    0m
            );

        var glancingTarget =
            CreateActor(
                "glancing-target",
                "enemy"
            );

        glancingSource.AddAbility(
            CreateReactiveAbility(
                "glancing-reactive",
                canGlance:
                    true,
                canCrush:
                    false,
                dependencyCondition:
                    EffectDependencyConditions.Glancing
            )
        );

        RunAbility(
            glancingSource,
            glancingTarget,
            new SimpleCombatRollResolver(
                useSingleRollTable:
                    true,
                glancingChancePercent:
                    100m,
                minimumGlancingDamageMultiplier:
                    0.8m,
                maximumGlancingDamageMultiplier:
                    0.8m
            )
        );

        Assert.Equal(
            10m,
            glancingSource.Resources["rage"].Current
        );

        var crushingSource =
            CreateActor(
                "crushing-source",
                "enemy",
                resourceKey:
                    "rage",
                maximum:
                    100m,
                current:
                    0m
            );

        var crushingTarget =
            CreateActor(
                "crushing-target",
                "raid"
            );

        crushingSource.AddAbility(
            CreateReactiveAbility(
                "crushing-reactive",
                canGlance:
                    false,
                canCrush:
                    true,
                dependencyCondition:
                    EffectDependencyConditions.Crushing
            )
        );

        RunAbility(
            crushingSource,
            crushingTarget,
            new SimpleCombatRollResolver(
                useSingleRollTable:
                    true,
                crushingChancePercent:
                    100m,
                crushingDamageMultiplier:
                    1.5m
            )
        );

        Assert.Equal(
            10m,
            crushingSource.Resources["rage"].Current
        );
    }

    [Fact]
    public void ResolverRejectsGlancingOrCrushingWithoutSingleRollTable()
    {
        var effect =
            CreateEffect(
                canGlance:
                    true
            );

        var rulesetResolver =
            CreateResolver(
                useSingleRollTable:
                    false,
                glancingChancePercent:
                    100m
            );

        var exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    rulesetResolver.Resolve(
                        CreateContext(),
                        CreateActor(
                            "attacker",
                            "raid"
                        ),
                        CreateActor(
                            "target",
                            "enemy"
                        ),
                        CreateAbility(),
                        effect
                    )
            );

        Assert.Contains(
            "single-roll",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );

        var simpleResolver =
            new SimpleCombatRollResolver(
                glancingChancePercent:
                    100m
            );

        Assert.Throws<InvalidOperationException>(
            () =>
                simpleResolver.Resolve(
                    CreateContext(),
                    CreateActor(
                        "attacker",
                        "raid"
                    ),
                    CreateActor(
                        "target",
                        "enemy"
                    ),
                    CreateAbility(),
                    effect
                )
        );
    }

    [Fact]
    public void ValidatorRejectsSpecialTableOutcomesOnUnsupportedEffectShapes()
    {
        var source =
            CreateActor(
                "source",
                "raid"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "invalid-special-results",

                Name =
                    "Invalid Special Results",

                IsOffGlobalCooldown =
                    true,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "glancing-hot",

                        EffectType =
                            AbilityEffectTypes.PeriodicHealing,

                        TargetType =
                            AbilityTargetTypes.Friendly,

                        ResolutionType =
                            CombatResolutionTypes.Spell,

                        DurationSeconds =
                            3m,

                        TickIntervalSeconds =
                            1m,

                        CanGlance =
                            true
                    },

                    new AbilityEffectDefinition
                    {
                        Key =
                            "always-hit-crush",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        CanCrush =
                            true
                    }
                ]
            }
        );

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        1m,

                    PrimaryActorKey =
                        source.Key
                }
            );

        context.AddActor(
            source
        );

        var exception =
            Assert.Throws<SimulationDefinitionValidationException>(
                () =>
                    new SimulationEngine()
                        .Run(
                            context
                        )
            );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "only supported on direct damage",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "cannot use the always-hits resolution type",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static RulesetCombatRollResolver CreateResolver(
        bool useSingleRollTable = true,
        decimal glancingChancePercent = 0m,
        decimal glancingMultiplier = 1m,
        decimal blockChancePercent = 0m,
        decimal criticalChancePercent = 0m,
        decimal crushingChancePercent = 0m,
        decimal crushingMultiplier = 1m)
    {
        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "special-table-tests",

                Version =
                    "1"
            };

        ruleset.RollRules[
            CombatResolutionTypes.Melee
        ] =
            new CombatRollRuleDefinition
            {
                ResolutionType =
                    CombatResolutionTypes.Melee,

                UseSingleRollTable =
                    useSingleRollTable,

                BaseHitChancePercent =
                    100m,

                BaseGlancingChancePercent =
                    glancingChancePercent,

                MinimumGlancingDamageMultiplier =
                    glancingMultiplier,

                MaximumGlancingDamageMultiplier =
                    glancingMultiplier,

                BaseBlockChancePercent =
                    blockChancePercent,

                BaseBlockValue =
                    25m,

                BaseCriticalChancePercent =
                    criticalChancePercent,

                CriticalMultiplier =
                    2m,

                BaseCrushingChancePercent =
                    crushingChancePercent,

                CrushingDamageMultiplier =
                    crushingMultiplier
            };

        return new RulesetCombatRollResolver(
            ruleset
        );
    }

    private static AbilityDefinition CreateAbility()
    {
        return new AbilityDefinition
        {
            Key =
                "attack",

            Name =
                "Attack"
        };
    }

    private static AbilityEffectDefinition CreateEffect(
        bool canGlance = false,
        bool canBeBlocked = false,
        bool canCrit = false,
        bool canCrush = false)
    {
        return new AbilityEffectDefinition
        {
            Key =
                "attack-effect",

            EffectType =
                AbilityEffectTypes.DirectDamage,

            TargetType =
                AbilityTargetTypes.Enemy,

            ResolutionType =
                CombatResolutionTypes.Melee,

            CanMiss =
                false,

            CanBeDodged =
                false,

            CanBeParried =
                false,

            CanGlance =
                canGlance,

            CanBeBlocked =
                canBeBlocked,

            CanCrit =
                canCrit,

            CanCrush =
                canCrush
        };
    }

    private static AbilityDefinition CreateDamageAbility(
        string key,
        decimal amount,
        bool canGlance = false,
        bool canCrush = false)
    {
        return new AbilityDefinition
        {
            Key =
                key,

            Name =
                key,

            IsOffGlobalCooldown =
                true,

            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key =
                        $"{key}-effect",

                    EffectType =
                        AbilityEffectTypes.DirectDamage,

                    TargetType =
                        AbilityTargetTypes.Enemy,

                    ResolutionType =
                        CombatResolutionTypes.Melee,

                    MitigationType =
                        DamageMitigationTypes.None,

                    CanMiss =
                        false,

                    CanBeDodged =
                        false,

                    CanBeParried =
                        false,

                    CanGlance =
                        canGlance,

                    CanBeBlocked =
                        false,

                    CanCrit =
                        false,

                    CanCrush =
                        canCrush,

                    MinimumValue =
                        amount,

                    MaximumValue =
                        amount
                }
            ]
        };
    }

    private static AbilityDefinition CreateReactiveAbility(
        string key,
        bool canGlance,
        bool canCrush,
        string dependencyCondition)
    {
        return new AbilityDefinition
        {
            Key =
                key,

            Name =
                key,

            IsOffGlobalCooldown =
                true,

            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key =
                        "attack",

                    EffectType =
                        AbilityEffectTypes.DirectDamage,

                    TargetType =
                        AbilityTargetTypes.Enemy,

                    ResolutionType =
                        CombatResolutionTypes.Melee,

                    MitigationType =
                        DamageMitigationTypes.None,

                    CanMiss =
                        false,

                    CanGlance =
                        canGlance,

                    CanCrit =
                        false,

                    CanCrush =
                        canCrush,

                    MinimumValue =
                        100m,

                    MaximumValue =
                        100m
                },

                new AbilityEffectDefinition
                {
                    Key =
                        "resource-followup",

                    EffectType =
                        AbilityEffectTypes.ResourceChange,

                    TargetType =
                        AbilityTargetTypes.Self,

                    ResolutionType =
                        CombatResolutionTypes.AlwaysHits,

                    CanMiss =
                        false,

                    CanCrit =
                        false,

                    ResourceKey =
                        "rage",

                    ResourceChangeOperation =
                        ResourceChangeOperationTypes.Gain,

                    MinimumValue =
                        10m,

                    MaximumValue =
                        10m,

                    DependsOnEffectKey =
                        "attack",

                    DependencyCondition =
                        dependencyCondition
                }
            ]
        };
    }

    private static SimulationRunResult RunAbility(
        SimulationActorState source,
        SimulationActorState target,
        ICombatRollResolver resolver)
    {
        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        1m,

                    PrimaryActorKey =
                        source.Key,

                    CaptureTimeline =
                        true
                }
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var executor =
            new AbilityExecutor(
                new AuraManager(),
                resolver,
                new RulesetDamageMitigationResolver(
                    new CombatRulesetDefinition
                    {
                        RulesetKey =
                            "special-damage-tests",

                        Version =
                            "1"
                    }
                )
            );

        return new SimulationEngine(
            [
                executor
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

    private static SimulationContext CreateContext()
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    1m,

                PrimaryActorKey =
                    "attacker"
            }
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        string? resourceKey = null,
        decimal maximum = 0m,
        decimal current = 0m)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    teamKey,

                Level =
                    60
            };

        actor.InitializeHealth(
            maximumHealth:
                5000m
        );

        actor.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0m
        );

        if (!string.IsNullOrWhiteSpace(
                resourceKey))
        {
            actor.AddResource(
                new ResourceState(
                    resourceKey,
                    maximum,
                    current
                )
            );
        }

        return actor;
    }
}
