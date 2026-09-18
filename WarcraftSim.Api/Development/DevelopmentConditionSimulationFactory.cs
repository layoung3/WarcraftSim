using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Development;

public static class DevelopmentConditionSimulationFactory
{
    public static SimulationRunResult Run(
        int seed = 12345,
        bool captureTimeline = true)
    {
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
                    "Condition Test Player",

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
                    "Condition Test Target",

                Level =
                    20
            };

        target.InitializeHealth(
            1000m
        );

        var rotation =
            new RotationProfile
            {
                Name =
                    "Conditional Priority Test",

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
                                    RotationConditionTypes.TargetHealthPercent,

                                ComparisonOperator =
                                    RotationComparisonOperators.LessThanOrEqual,

                                Value =
                                    50m
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
                        12m,

                    PrimaryActorKey =
                        "player",

                    CaptureTimeline =
                        captureTimeline
                }
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
                    "development-condition-test",

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
