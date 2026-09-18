using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Development;

public static class DevelopmentEncounterSimulationFactory
{
    public static SimulationRunResult Run(
        int seed = 12345,
        bool captureTimeline = true)
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Timed Execute Test",

                RulesetKey =
                    "development",

                DurationSeconds =
                    12m,

                Phases =
                [
                    new EncounterPhaseDefinition
                    {
                        Key =
                            "normal",

                        Name =
                            "Normal Phase",

                        StartTimeSeconds =
                            0m,

                        EndTimeSeconds =
                            6m
                    },

                    new EncounterPhaseDefinition
                    {
                        Key =
                            "execute",

                        Name =
                            "Execute Phase",

                        StartTimeSeconds =
                            6m,

                        EndTimeSeconds =
                            12m,

                        Tags =
                        [
                            "execute"
                        ]
                    }
                ]
            };

        var finisher =
            new AbilityDefinition
            {
                Key =
                    "test-finisher",

                Name =
                    "Test Finisher",

                CastTimeSeconds =
                    0m,

                GlobalCooldownSeconds =
                    1.5m,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "test-finisher-impact",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        SchoolKey =
                            "physical",

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        MitigationType =
                            DamageMitigationTypes.None,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            250m,

                        MaximumValue =
                            250m
                    }
                ]
            };

        var basicStrike =
            new AbilityDefinition
            {
                Key =
                    "test-basic-strike",

                Name =
                    "Test Basic Strike",

                CastTimeSeconds =
                    0m,

                GlobalCooldownSeconds =
                    1.5m,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "test-basic-strike-impact",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        SchoolKey =
                            "physical",

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        MitigationType =
                            DamageMitigationTypes.None,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            120m,

                        MaximumValue =
                            120m
                    }
                ]
            };

        var player =
            new SimulationActorState
            {
                Key =
                    "player",

                Name =
                    "Encounter Test Player",

                Level =
                    20
            };

        player.InitializeHealth(
            5000m
        );

        player.AddAbility(
            finisher
        );

        player.AddAbility(
            basicStrike
        );

        var target =
            new SimulationActorState
            {
                Key =
                    "target",

                Name =
                    "Encounter Test Boss",

                Level =
                    20
            };

        // Deliberately huge. This test proves that the execute transition
        // comes from encounter time, not from this character personally
        // pushing the boss below a health percentage.
        target.InitializeHealth(
            100000m
        );

        var rotation =
            new RotationProfile
            {
                Name =
                    "Timed Execute Rotation",

                RulesetKey =
                    "development",

                ClassKey =
                    "test",

                SimulationType =
                    SimulationType.Dps,

                Entries =
                [
                    new RotationEntry
                    {
                        AbilityKey =
                            "test-finisher",

                        Priority =
                            1,

                        IsEnabled =
                            true,

                        Conditions =
                        [
                            new RotationConditionDefinition
                            {
                                ConditionType =
                                    RotationConditionTypes.EncounterPhaseActive,

                                Key =
                                    "execute"
                            }
                        ]
                    },

                    new RotationEntry
                    {
                        AbilityKey =
                            "test-basic-strike",

                        Priority =
                            2,

                        IsEnabled =
                            true
                    }
                ]
            };

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        seed,

                    DurationSeconds =
                        encounter.DurationSeconds,

                    PrimaryActorKey =
                        "player",

                    CaptureTimeline =
                        captureTimeline
                },
                encounter
            );

        context.AddActor(
            player
        );

        context.AddActor(
            target
        );

        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "development-encounter-test",

                Version =
                    "1"
            };

        var auraManager =
            new AuraManager();

        var combatRollResolver =
            new SimpleCombatRollResolver();

        var mitigationResolver =
            new RulesetDamageMitigationResolver(
                ruleset
            );

        var abilityExecutor =
            new AbilityExecutor(
                auraManager,
                combatRollResolver,
                mitigationResolver
            );

        var encounterTimelineProcessor =
            new EncounterTimelineProcessor();

        var rotationExecutor =
            new PriorityRotationExecutor(
                rotation,
                "player",
                "target",
                abilityExecutor
            );

        var engine =
            new SimulationEngine(
                [
                    encounterTimelineProcessor,
                    abilityExecutor,
                    auraManager,
                    rotationExecutor
                ]
            );

        return engine.Run(
            context
        );
    }
}
