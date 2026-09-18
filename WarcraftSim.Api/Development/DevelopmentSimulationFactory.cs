using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Development;

public static class DevelopmentSimulationFactory
{
    public static SimulationRunResult RunPrioritySimulation(
        int seed,
        bool captureTimeline)
    {
        var testFireball =
            new AbilityDefinition
            {
                Key = "test-fireball",
                Name = "Test Fireball",
                ClassKey = "mage",
                CastTimeSeconds = 2m,
                GlobalCooldownSeconds = 1.5m,

                ResourceCosts =
                [
                    new AbilityResourceCost
                    {
                        ResourceKey = "mana",
                        Amount = 10m
                    }
                ],

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "test-fireball-impact",
                        EffectType =
                            AbilityEffectTypes.DirectDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        SchoolKey = "fire",
                        ResolutionType =
                            CombatResolutionTypes.Spell,
                        MitigationType =
                            DamageMitigationTypes.Resistance,
                        CanMiss = true,
                        CanCrit = true,
                        MinimumValue = 100m,
                        MaximumValue = 120m,
                        TravelTimeSeconds = 1.5m
                    },

                    new AbilityEffectDefinition
                    {
                        Key = "test-fireball-burning",
                        EffectType =
                            AbilityEffectTypes.PeriodicDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        SchoolKey = "fire",
                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,
                        MitigationType =
                            DamageMitigationTypes.Resistance,
                        CanMiss = false,
                        CanCrit = false,
                        MinimumValue = 20m,
                        MaximumValue = 20m,
                        DurationSeconds = 6m,
                        TickIntervalSeconds = 2m,
                        TravelTimeSeconds = 1.5m,
                        AuraKey = "test-burning",
                        AuraStackingMode =
                            AuraStackingMode.Refresh,
                        MaxStacks = 1,
                        DependsOnEffectKey =
                            "test-fireball-impact",
                        DependencyCondition =
                            EffectDependencyConditions.Landed
                    }
                ]
            };

        var testBossStrike =
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
                        EffectType =
                            AbilityEffectTypes.DirectDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        SchoolKey = "physical",
                        ResolutionType =
                            CombatResolutionTypes.Melee,
                        MitigationType =
                            DamageMitigationTypes.Armor,
                        CanMiss = false,
                        CanCrit = false,
                        MinimumValue = 300m,
                        MaximumValue = 300m
                    }
                ]
            };

        var player =
            new SimulationActorState
            {
                Key = "player",
                Name = "Test Mage",
                Level = 20
            };

        player.InitializeHealth(
            5000m
        );

        player.Stats.Set(
            "spell-hit-percent",
            8m
        );

        player.Stats.Set(
            "spell-crit-percent",
            25m
        );

        player.Stats.Set(
            "armor",
            1200m
        );

        player.AddResource(
            new ResourceState(
                "mana",
                1000m,
                1000m
            )
        );

        player.AddAbility(
            testFireball
        );

        var boss =
            new SimulationActorState
            {
                Key = "boss",
                Name = "Training Boss",
                Level = 23
            };

        boss.InitializeHealth(
            100000m
        );

        boss.Stats.Set(
            "spell-avoidance-percent",
            3m
        );

        boss.Stats.Set(
            "spell-crit-suppression-percent",
            2m
        );

        boss.Stats.Set(
            "fire-resistance",
            50m
        );

        boss.AddAbility(
            testBossStrike
        );

        var ruleset =
            BuildDevelopmentRuleset();

        var playerRotation =
            new RotationProfile
            {
                Name = "Test Mage Rotation",
                RulesetKey = "development",
                ClassKey = "mage",
                SimulationType =
                    SimulationType.Dps,

                Entries =
                [
                    new RotationEntry
                    {
                        AbilityKey = "test-fireball",
                        Priority = 1,
                        IsEnabled = true
                    }
                ]
            };

        var bossRotation =
            new RotationProfile
            {
                Name = "Test Boss Rotation",
                RulesetKey = "development",
                ClassKey = "boss",
                SimulationType =
                    SimulationType.Tank,

                Entries =
                [
                    new RotationEntry
                    {
                        AbilityKey = "test-boss-strike",
                        Priority = 1,
                        IsEnabled = true
                    }
                ]
            };

        var options =
            new SimulationRunOptions
            {
                Seed = seed,
                DurationSeconds = 12m,
                PrimaryActorKey = "player",
                CaptureTimeline = captureTimeline
            };

        var context =
            new SimulationContext(
                options
            );

        context.AddActor(
            player
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

        var playerRotationExecutor =
            new PriorityRotationExecutor(
                playerRotation,
                "player",
                "boss",
                abilityExecutor
            );

        var bossRotationExecutor =
            new PriorityRotationExecutor(
                bossRotation,
                "boss",
                "player",
                abilityExecutor
            );

        var engine =
            new SimulationEngine(
                [
                    abilityExecutor,
                    auraManager,
                    playerRotationExecutor,
                    bossRotationExecutor
                ]
            );

        return engine.Run(
            context
        );
    }

    private static CombatRulesetDefinition
        BuildDevelopmentRuleset()
    {
        return new CombatRulesetDefinition
        {
            RulesetKey =
                "development",

            Version =
                "2",

            RollRules =
            {
                [CombatResolutionTypes.Spell] =
                    new CombatRollRuleDefinition
                    {
                        ResolutionType =
                            CombatResolutionTypes.Spell,

                        BaseHitChancePercent =
                            80m,

                        HitChanceStatKey =
                            "spell-hit-percent",

                        TargetAvoidanceStatKey =
                            "spell-avoidance-percent",

                        HitPenaltyPerHigherTargetLevelPercent =
                            2m,

                        BaseCriticalChancePercent =
                            5m,

                        CriticalChanceStatKey =
                            "spell-crit-percent",

                        TargetCriticalSuppressionStatKey =
                            "spell-crit-suppression-percent",

                        CriticalMultiplier =
                            2m
                    },

                [CombatResolutionTypes.Melee] =
                    new CombatRollRuleDefinition
                    {
                        ResolutionType =
                            CombatResolutionTypes.Melee,

                        BaseHitChancePercent =
                            100m,

                        BaseCriticalChancePercent =
                            0m,

                        CriticalMultiplier =
                            2m
                    }
            },

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
    }
}
