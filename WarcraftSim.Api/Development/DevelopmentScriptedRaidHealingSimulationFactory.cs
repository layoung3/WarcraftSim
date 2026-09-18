using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Development;

public static class DevelopmentScriptedRaidHealingSimulationFactory
{
    public static SimulationRunResult Run(
        int seed = 12345,
        bool captureTimeline = true)
    {
        var encounter =
            new EncounterProfile
            {
                Name =
                    "Scripted Raid Healing Test",

                RulesetKey =
                    "development",

                DurationSeconds =
                    12m,

                DamageEvents =
                [
                    new EncounterDamageEventDefinition
                    {
                        Key = "raid-burst-dps-1",
                        Name = "Raid Burst on DPS One",
                        TimeSeconds = 1m,
                        SourceActorKey = "boss",
                        TargetActorKey = "dps-1",
                        Amount = 1800m,
                        SchoolKey = "shadow"
                    },

                    new EncounterDamageEventDefinition
                    {
                        Key = "tank-spike-1",
                        Name = "Tank Spike One",
                        TimeSeconds = 2.5m,
                        SourceActorKey = "boss",
                        TargetActorKey = "tank",
                        Amount = 3000m,
                        SchoolKey = "physical"
                    },

                    new EncounterDamageEventDefinition
                    {
                        Key = "raid-burst-dps-2",
                        Name = "Raid Burst on DPS Two",
                        TimeSeconds = 4m,
                        SourceActorKey = "boss",
                        TargetActorKey = "dps-2",
                        Amount = 1700m,
                        SchoolKey = "fire"
                    },

                    new EncounterDamageEventDefinition
                    {
                        Key = "healer-splash",
                        Name = "Healer Splash",
                        TimeSeconds = 5.5m,
                        SourceActorKey = "boss",
                        TargetActorKey = "healer",
                        Amount = 1200m,
                        SchoolKey = "fire"
                    },

                    new EncounterDamageEventDefinition
                    {
                        Key = "tank-spike-2",
                        Name = "Tank Spike Two",
                        TimeSeconds = 7m,
                        SourceActorKey = "boss",
                        TargetActorKey = "tank",
                        Amount = 2600m,
                        SchoolKey = "physical"
                    },

                    new EncounterDamageEventDefinition
                    {
                        Key = "raid-burst-dps-1-2",
                        Name = "Second Raid Burst on DPS One",
                        TimeSeconds = 9m,
                        SourceActorKey = "boss",
                        TargetActorKey = "dps-1",
                        Amount = 1500m,
                        SchoolKey = "shadow"
                    }
                ]
            };

        var emergencyTankHeal =
            new AbilityDefinition
            {
                Key =
                    "test-scripted-emergency-tank-heal",

                Name =
                    "Test Scripted Emergency Tank Heal",

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
                            "test-scripted-emergency-tank-heal-effect",

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
                    "test-scripted-raid-heal",

                Name =
                    "Test Scripted Raid Heal",

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
                            "test-scripted-raid-heal-effect",

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
                    "Scripted Raid Healer",

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
                    "Scripted Tank",

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

        var dpsOne =
            new SimulationActorState
            {
                Key =
                    "dps-1",

                Name =
                    "Scripted DPS One",

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

        var dpsTwo =
            new SimulationActorState
            {
                Key =
                    "dps-2",

                Name =
                    "Scripted DPS Two",

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

        var boss =
            new SimulationActorState
            {
                Key =
                    "boss",

                Name =
                    "Scripted Boss",

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
                    "Scripted Raid Healing Rotation",

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
                            "test-scripted-emergency-tank-heal",

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
                            "test-scripted-raid-heal",

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

        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "development-scripted-raid-healing",

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

        var scriptedDamageProcessor =
            new ScriptedEncounterDamageProcessor();

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
