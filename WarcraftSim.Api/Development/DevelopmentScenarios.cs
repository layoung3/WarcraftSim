using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Analysis;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Development;

public static class DevelopmentScenarios
{
    public static SimulationRunResult RunCombatSmoke(
        int seed = 12345,
        bool captureTimeline = true)
    {
        var strike =
            new AbilityDefinition
            {
                Key =
                    "dev-smoke-strike",

                Name =
                    "Dev Smoke Strike",

                CastTimeSeconds =
                    0m,

                GlobalCooldownSeconds =
                    1.5m,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "dev-smoke-strike-impact",

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
                    "Dev Smoke Player",

                TeamKey =
                    "raid",

                AssignedRole =
                    SimulationType.Dps,

                Level =
                    20
            };

        player.InitializeHealth(
            5000m
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
                    "Dev Smoke Target",

                TeamKey =
                    "enemy",

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
                    "Dev Smoke Rotation",

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
                            "dev-smoke-strike",

                        Priority =
                            1
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
                        6m,

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
                    "development-smoke",

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

    public static HealingThroughputResult RunHealingThroughput(
        int seed = 12345,
        bool captureTimeline = true)
    {
        var burstHeal =
            new AbilityDefinition
            {
                Key =
                    "dev-throughput-burst-heal",

                Name =
                    "Dev Throughput Burst Heal",

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
                            "dev-throughput-burst-heal-effect",

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
                    "dev-throughput-efficient-heal",

                Name =
                    "Dev Throughput Efficient Heal",

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
                            "dev-throughput-efficient-heal-effect",

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
                    "Dev Throughput Healer",

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

        var target =
            new SimulationActorState
            {
                Key =
                    "throughput-target",

                Name =
                    "Dev Throughput Target",

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
                    "Dev Healing Throughput Rotation",

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
                            "dev-throughput-burst-heal",

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
                            "dev-throughput-efficient-heal",

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

    public static SimulationRunResult RunRaidHealing(
        int seed = 12345,
        bool captureTimeline = true)
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Dev Raid Healing",

                RulesetKey =
                    "development",

                DurationSeconds =
                    13m,

                DamageEvents =
                [
                    new EncounterDamageEventDefinition
                    {
                        Key =
                            "healer-splash",

                        Name =
                            "Healer Splash",

                        TimeSeconds =
                            6m,

                        SourceActorKey =
                            "boss",

                        TargetActorKey =
                            "healer",

                        Amount =
                            1200m,

                        SchoolKey =
                            "fire",

                        MitigationType =
                            DamageMitigationTypes.Resistance
                    }
                ],

                DamagePatterns =
                [
                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "tank-swing",

                        Name =
                            "Tank Swing",

                        StartTimeSeconds =
                            1.5m,

                        EndTimeSeconds =
                            9.5m,

                        IntervalSeconds =
                            2m,

                        SourceActorKey =
                            "boss",

                        TargetActorKey =
                            "tank",

                        Amount =
                            2500m,

                        SchoolKey =
                            "physical",

                        MitigationType =
                            DamageMitigationTypes.Armor
                    },

                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "shadow-raid-pulse",

                        Name =
                            "Shadow Raid Pulse",

                        StartTimeSeconds =
                            2m,

                        EndTimeSeconds =
                            10m,

                        IntervalSeconds =
                            4m,

                        SourceActorKey =
                            "boss",

                        TargetActorKey =
                            "dps-1",

                        Amount =
                            1500m,

                        SchoolKey =
                            "shadow",

                        MitigationType =
                            DamageMitigationTypes.Resistance
                    },

                    new EncounterDamagePatternDefinition
                    {
                        Key =
                            "fire-raid-pulse",

                        Name =
                            "Fire Raid Pulse",

                        StartTimeSeconds =
                            4m,

                        EndTimeSeconds =
                            8m,

                        IntervalSeconds =
                            4m,

                        SourceActorKey =
                            "boss",

                        TargetActorKey =
                            "dps-2",

                        Amount =
                            1400m,

                        SchoolKey =
                            "fire",

                        MitigationType =
                            DamageMitigationTypes.Resistance
                    }
                ]
            };

        var emergencyTankHeal =
            new AbilityDefinition
            {
                Key =
                    "dev-emergency-tank-heal",

                Name =
                    "Dev Emergency Tank Heal",

                CastTimeSeconds =
                    0m,

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
                            180m
                    }
                ],

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "dev-emergency-tank-heal-effect",

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
                            1800m,

                        MaximumValue =
                            1800m
                    }
                ]
            };

        var raidHeal =
            new AbilityDefinition
            {
                Key =
                    "dev-raid-heal",

                Name =
                    "Dev Raid Heal",

                CastTimeSeconds =
                    1.5m,

                GlobalCooldownSeconds =
                    1.5m,

                ResourceCosts =
                [
                    new AbilityResourceCost
                    {
                        ResourceKey =
                            "mana",

                        Amount =
                            80m
                    }
                ],

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "dev-raid-heal-effect",

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
                            900m,

                        MaximumValue =
                            900m
                    }
                ]
            };

        var healer =
            new SimulationActorState
            {
                Key =
                    "healer",

                Name =
                    "Dev Raid Healer",

                TeamKey =
                    "raid",

                AssignedRole =
                    SimulationType.Healing,

                Level =
                    20
            };

        healer.InitializeHealth(
            4000m
        );

        healer.ConfigureActionTiming(
            initialActionDelaySeconds: 0m,
            inputDelaySeconds: 0.1m
        );

        healer.AddResource(
            new ResourceState(
                resourceKey: "mana",
                maximum: 1600m,
                startingValue: 1600m,
                regenerationPerSecond: 10m
            )
        );

        healer.Stats.Set(
            "fire-resistance",
            40m
        );

        healer.AddAbility(
            emergencyTankHeal
        );

        healer.AddAbility(
            raidHeal
        );

        var tank =
            new SimulationActorState
            {
                Key =
                    "tank",

                Name =
                    "Dev Tank",

                TeamKey =
                    "raid",

                AssignedRole =
                    SimulationType.Tank,

                Level =
                    20
            };

        tank.InitializeHealth(
            6000m
        );

        tank.Stats.Set(
            "armor",
            1800m
        );

        var dpsOne =
            new SimulationActorState
            {
                Key =
                    "dps-1",

                Name =
                    "Dev DPS One",

                TeamKey =
                    "raid",

                AssignedRole =
                    SimulationType.Dps,

                Level =
                    20
            };

        dpsOne.InitializeHealth(
            3200m
        );

        dpsOne.Stats.Set(
            "shadow-resistance",
            60m
        );

        var dpsTwo =
            new SimulationActorState
            {
                Key =
                    "dps-2",

                Name =
                    "Dev DPS Two",

                TeamKey =
                    "raid",

                AssignedRole =
                    SimulationType.Dps,

                Level =
                    20
            };

        dpsTwo.InitializeHealth(
            3200m
        );

        dpsTwo.Stats.Set(
            "fire-resistance",
            50m
        );

        var boss =
            new SimulationActorState
            {
                Key =
                    "boss",

                Name =
                    "Dev Boss",

                TeamKey =
                    "enemy",

                Level =
                    23
            };

        boss.InitializeHealth(
            100000m
        );

        var healerRotation =
            new RotationProfile
            {
                Name =
                    "Dev Raid Healing Rotation",

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
                            "dev-emergency-tank-heal",

                        Priority =
                            1,

                        InterruptCurrentCast =
                            true,

                        Target =
                            new RotationTargetDefinition
                            {
                                Mode =
                                    RotationTargetSelectionModes.LowestHealthAlly,

                                IncludeSelf =
                                    false,

                                AllowedRoles =
                                [
                                    SimulationType.Tank
                                ]
                            },

                        Conditions =
                        [
                            new RotationConditionDefinition
                            {
                                ConditionType =
                                    RotationConditionTypes.TargetHealthPercent,

                                ComparisonOperator =
                                    RotationComparisonOperators.LessThanOrEqual,

                                Value =
                                    45m
                            }
                        ]
                    },

                    new RotationEntry
                    {
                        AbilityKey =
                            "dev-raid-heal",

                        Priority =
                            2,

                        Target =
                            new RotationTargetDefinition
                            {
                                Mode =
                                    RotationTargetSelectionModes.LowestHealthAlly,

                                IncludeSelf =
                                    true
                            },

                        Conditions =
                        [
                            new RotationConditionDefinition
                            {
                                ConditionType =
                                    RotationConditionTypes.TargetHealthPercent,

                                ComparisonOperator =
                                    RotationComparisonOperators.LessThanOrEqual,

                                Value =
                                    80m
                            }
                        ]
                    }
                ]
            };

        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "development-raid-healing",

                Version =
                    "2",

                MitigationRules =
                {
                    [DamageMitigationTypes.Armor] =
                        new DamageMitigationRuleDefinition
                        {
                            MitigationType =
                                DamageMitigationTypes.Armor,

                            FormulaType =
                                DamageMitigationFormulaTypes.RationalLevelScaled,

                            DefenseStatKey =
                                "armor",

                            BaseConstant =
                                400m,

                            PerAttackerLevelConstant =
                                85m,

                            MaximumReductionPercent =
                                75m
                        },

                    [DamageMitigationTypes.Resistance] =
                        new DamageMitigationRuleDefinition
                        {
                            MitigationType =
                                DamageMitigationTypes.Resistance,

                            FormulaType =
                                DamageMitigationFormulaTypes.ResistanceAverage,

                            DefenseStatKeyFormat =
                                "{school}-resistance",

                            DefensePerAttackerLevel =
                                5m,

                            MaximumReductionPercent =
                                75m
                        }
                }
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
                        "healer",

                    CaptureTimeline =
                        captureTimeline
                },
                encounter
            );

        context.AddActor(
            healer
        );

        context.AddActor(
            tank
        );

        context.AddActor(
            dpsOne
        );

        context.AddActor(
            dpsTwo
        );

        context.AddActor(
            boss
        );

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

        var scriptedDamageProcessor =
            new ScriptedEncounterDamageProcessor(
                mitigationResolver
            );

        var healerRotationExecutor =
            new PriorityRotationExecutor(
                healerRotation,
                "healer",
                "tank",
                abilityExecutor,
                reactToTargetStateChanges: true
            );

        var engine =
            new SimulationEngine(
                [
                    encounterTimelineProcessor,
                    scriptedDamageProcessor,
                    abilityExecutor,
                    auraManager,
                    healerRotationExecutor
                ]
            );

        return engine.Run(
            context
        );
    }
}
