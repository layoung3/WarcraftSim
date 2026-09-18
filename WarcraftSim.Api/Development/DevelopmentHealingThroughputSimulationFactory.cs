using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Analysis;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Development;

public static class DevelopmentHealingThroughputSimulationFactory
{
    public static HealingThroughputResult Run(
        int seed = 12345,
        bool captureTimeline = true)
    {
        var burstHeal =
            new AbilityDefinition
            {
                Key =
                    "test-throughput-burst-heal",

                Name =
                    "Test Throughput Burst Heal",

                CastTimeSeconds =
                    2m,

                GlobalCooldownSeconds =
                    1.5m,

                CooldownSeconds =
                    8m,

                ResourceCosts =
                [
                    new AbilityResourceCost
                    {
                        ResourceKey =
                            "mana",

                        Amount =
                            220m
                    }
                ],

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "test-throughput-burst-heal-effect",

                        EffectType =
                            AbilityEffectTypes.DirectHealing,

                        TargetType =
                            AbilityTargetTypes.Friendly,

                        SchoolKey =
                            "holy",

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        MitigationType =
                            DamageMitigationTypes.None,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            1600m,

                        MaximumValue =
                            1600m
                    }
                ]
            };

        var efficientHeal =
            new AbilityDefinition
            {
                Key =
                    "test-throughput-efficient-heal",

                Name =
                    "Test Throughput Efficient Heal",

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
                            100m
                    }
                ],

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "test-throughput-efficient-heal-effect",

                        EffectType =
                            AbilityEffectTypes.DirectHealing,

                        TargetType =
                            AbilityTargetTypes.Friendly,

                        SchoolKey =
                            "holy",

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        MitigationType =
                            DamageMitigationTypes.None,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            700m,

                        MaximumValue =
                            700m
                    }
                ]
            };

        var healer =
            new SimulationActorState
            {
                Key =
                    "healer",

                Name =
                    "Throughput Test Healer",

                TeamKey =
                    "raid",

                AssignedRole =
                    SimulationType.Healing,

                Level =
                    20
            };

        healer.InitializeHealth(
            5000m
        );

        healer.ConfigureActionTiming(
            initialActionDelaySeconds: 0m,
            inputDelaySeconds: 0.1m
        );

        healer.AddResource(
            new ResourceState(
                resourceKey: "mana",
                maximum: 1000m,
                startingValue: 1000m,
                regenerationPerSecond: 5m
            )
        );

        healer.AddAbility(
            burstHeal
        );

        healer.AddAbility(
            efficientHeal
        );

        // Huge missing-health pool keeps the benchmark focused on spell
        // throughput and mana longevity instead of target health capping.
        var target =
            new SimulationActorState
            {
                Key =
                    "throughput-target",

                Name =
                    "Throughput Healing Target",

                TeamKey =
                    "raid",

                AssignedRole =
                    SimulationType.Tank,

                Level =
                    20
            };

        target.InitializeHealth(
            maximumHealth: 1000000m,
            startingHealth: 1m
        );

        var rotation =
            new RotationProfile
            {
                Name =
                    "Healing Throughput Priority",

                RulesetKey =
                    "development",

                ClassKey =
                    "healer",

                SimulationType =
                    SimulationType.Healing,

                Entries =
                [
                    new RotationEntry
                    {
                        AbilityKey =
                            "test-throughput-burst-heal",

                        Priority =
                            1,

                        Target =
                            new RotationTargetDefinition
                            {
                                Mode =
                                    RotationTargetSelectionModes.Fixed,

                                ActorKey =
                                    "throughput-target"
                            }
                    },

                    new RotationEntry
                    {
                        AbilityKey =
                            "test-throughput-efficient-heal",

                        Priority =
                            2,

                        Target =
                            new RotationTargetDefinition
                            {
                                Mode =
                                    RotationTargetSelectionModes.Fixed,

                                ActorKey =
                                    "throughput-target"
                            }
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
                        60m,

                    PrimaryActorKey =
                        "healer",

                    CaptureTimeline =
                        captureTimeline
                }
            );

        context.AddActor(
            healer
        );

        context.AddActor(
            target
        );

        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "development-healing-throughput",

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
                "healer",
                "throughput-target",
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

        var simulation =
            engine.Run(
                context
            );

        return new HealingThroughputResult
        {
            Metrics =
                HealingThroughputAnalyzer.Analyze(
                    simulation,
                    "healer",
                    "mana"
                ),

            Simulation =
                simulation
        };
    }
}
