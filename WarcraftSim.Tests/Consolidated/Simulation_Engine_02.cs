// Consolidated tests for Simulation Engine.
// Original class names are retained for xUnit test filtering.

// ==================== SimulationBoundaryContractTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationBoundaryContractTests
{
    [Fact]
    public void Run_UsesHalfOpenGameplayWindow()
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
                        "player",

                    CaptureTimeline =
                        true
                }
            );

        context.AddActor(
            CreateActor(
                "player"
            )
        );

        context.AddActor(
            CreateActor(
                "target"
            )
        );

        var engine =
            new SimulationEngine(
                [
                    new BoundaryEventScheduler()
                ]
            );

        var result =
            engine.Run(
                context
            );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.Damage &&
                combatEvent.TimeSeconds ==
                    0.999m
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                    CombatEventType.Damage &&
                combatEvent.TimeSeconds ==
                    1m
        );

        var ended =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                    CombatEventType.SimulationEnded
            );

        Assert.Equal(
            1m,
            ended.TimeSeconds
        );

        Assert.Equal(
            10m,
            result.Summary.DamageDone
        );
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
                    key == "player"
                        ? "raid"
                        : "enemy"
            };

        actor.InitializeHealth(
            maximumHealth:
                1000m
        );

        return actor;
    }

    private sealed class BoundaryEventScheduler :
        ICombatEventProcessor
    {
        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            if (
                combatEvent.Type !=
                    CombatEventType.SimulationStarted
            )
            {
                return;
            }

            context.ScheduleEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        0.999m,

                    Type =
                        CombatEventType.Damage,

                    SourceActorKey =
                        "player",

                    TargetActorKey =
                        "target",

                    Amount =
                        10m,

                    Description =
                        "Damage immediately before the simulation boundary."
                }
            );

            context.ScheduleEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        1m,

                    Type =
                        CombatEventType.Damage,

                    SourceActorKey =
                        "player",

                    TargetActorKey =
                        "target",

                    Amount =
                        100m,

                    Description =
                        "Damage exactly at the simulation boundary."
                }
            );
        }
    }
}
}

// ==================== SimulationDefinitionValidatorTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

public sealed class SimulationDefinitionValidatorTests
{
    [Fact]
    public void Run_RejectsUnsupportedAbilityEffectTypeBeforeSimulationStarts()
    {
        var context =
            CreateContext();

        var player =
            CreateActor(
                "player",
                "raid"
            );

        player.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "unsupported-summon",

                Name =
                    "Unsupported Summon",

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "unsupported-summon-effect",

                        EffectType =
                            AbilityEffectTypes.Summon
                    }
                ]
            }
        );

        context.AddActor(
            player
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
                    "unsupported effect type 'summon'",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Empty(
            context.Timeline
        );
    }

    [Fact]
    public void Run_RejectsInvalidEffectTargetCountBeforeSimulationStarts()
    {
        var context =
            CreateContext();

        var player =
            CreateActor(
                "player",
                "raid"
            );

        player.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "invalid-target-count",

                Name =
                    "Invalid Target Count",

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "invalid-target-count-effect",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        MaxTargets =
                            0
                    }
                ]
            }
        );

        context.AddActor(
            player
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
                    "MaxTargets to be at least 1",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    [Fact]
    public void Run_RejectsMissingEffectDependencyBeforeSimulationStarts()
    {
        var context =
            CreateContext();

        var player =
            CreateActor(
                "player",
                "raid"
            );

        player.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "dependency-test",

                Name =
                    "Dependency Test",

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "dependent-effect",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        DependsOnEffectKey =
                            "missing-effect"
                    }
                ]
            }
        );

        context.AddActor(
            player
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
                    "depends on missing effect 'missing-effect'",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    [Fact]
    public void Run_RejectsRotationEntryReferencingUnknownAbility()
    {
        var context =
            CreateContext();

        context.AddActor(
            CreateActor(
                "player",
                "raid"
            )
        );

        context.AddActor(
            CreateActor(
                "target",
                "enemy"
            )
        );

        var rotation =
            new RotationProfile
            {
                Name =
                    "Invalid Rotation",

                Entries =
                [
                    new RotationEntry
                    {
                        AbilityKey =
                            "missing-ability",

                        Priority =
                            1
                    }
                ]
            };

        var abilityExecutor =
            CreateAbilityExecutor();

        var engine =
            new SimulationEngine(
                [
                    abilityExecutor,
                    new PriorityRotationExecutor(
                        rotation,
                        "player",
                        "target",
                        abilityExecutor
                    )
                ]
            );

        var exception =
            Assert.Throws<SimulationDefinitionValidationException>(
                () =>
                    engine.Run(
                        context
                    )
            );

        Assert.Contains(
            exception.Errors,
            error =>
                error.Contains(
                    "missing-ability",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                error.Contains(
                    "does not have",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    [Fact]
    public void Run_RejectsSchemaOnlyEncounterTargets()
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Unsupported Target Materialization",

                Targets =
                [
                    new EncounterTarget
                    {
                        Key =
                            "boss",

                        Name =
                            "Boss",

                        MaxHealth =
                            10000m
                    }
                ]
            };

        var context =
            CreateContext(
                encounter
            );

        context.AddActor(
            CreateActor(
                "player",
                "raid"
            )
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
                    "target materialization is not supported yet",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    [Fact]
    public void Run_AcceptsActorBackedTargetProximityLinks()
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Supported Proximity",

                TargetProximityLinks =
                [
                    new TargetProximityLink
                    {
                        TargetAKey =
                            "target-a",

                        TargetBKey =
                            "target-b"
                    }
                ]
            };

        var context =
            CreateContext(
                encounter
            );

        context.AddActor(
            CreateActor(
                "player",
                "raid"
            )
        );

        context.AddActor(
            CreateActor(
                "target-a",
                "enemy"
            )
        );

        context.AddActor(
            CreateActor(
                "target-b",
                "enemy"
            )
        );

        var result =
            new SimulationEngine()
                .Run(
                    context
                );

        Assert.NotNull(result);
    }

    [Fact]
    public void Run_RejectsTargetProximityLinkThatReferencesMissingActor()
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Invalid Proximity",

                TargetProximityLinks =
                [
                    new TargetProximityLink
                    {
                        TargetAKey =
                            "target-a",

                        TargetBKey =
                            "missing-target"
                    }
                ]
            };

        var context =
            CreateContext(
                encounter
            );

        context.AddActor(
            CreateActor(
                "player",
                "raid"
            )
        );

        context.AddActor(
            CreateActor(
                "target-a",
                "enemy"
            )
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
                    "missing actor 'missing-target'",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private static SimulationContext CreateContext(
        EncounterProfile? encounter = null)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    5m,

                PrimaryActorKey =
                    "player",

                CaptureTimeline =
                    true
            },
            encounter
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
                    70
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

    private static AbilityExecutor CreateAbilityExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "definition-validation-tests",

                    Version =
                        "1"
                }
            )
        );
    }
}
}

