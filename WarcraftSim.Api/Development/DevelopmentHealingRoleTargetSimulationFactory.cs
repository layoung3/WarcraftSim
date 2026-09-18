using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Development;

public static class DevelopmentHealingRoleTargetSimulationFactory
{
    public static SimulationRunResult Run(
        int seed = 12345,
        bool captureTimeline = true)
    {
        var tankHeal =
            new AbilityDefinition
            {
                Key =
                    "test-role-tank-heal",

                Name =
                    "Test Role Tank Heal",

                CastTimeSeconds =
                    0m,

                GlobalCooldownSeconds =
                    1.5m,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "test-role-tank-heal-effect",

                        EffectType =
                            AbilityEffectTypes.DirectHealing,

                        TargetType =
                            AbilityTargetTypes.Friendly,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        MitigationType =
                            DamageMitigationTypes.None,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            1000m,

                        MaximumValue =
                            1000m
                    }
                ]
            };

        var raidHeal =
            new AbilityDefinition
            {
                Key =
                    "test-role-raid-heal",

                Name =
                    "Test Role Raid Heal",

                CastTimeSeconds =
                    0m,

                GlobalCooldownSeconds =
                    1.5m,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "test-role-raid-heal-effect",

                        EffectType =
                            AbilityEffectTypes.DirectHealing,

                        TargetType =
                            AbilityTargetTypes.Friendly,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        MitigationType =
                            DamageMitigationTypes.None,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            800m,

                        MaximumValue =
                            800m
                    }
                ]
            };

        var healer =
            new SimulationActorState
            {
                Key =
                    "healer",

                Name =
                    "Role Test Healer",

                TeamKey =
                    "raid",

                AssignedRole =
                    SimulationType.Healing,

                Level =
                    20
            };

        healer.InitializeHealth(
            3500m
        );

        healer.AddAbility(
            tankHeal
        );

        healer.AddAbility(
            raidHeal
        );

        var tankOne =
            new SimulationActorState
            {
                Key =
                    "tank-1",

                Name =
                    "Tank One",

                TeamKey =
                    "raid",

                AssignedRole =
                    SimulationType.Tank,

                Level =
                    20
            };

        tankOne.InitializeHealth(
            maximumHealth: 5000m,
            startingHealth: 2000m
        );

        var tankTwo =
            new SimulationActorState
            {
                Key =
                    "tank-2",

                Name =
                    "Tank Two",

                TeamKey =
                    "raid",

                AssignedRole =
                    SimulationType.Tank,

                Level =
                    20
            };

        tankTwo.InitializeHealth(
            maximumHealth: 5000m,
            startingHealth: 3500m
        );

        var dps =
            new SimulationActorState
            {
                Key =
                    "dps-1",

                Name =
                    "Injured DPS",

                TeamKey =
                    "raid",

                AssignedRole =
                    SimulationType.Dps,

                Level =
                    20
            };

        dps.InitializeHealth(
            maximumHealth: 3000m,
            startingHealth: 600m
        );

        var rotation =
            new RotationProfile
            {
                Name =
                    "Role-Aware Healing Test",

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
                            "test-role-tank-heal",

                        Priority =
                            1,

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
                                    50m
                            }
                        ]
                    },

                    new RotationEntry
                    {
                        AbilityKey =
                            "test-role-raid-heal",

                        Priority =
                            2,

                        Target =
                            new RotationTargetDefinition
                            {
                                Mode =
                                    RotationTargetSelectionModes.LowestHealthAlly,

                                IncludeSelf =
                                    false
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
                        6m,

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
            tankOne
        );

        context.AddActor(
            tankTwo
        );

        context.AddActor(
            dps
        );

        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey =
                    "development-role-targets",

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
                "tank-1",
                abilityExecutor,
                reactToTargetStateChanges: true
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
