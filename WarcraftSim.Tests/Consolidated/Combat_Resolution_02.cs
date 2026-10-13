// Consolidated tests for Combat Resolution.
// Original class names are retained for xUnit test filtering.

// ==================== GlancingCrushingFoundationTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

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
}

// ==================== SingleRollAttackTableTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

public sealed class SingleRollAttackTableTests
{
    [Fact]
    public void OrderedTable_ResolvesOutcomesInDeclaredOrder()
    {
        var entries =
            new[]
            {
                Entry(
                    10m,
                    CombatRollResult.Miss()
                ),

                Entry(
                    20m,
                    CombatRollResult.Dodge()
                ),

                Entry(
                    30m,
                    CombatRollResult.Parry()
                )
            };

        Assert.Equal(
            CombatResultTypes.Miss,
            OrderedCombatRollTable.Resolve(
                9.999m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Dodge,
            OrderedCombatRollTable.Resolve(
                10m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Parry,
            OrderedCombatRollTable.Resolve(
                59.999m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Hit,
            OrderedCombatRollTable.Resolve(
                60m,
                entries
            ).ResultKey
        );
    }

    [Fact]
    public void OrderedTable_CriticalChanceOccupiesDirectTableSpace()
    {
        var entries =
            new[]
            {
                Entry(
                    20m,
                    CombatRollResult.Miss()
                ),

                Entry(
                    50m,
                    CombatRollResult.Critical(
                        2m
                    )
                )
            };

        Assert.Equal(
            CombatResultTypes.Miss,
            OrderedCombatRollTable.Resolve(
                19.999m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Critical,
            OrderedCombatRollTable.Resolve(
                20m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Critical,
            OrderedCombatRollTable.Resolve(
                69.999m,
                entries
            ).ResultKey
        );

        Assert.Equal(
            CombatResultTypes.Hit,
            OrderedCombatRollTable.Resolve(
                70m,
                entries
            ).ResultKey
        );
    }

    [Fact]
    public void OrderedTable_EarlierOutcomesCanPushLaterOutcomesOffTable()
    {
        var entries =
            new[]
            {
                Entry(
                    100m,
                    CombatRollResult.Block(
                        50m
                    )
                ),

                Entry(
                    100m,
                    CombatRollResult.Critical(
                        2m
                    )
                )
            };

        var result =
            OrderedCombatRollTable.Resolve(
                99.999m,
                entries
            );

        Assert.Equal(
            CombatResultTypes.Block,
            result.ResultKey
        );

        Assert.False(
            result.IsCritical
        );
    }

    [Fact]
    public void OrderedTable_RejectsRollOutsidePercentageRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                OrderedCombatRollTable.Resolve(
                    -0.001m,
                    []
                )
        );

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                OrderedCombatRollTable.Resolve(
                    100m,
                    []
                )
        );
    }

    [Fact]
    public void RulesetResolver_SingleRollTableKeepsBlockAheadOfCritical()
    {
        var source =
            CreateActor(
                "attacker",
                "raid"
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        var ruleset =
            CreateRuleset(
                useSingleRollTable:
                    true,
                blockChancePercent:
                    100m,
                criticalChancePercent:
                    100m
            );

        var result =
            new RulesetCombatRollResolver(
                ruleset
            )
            .Resolve(
                CreateContext(),
                source,
                target,
                CreateAbility(),
                CreateEffect(
                    canBeBlocked:
                        true,
                    canCrit:
                        true
                )
            );

        Assert.Equal(
            CombatResultTypes.Block,
            result.ResultKey
        );

        Assert.False(
            result.IsCritical
        );
    }

    [Fact]
    public void RulesetResolver_SingleRollTableStillAllowsCriticalWithoutEarlierOutcome()
    {
        var ruleset =
            CreateRuleset(
                useSingleRollTable:
                    true,
                criticalChancePercent:
                    100m
            );

        var result =
            new RulesetCombatRollResolver(
                ruleset
            )
            .Resolve(
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
                    canCrit:
                        true
                )
            );

        Assert.True(
            result.IsCritical
        );

        Assert.Equal(
            CombatResultTypes.Critical,
            result.ResultKey
        );
    }

    [Fact]
    public void RulesetResolver_DefaultStagedModeRemainsAvailable()
    {
        var ruleset =
            CreateRuleset(
                useSingleRollTable:
                    false,
                criticalChancePercent:
                    100m
            );

        var result =
            new RulesetCombatRollResolver(
                ruleset
            )
            .Resolve(
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
                    canCrit:
                        true
                )
            );

        Assert.True(
            result.IsCritical
        );

        Assert.Equal(
            CombatResultTypes.Critical,
            result.ResultKey
        );
    }

    [Fact]
    public void SimpleResolver_CanUseSameSingleRollTableFoundation()
    {
        var result =
            new SimpleCombatRollResolver(
                hitChancePercent:
                    100m,
                blockChancePercent:
                    100m,
                blockValue:
                    25m,
                criticalChancePercent:
                    100m,
                useSingleRollTable:
                    true
            )
            .Resolve(
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
                    canBeBlocked:
                        true,
                    canCrit:
                        true
                )
            );

        Assert.Equal(
            CombatResultTypes.Block,
            result.ResultKey
        );

        Assert.Equal(
            25m,
            result.BlockValue
        );

        Assert.False(
            result.IsCritical
        );
    }

    private static CombatRollTableEntry Entry(
        decimal chancePercent,
        CombatRollResult result)
    {
        return new CombatRollTableEntry
        {
            ChancePercent =
                chancePercent,

            Result =
                result
        };
    }

    private static CombatRulesetDefinition CreateRuleset(
        bool useSingleRollTable,
        decimal blockChancePercent = 0m,
        decimal criticalChancePercent = 0m)
    {
        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "single-roll-table-tests",

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

                BaseBlockChancePercent =
                    blockChancePercent,

                BaseBlockValue =
                    50m,

                BaseCriticalChancePercent =
                    criticalChancePercent,

                CriticalMultiplier =
                    2m
            };

        return ruleset;
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
        bool canBeBlocked = false,
        bool canCrit = false)
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

            CanBeBlocked =
                canBeBlocked,

            CanCrit =
                canCrit
        };
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
        string teamKey)
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

        return actor;
    }
}
}

// ==================== SpellCoefficientDistributionTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

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
}
