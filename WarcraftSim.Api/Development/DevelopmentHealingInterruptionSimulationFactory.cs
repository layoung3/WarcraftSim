using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Development;

public static class DevelopmentHealingInterruptionSimulationFactory
{
    public static SimulationRunResult Run(
        int seed = 12345,
        bool captureTimeline = true)
    {
        var efficientHeal =
            new AbilityDefinition
            {
                Key = "test-efficient-heal",
                Name = "Test Efficient Heal",
                CastTimeSeconds = 3m,
                GlobalCooldownSeconds = 1.5m,

                ResourceCosts =
                [
                    new AbilityResourceCost
                    {
                        ResourceKey = "mana",
                        Amount = 80m
                    }
                ],

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "test-efficient-heal-effect",
                        EffectType = AbilityEffectTypes.DirectHealing,
                        TargetType = AbilityTargetTypes.Friendly,
                        SchoolKey = "holy",
                        ResolutionType = CombatResolutionTypes.AlwaysHits,
                        MitigationType = DamageMitigationTypes.None,
                        CanMiss = false,
                        CanCrit = false,
                        MinimumValue = 900m,
                        MaximumValue = 900m
                    }
                ]
            };

        var emergencyHeal =
            new AbilityDefinition
            {
                Key = "test-emergency-heal",
                Name = "Test Emergency Heal",
                CastTimeSeconds = 0m,
                GlobalCooldownSeconds = 1.5m,
                CooldownSeconds = 10m,

                ResourceCosts =
                [
                    new AbilityResourceCost
                    {
                        ResourceKey = "mana",
                        Amount = 200m
                    }
                ],

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "test-emergency-heal-effect",
                        EffectType = AbilityEffectTypes.DirectHealing,
                        TargetType = AbilityTargetTypes.Friendly,
                        SchoolKey = "holy",
                        ResolutionType = CombatResolutionTypes.AlwaysHits,
                        MitigationType = DamageMitigationTypes.None,
                        CanMiss = false,
                        CanCrit = false,
                        MinimumValue = 1800m,
                        MaximumValue = 1800m
                    }
                ]
            };

        var bossStrike =
            new AbilityDefinition
            {
                Key = "test-boss-strike",
                Name = "Test Boss Strike",
                CastTimeSeconds = 0m,
                GlobalCooldownSeconds = 1.5m,
                CooldownSeconds = 2m,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "test-boss-strike-impact",
                        EffectType = AbilityEffectTypes.DirectDamage,
                        TargetType = AbilityTargetTypes.Enemy,
                        SchoolKey = "physical",
                        ResolutionType = CombatResolutionTypes.Melee,
                        MitigationType = DamageMitigationTypes.Armor,
                        CanMiss = false,
                        CanCrit = false,
                        MinimumValue = 1500m,
                        MaximumValue = 1500m
                    }
                ]
            };

        var healer =
            new SimulationActorState
            {
                Key = "healer",
                Name = "Emergency Test Healer",
                Level = 20
            };

        healer.InitializeHealth(
            3500m
        );

        healer.ConfigureActionTiming(
            initialActionDelaySeconds: 0m,
            inputDelaySeconds: 0.1m
        );

        healer.AddResource(
            new ResourceState(
                "mana",
                1200m,
                1200m,
                8m
            )
        );

        healer.AddAbility(
            efficientHeal
        );

        healer.AddAbility(
            emergencyHeal
        );

        var tank =
            new SimulationActorState
            {
                Key = "tank",
                Name = "Emergency Test Tank",
                Level = 20
            };

        tank.InitializeHealth(
            5000m
        );

        tank.Stats.Set(
            "armor",
            1500m
        );

        var boss =
            new SimulationActorState
            {
                Key = "boss",
                Name = "Emergency Test Boss",
                Level = 23
            };

        boss.InitializeHealth(
            100000m
        );

        boss.AddAbility(
            bossStrike
        );

        var healerRotation =
            new RotationProfile
            {
                Name = "Emergency Healing Rotation",
                RulesetKey = "development",
                ClassKey = "healer",
                SimulationType = SimulationType.Healing,

                Entries =
                [
                    new RotationEntry
                    {
                        AbilityKey = "test-emergency-heal",
                        Priority = 1,
                        InterruptCurrentCast = true,

                        Conditions =
                        [
                            new RotationConditionDefinition
                            {
                                ConditionType =
                                    RotationConditionTypes.TargetHealthPercent,

                                ComparisonOperator =
                                    RotationComparisonOperators.LessThanOrEqual,

                                Value = 50m
                            }
                        ]
                    },

                    new RotationEntry
                    {
                        AbilityKey = "test-efficient-heal",
                        Priority = 2,

                        Conditions =
                        [
                            new RotationConditionDefinition
                            {
                                ConditionType =
                                    RotationConditionTypes.TargetHealthPercent,

                                ComparisonOperator =
                                    RotationComparisonOperators.LessThanOrEqual,

                                Value = 80m
                            }
                        ]
                    }
                ]
            };

        var bossRotation =
            new RotationProfile
            {
                Name = "Emergency Test Boss Rotation",
                RulesetKey = "development",
                ClassKey = "boss",
                SimulationType = SimulationType.Tank,

                Entries =
                [
                    new RotationEntry
                    {
                        AbilityKey = "test-boss-strike",
                        Priority = 1
                    }
                ]
            };

        var ruleset =
            new CombatRulesetDefinition
            {
                RulesetKey = "development-healing-interrupt",
                Version = "1",

                RollRules =
                {
                    [CombatResolutionTypes.Melee] =
                        new CombatRollRuleDefinition
                        {
                            ResolutionType = CombatResolutionTypes.Melee,
                            BaseHitChancePercent = 100m,
                            BaseCriticalChancePercent = 0m,
                            CriticalMultiplier = 2m
                        }
                },

                MitigationRules =
                {
                    [DamageMitigationTypes.Armor] =
                        new DamageMitigationRuleDefinition
                        {
                            MitigationType = DamageMitigationTypes.Armor,
                            FormulaType =
                                DamageMitigationFormulaTypes.RationalLevelScaled,
                            DefenseStatKey = "armor",
                            BaseConstant = 400m,
                            PerAttackerLevelConstant = 85m,
                            MaximumReductionPercent = 75m
                        }
                }
            };

        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed = seed,
                    DurationSeconds = 12m,
                    PrimaryActorKey = "healer",
                    CaptureTimeline = captureTimeline
                }
            );

        context.AddActor(
            healer
        );

        context.AddActor(
            tank
        );

        context.AddActor(
            boss
        );

        var auraManager =
            new AuraManager();

        var combatRollResolver =
            new RulesetCombatRollResolver(
                ruleset
            );

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

        var bossRotationExecutor =
            new PriorityRotationExecutor(
                bossRotation,
                "boss",
                "tank",
                abilityExecutor
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
                    abilityExecutor,
                    auraManager,
                    bossRotationExecutor,
                    healerRotationExecutor
                ]
            );

        return engine.Run(
            context
        );
    }
}
