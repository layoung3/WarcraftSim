using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Development;

public static class DevelopmentActionTimingSimulationFactory
{
    public static SimulationRunResult Run(
        int seed = 12345,
        bool captureTimeline = true)
    {
        var strike =
            new AbilityDefinition
            {
                Key =
                    "test-timing-strike",

                Name =
                    "Test Timing Strike",

                CastTimeSeconds =
                    0m,

                GlobalCooldownSeconds =
                    1.5m,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "test-timing-strike-impact",

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
                            100m,

                        MaximumValue =
                            100m
                    }
                ]
            };

        var player =
            new SimulationActorState
            {
                Key =
                    "player",

                Name =
                    "Timing Test Player",

                Level =
                    20
            };

        player.InitializeHealth(
            5000m
        );

        // Example user settings:
        // - Wait 2 seconds before beginning actions.
        // - Add 0.1 seconds after the action lock/GCD before the next input.
        player.ConfigureActionTiming(
            initialActionDelaySeconds: 2m,
            inputDelaySeconds: 0.1m
        );

        player.AddAbility(
            strike
        );

        var target =
            new SimulationActorState
            {
                Key =
                    "target",

                Name =
                    "Timing Test Target",

                Level =
                    20
            };

        target.InitializeHealth(
            100000m
        );

        var rotation =
            new RotationProfile
            {
                Name =
                    "Action Timing Test",

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
                            "test-timing-strike",

                        Priority =
                            1,

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
                    "development-action-timing",

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