// ==================== SimulationRegressionTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Api.Development;
using WarcraftSim.Core.Simulation.Engine;

public sealed class SimulationRegressionTests
{
    [Fact]
    public void CombatSmoke_IsDeterministic()
    {
        var first =
            DevelopmentScenarios.RunCombatSmoke(
                seed: 12345,
                captureTimeline: true
            );

        var second =
            DevelopmentScenarios.RunCombatSmoke(
                seed: 12345,
                captureTimeline: true
            );

        Assert.Equal(
            first.Summary.DamageDone,
            second.Summary.DamageDone
        );

        Assert.True(
            first.Summary.DamageDone > 0m
        );

        Assert.Contains(
            first.Timeline,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.Damage
        );
    }

    [Fact]
    public void HealingThroughput_TracksResourceStarvation()
    {
        var result =
            DevelopmentScenarios.RunHealingThroughput(
                seed: 12345,
                captureTimeline: true
            );

        var metrics =
            result.Metrics;

        Assert.Equal(
            60m,
            metrics.WindowSeconds
        );

        Assert.True(
            metrics.EffectiveHealing > 0m
        );

        Assert.True(
            metrics.EffectiveHps > 0m
        );

        Assert.True(
            metrics.EndingResource <
            metrics.StartingResource
        );

        Assert.True(
            metrics.BecameResourceStarved
        );

        Assert.NotNull(
            metrics.FirstResourceStarvedAtSeconds
        );

        Assert.True(
            metrics.ResourceStarvedSeconds > 0m
        );

        Assert.True(
            metrics.CompletedCastsByAbility.Count >= 2
        );
    }

    [Fact]
    public void RaidHealing_UsesNamedEncounterDamageBreakdowns()
    {
        var result =
            DevelopmentScenarios.RunRaidHealing(
                seed: 12345,
                captureTimeline: true
            );

        var tankSummary =
            result.Summary.ActorSummaries[
                "tank"
            ];

        Assert.Contains(
            "encounter:tank-swing",
            tankSummary.DamageTakenByAbility.Keys
        );

        Assert.DoesNotContain(
            "unknown",
            tankSummary.DamageTakenByAbility.Keys
        );
    }

