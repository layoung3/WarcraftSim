using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Development;

public static class DevelopmentHealingTargetSimulationFactory
{
    public static SimulationRunResult Run(
        int seed = 12345,
        bool captureTimeline = true)
    {
        var raidHeal = new AbilityDefinition
        {
            Key = "test-raid-heal",
            Name = "Test Raid Heal",
            CastTimeSeconds = 1.5m,
            GlobalCooldownSeconds = 1.5m,
            ResourceCosts =
            [
                new AbilityResourceCost
                {
                    ResourceKey = "mana",
                    Amount = 60m
                }
            ],
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = "test-raid-heal-effect",
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

        var emergencyTankHeal = new AbilityDefinition
        {
            Key = "test-emergency-tank-heal",
            Name = "Test Emergency Tank Heal",
            CastTimeSeconds = 0m,
            GlobalCooldownSeconds = 1.5m,
            CooldownSeconds = 10m,
            ResourceCosts =
            [
                new AbilityResourceCost
                {
                    ResourceKey = "mana",
                    Amount = 150m
                }
            ],
            Effects =
            [
                new AbilityEffectDefinition
                {
                    Key = "test-emergency-tank-heal-effect",
                    EffectType = AbilityEffectTypes.DirectHealing,
                    TargetType = AbilityTargetTypes.Friendly,
                    SchoolKey = "holy",
                    ResolutionType = CombatResolutionTypes.AlwaysHits,
                    MitigationType = DamageMitigationTypes.None,
                    CanMiss = false,
                    CanCrit = false,
                    MinimumValue = 1600m,
                    MaximumValue = 1600m
                }
            ]
        };

        var healer = new SimulationActorState
        {
            Key = "healer",
            Name = "Target Selection Healer",
            TeamKey = "raid",
            Level = 20
        };

        healer.InitializeHealth(3500m);
        healer.AddResource(new ResourceState("mana", 1200m, 1200m, 8m));
        healer.AddAbility(raidHeal);
        healer.AddAbility(emergencyTankHeal);

        var tank = new SimulationActorState
        {
            Key = "tank",
            Name = "Assigned Tank",
            TeamKey = "raid",
            Level = 20
        };

        tank.InitializeHealth(
            maximumHealth: 5000m,
            startingHealth: 3000m);

        var raidMember = new SimulationActorState
        {
            Key = "raid-member-1",
            Name = "Injured Raid Member",
            TeamKey = "raid",
            Level = 20
        };

        raidMember.InitializeHealth(
            maximumHealth: 3000m,
            startingHealth: 900m);

        var boss = new SimulationActorState
        {
            Key = "boss",
            Name = "Target Selection Boss",
            TeamKey = "enemy",
            Level = 23
        };

        boss.InitializeHealth(100000m);

        var rotation = new RotationProfile
        {
            Name = "Dynamic Healing Target Test",
            RulesetKey = "development",
            ClassKey = "healer",
            SimulationType = SimulationType.Healing,
            Entries =
            [
                new RotationEntry
                {
                    AbilityKey = "test-emergency-tank-heal",
                    Priority = 1,
                    InterruptCurrentCast = true,
                    Target = new RotationTargetDefinition
                    {
                        Mode = RotationTargetSelectionModes.Fixed,
                        ActorKey = "tank"
                    },
                    Conditions =
                    [
                        new RotationConditionDefinition
                        {
                            ConditionType =
                                RotationConditionTypes.TargetHealthPercent,
                            ComparisonOperator =
                                RotationComparisonOperators.LessThanOrEqual,
                            Value = 35m
                        }
                    ]
                },
                new RotationEntry
                {
                    AbilityKey = "test-raid-heal",
                    Priority = 2,
                    Target = new RotationTargetDefinition
                    {
                        Mode =
                            RotationTargetSelectionModes.LowestHealthAlly,
                        IncludeSelf = true
                    },
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

        var context = new SimulationContext(
            new SimulationRunOptions
            {
                Seed = seed,
                DurationSeconds = 8m,
                PrimaryActorKey = "healer",
                CaptureTimeline = captureTimeline
            });

        context.AddActor(healer);
        context.AddActor(tank);
        context.AddActor(raidMember);
        context.AddActor(boss);

        var ruleset = new CombatRulesetDefinition
        {
            RulesetKey = "development-healing-targets",
            Version = "1"
        };

        var auraManager = new AuraManager();
        var combatRollResolver = new SimpleCombatRollResolver();
        var mitigationResolver = new RulesetDamageMitigationResolver(ruleset);

        var abilityExecutor = new AbilityExecutor(
            auraManager,
            combatRollResolver,
            mitigationResolver);

        var rotationExecutor = new PriorityRotationExecutor(
            rotation,
            "healer",
            "tank",
            abilityExecutor,
            reactToTargetStateChanges: true);

        var engine = new SimulationEngine(
        [
            abilityExecutor,
            auraManager,
            rotationExecutor
        ]);

        return engine.Run(context);
    }
}
