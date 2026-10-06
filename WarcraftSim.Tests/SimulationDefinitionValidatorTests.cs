using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Validation;

namespace WarcraftSim.Tests;

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
                    "unsupported-shield",

                Name =
                    "Unsupported Shield",

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "unsupported-shield-effect",

                        EffectType =
                            AbilityEffectTypes.Absorb
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
                    "unsupported effect type 'absorb'",
                    StringComparison.OrdinalIgnoreCase
                )
        );

        Assert.Empty(
            context.Timeline
        );
    }

    [Fact]
    public void Run_RejectsEffectLevelMultiTargetingBeforeSimulationStarts()
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
                    "unsupported-cleave",

                Name =
                    "Unsupported Cleave",

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "unsupported-cleave-effect",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        MaxTargets =
                            2
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
                    "multi-targeting is not supported yet",
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
    public void Run_RejectsSchemaOnlyTargetProximityLinks()
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Unsupported Proximity",

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
                    "target proximity is not supported yet",
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