    [Fact]
    public void RaidHealing_AppliesMitigationToScriptedDamage()
    {
        var result =
            DevelopmentScenarios.RunRaidHealing(
                seed: 12345,
                captureTimeline: true
            );

        var tankSwing =
            result.Timeline.First(
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage &&
                    string.Equals(
                        combatEvent.AbilityKey,
                        "encounter:tank-swing",
                        StringComparison.OrdinalIgnoreCase
                    )
            );

        Assert.NotNull(
            tankSwing.RawAmount
        );

        Assert.NotNull(
            tankSwing.MitigatedAmount
        );

        Assert.NotNull(
            tankSwing.MitigationPercent
        );

        Assert.NotNull(
            tankSwing.Amount
        );

        Assert.True(
            tankSwing.RawAmount!.Value >
            tankSwing.Amount!.Value
        );

        Assert.True(
            tankSwing.MitigatedAmount!.Value > 0m
        );

        Assert.True(
            tankSwing.MitigationPercent!.Value > 0m
        );
    }

    [Fact]
    public void RaidHealing_HealsMultipleRaidMembers()
    {
        var result =
            DevelopmentScenarios.RunRaidHealing(
                seed: 12345,
                captureTimeline: true
            );

        var healedTargets =
            result.Timeline
                .Where(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.Healing &&
                        string.Equals(
                            combatEvent.SourceActorKey,
                            "healer",
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .Select(
                    combatEvent =>
                        combatEvent.TargetActorKey
                )
                .Where(
                    actorKey =>
                        !string.IsNullOrWhiteSpace(
                            actorKey
                        )
                )
                .Select(
                    actorKey =>
                        actorKey!
                )
                .Distinct(
                    StringComparer.OrdinalIgnoreCase
                )
                .ToList();

        Assert.Contains(
            "tank",
            healedTargets
        );

        Assert.Contains(
            "dps-1",
            healedTargets
        );

        Assert.Contains(
            "dps-2",
            healedTargets
        );
    }

    [Fact]
    public void EncounterDamagePatterns_RepeatAtExpectedTimes()
    {
        var result =
            DevelopmentScenarios.RunRaidHealing(
                seed: 12345,
                captureTimeline: true
            );

        var tankSwingTimes =
            result.Timeline
                .Where(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.Damage &&
                        string.Equals(
                            combatEvent.AbilityKey,
                            "encounter:tank-swing",
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .Select(
                    combatEvent =>
                        combatEvent.TimeSeconds
                )
                .ToList();

        Assert.Equal(
            [
                1.5m,
                3.5m,
                5.5m,
                7.5m,
                9.5m
            ],
            tankSwingTimes
        );
    }
}
}

// ==================== StandardRuntimePipelineTests.cs ====================
namespace WarcraftSim.Tests
{
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Characters.Buffs;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Characters.Talents;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Stats;

public sealed class StandardRuntimePipelineTests
{
    [Fact]
    public void CreateStandard_UsesOfficialContributorOrder()
    {
        var contributors =
            CharacterSimulationStatContributorPipeline
                .CreateStandard();

        Assert.Equal(
            new[]
            {
                "equipment",
                "talents",
                "buffs"
            },
            contributors
                .Select(
                    contributor =>
                        contributor.Key
                )
                .ToArray()
        );

        Assert.Equal(
            CharacterSimulationStatContributorOrders.Equipment,
            contributors[0].Order
        );

        Assert.Equal(
            CharacterSimulationStatContributorOrders.Talents,
            contributors[1].Order
        );

        Assert.Equal(
            CharacterSimulationStatContributorOrders.Buffs,
            contributors[2].Order
        );
    }

    [Fact]
    public void StandardContributors_DefaultToOfficialOrders()
    {
        Assert.Equal(
            CharacterSimulationStatContributorOrders.Equipment,
            new CharacterEquipmentStatContributor().Order
        );

        Assert.Equal(
            CharacterSimulationStatContributorOrders.Talents,
            new CharacterTalentStatContributor().Order
        );

        Assert.Equal(
            CharacterSimulationStatContributorOrders.Buffs,
            new CharacterBuffStatContributor().Order
        );
    }

    [Fact]
    public void CreateStandardCalculator_AggregatesEquipmentTalentsAndBuffs()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var item =
            new CharacterEquipmentItem
            {
                SlotKey =
                    "head",

                ItemKey =
                    "helmet",

                Name =
                    "Heavy Helmet"
            };

        item.Stats.Set(
            "strength",
            20m
        );

        profile.Equipment.Items.Add(
            item
        );

        var talent =
            new CharacterTalentSelection
            {
                TalentKey =
                    "battle-conditioning",

                Name =
                    "Battle Conditioning",

                Rank =
                    1
            };

        talent.Stats.Set(
            "strength",
            15m
        );

        profile.Talents.Selections.Add(
            talent
        );

        var buff =
            new CharacterBuffSelection
            {
                BuffKey =
                    "raid-strength",

                Name =
                    "Raid Strength",

                IsEnabled =
                    true
            };

        buff.Stats.Set(
            "strength",
            25m
        );

        profile.Buffs.Selections.Add(
            buff
        );

        var result =
            CharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            10000m
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            160m,
            result.EffectiveStats.Get(
                "strength"
            )
        );
    }

    [Fact]
    public void CreateStandardCalculator_PreservesSourceProvenanceAcrossPipeline()
    {
        var profile =
            CreateProfile();

        var item =
            new CharacterEquipmentItem
            {
                SlotKey =
                    "head",

                ItemKey =
                    "helmet",

                Name =
                    "Heavy Helmet"
            };

        item.Stats.Set(
            "armor",
            900m
        );

        profile.Equipment.Items.Add(
            item
        );

        var talent =
            new CharacterTalentSelection
            {
                TalentKey =
                    "armor-training",

                Name =
                    "Armor Training",

                Rank =
                    1
            };

        talent.Stats.Set(
            "armor",
            300m
        );

        profile.Talents.Selections.Add(
            talent
        );

        var buff =
            new CharacterBuffSelection
            {
                BuffKey =
                    "raid-armor",

                Name =
                    "Raid Armor",

                IsEnabled =
                    true
            };

        buff.Stats.Set(
            "armor",
            100m
        );

        profile.Buffs.Selections.Add(
            buff
        );

        var result =
            CharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            10000m
                )
                .Calculate(
                    profile
                );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.SourceType ==
                    "equipment" &&
                contribution.SourceName ==
                    "Heavy Helmet" &&
                contribution.Amount ==
                    900m
        );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.SourceType ==
                    "talent" &&
                contribution.SourceName ==
                    "Armor Training" &&
                contribution.Amount ==
                    300m
        );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.SourceType ==
                    "buff" &&
                contribution.SourceName ==
                    "Raid Armor" &&
                contribution.Amount ==
                    100m
        );
    }

    [Fact]
    public void CreateStandardCalculator_AllowsAdditionalContributors()
    {
        var profile =
            CreateProfile();

        profile.BaseStats.Set(
            "strength",
            100m
        );

        var additionalContributor =
            new ExplicitStatBonusContributor(
                key:
                    "ruleset-adjustment",

                order:
                    CharacterSimulationStatContributorOrders.Buffs +
                    100,

                statBonuses:
                    new Dictionary<string, decimal>
                    {
                        ["strength"] =
                            50m
                    }
            );

        var result =
            CharacterSimulationRuntimeCalculatorFactory
                .CreateStandard(
                    maximumHealthResolver:
                        _ =>
                            10000m,

                    additionalStatContributors:
                        [
                            additionalContributor
                        ]
                )
                .Calculate(
                    profile
                );

        Assert.Equal(
            150m,
            result.EffectiveStats.Get(
                "strength"
            )
        );

        Assert.Contains(
            result.StatContributions,
            contribution =>
                contribution.ContributorKey ==
                    "ruleset-adjustment" &&
                contribution.Amount ==
                    50m
        );
    }

    [Fact]
    public void CreateStandardCalculator_RejectsDuplicateStandardContributorKey()
    {
        var duplicateEquipmentContributor =
            new TestContributor(
                key:
                    "EQUIPMENT",

                order:
                    500
            );

        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    CharacterSimulationRuntimeCalculatorFactory
                        .CreateStandard(
                            maximumHealthResolver:
                                _ =>
                                    10000m,

                            additionalStatContributors:
                                [
                                    duplicateEquipmentContributor
                                ]
                        )
            );

        Assert.Contains(
            "Duplicate character stat contributor key",
            exception.Message,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static CharacterProfile CreateProfile()
    {
        return new CharacterProfile
        {
            Id =
                Guid.Parse(
                    "fc0da533-4df6-4a2a-9302-c5d7f3b1c227"
                ),

            Name =
                "Standard Pipeline Character",

            RulesetKey =
                "development",

            ClassKey =
                "warrior",

            SpecializationKey =
                "protection",

            Level =
                70
        };
    }

    private sealed class TestContributor :
        ICharacterSimulationStatContributor
    {
        public TestContributor(
            string key,
            int order)
        {
            Key =
                key;

            Order =
                order;
        }

        public string Key { get; }

        public int Order { get; }

        public void Contribute(
            CharacterProfile profile,
            StatCollection effectiveStats)
        {
        }
    }
}
}
