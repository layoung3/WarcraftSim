using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class AbilityEffectTargetingTests
{
    [Fact]
    public void Ability_CanDamagePrimaryEnemyAndHealSource()
    {
        var source =
            CreateActor(
                "source",
                "raid",
                maximumHealth:
                    1000m,

                startingHealth:
                    500m
            );

        var target =
            CreateActor(
                "target",
                "enemy"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "draining-strike",

                Name =
                    "Draining Strike",

                IsOffGlobalCooldown =
                    true,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "strike-damage",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            100m,

                        MaximumValue =
                            100m
                    },

                    new AbilityEffectDefinition
                    {
                        Key =
                            "self-heal",

                        EffectType =
                            AbilityEffectTypes.DirectHealing,

                        TargetType =
                            AbilityTargetTypes.Self,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            200m,

                        MaximumValue =
                            200m
                    }
                ]
            }
        );

        var result =
            RunSingleAbility(
                source,
                target,
                []
            );

        Assert.Equal(
            900m,
            target.CurrentHealth
        );

        Assert.Equal(
            700m,
            source.CurrentHealth
        );

        var damage =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Damage
            );

        Assert.Equal(
            target.Key,
            damage.TargetActorKey
        );

        var healing =
            Assert.Single(
                result.Timeline,
                combatEvent =>
                    combatEvent.Type ==
                        CombatEventType.Healing
            );

        Assert.Equal(
            source.Key,
            healing.TargetActorKey
        );
    }

    [Fact]
    public void Ability_MultiTargetDamageUsesPrimaryThenDeterministicAdditionalEnemies()
    {
        var source =
            CreateActor(
                "source",
                "raid"
            );

        var primary =
            CreateActor(
                "boss",
                "enemy"
            );

        var addZ =
            CreateActor(
                "z-add",
                "enemy"
            );

        var addA =
            CreateActor(
                "a-add",
                "enemy"
            );

        var addM =
            CreateActor(
                "m-add",
                "enemy"
            );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "cleave",

                Name =
                    "Cleave",

                IsOffGlobalCooldown =
                    true,

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "cleave-damage",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        TargetType =
                            AbilityTargetTypes.Enemy,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            100m,

                        MaximumValue =
                            100m,

                        MaxTargets =
                            3
                    }
                ]
            }
        );

        var result =
            RunSingleAbility(
                source,
                primary,
                [
                    addZ,
                    addM,
                    addA
                ]
            );

        Assert.Equal(
            900m,
            primary.CurrentHealth
        );

        Assert.Equal(
            900m,
            addA.CurrentHealth
        );

        Assert.Equal(
            900m,
            addM.CurrentHealth
        );

        Assert.Equal(
            1000m,
            addZ.CurrentHealth
        );

        Assert.Equal(
            new[]
            {
                "boss",
                "a-add",
                "m-add"
            },
            result.Timeline
                .Where(
                    combatEvent =>
                        combatEvent.Type ==
                            CombatEventType.Damage
                )
                .Select(
                    combatEvent =>
                        combatEvent.TargetActorKey!
                )
                .ToArray()
        );
    }

    private static SimulationRunResult RunSingleAbility(
        SimulationActorState source,
        SimulationActorState primaryTarget,
        IReadOnlyList<SimulationActorState> additionalActors)
    {
        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed =
                        12345,

                    DurationSeconds =
                        1m,

                    PrimaryActorKey =
                        source.Key,

                    CaptureTimeline =
                        true
                }
            );

        context.AddActor(
            source
        );

        context.AddActor(
            primaryTarget
        );

        foreach (
            var additionalActor in
            additionalActors)
        {
            context.AddActor(
                additionalActor
            );
        }

        var executor =
            CreateAbilityExecutor();

        var engine =
            new SimulationEngine(
                [
                    executor
                ]
            );

        return engine.Run(
            context,
            startedContext =>
            {
                var startResult =
                    executor.TryStartAbility(
                        startedContext,
                        source.Key,
                        primaryTarget.Key,
                        source.Abilities.Values
                            .Single()
                            .Definition.Key
                    );

                Assert.True(
                    startResult.Success
                );
            }
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey,
        decimal maximumHealth = 1000m,
        decimal? startingHealth = null)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    key,

                TeamKey =
                    teamKey
            };

        actor.InitializeHealth(
            maximumHealth,
            startingHealth
        );

        actor.ConfigureActionTiming(
            initialActionDelaySeconds:
                0m,

            inputDelaySeconds:
                0m
        );

        return actor;
    }

    private static AbilityExecutor CreateAbilityExecutor()
    {
        return new AbilityExecutor(
            new AuraManager(),
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "effect-targeting-tests",

                    Version =
                        "1"
                }
            )
        );
    }
}
