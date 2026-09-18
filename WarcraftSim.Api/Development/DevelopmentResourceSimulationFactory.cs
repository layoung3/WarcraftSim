using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Development;

public static class DevelopmentResourceSimulationFactory
{
    public static SimulationRunResult Run(
        int seed = 12345,
        bool captureTimeline = true)
    {
        var manaBolt =
            new AbilityDefinition
            {
                Key =
                    "test-mana-bolt",

                Name =
                    "Test Mana Bolt",

                ClassKey =
                    "mage",

                CastTimeSeconds =
                    2m,

                GlobalCooldownSeconds =
                    1.5m,

                ResourceCosts =
                [
                    new AbilityResourceCost
                    {
                        ResourceKey =
                            "mana",

                        Amount =
                            40m
                    }
                ],

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "test-mana-bolt-impact",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        SchoolKey =
                            "arcane",

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
                    "Resource Test Mage",

                Level =
                    20
            };

        player.InitializeHealth(
            5000m
        );

        // Starts with 50 mana, regenerates 10 mana per second,
        // and Test Mana Bolt costs 40 mana.
        player.AddResource(
            new ResourceState(
                resourceKey: "mana",
                maximum: 100m,
                startingValue: 50m,
                regenerationPerSecond: 10m
            )
        );

        player.AddAbility(
            manaBolt
        );

        var target =
            new SimulationActorState
            {
                Key =
                    "target",

                Name =
                    "Resource Test Target",

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
                    "Resource Test Rotation",

                RulesetKey =
                    "development",

                ClassKey =
                    "mage",

                SimulationType =
                    SimulationType.Dps,

                Entries =
                [
                    new RotationEntry
                    {
                        AbilityKey =
                            "test-mana-bolt",

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
                    "development-resource-test",

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
