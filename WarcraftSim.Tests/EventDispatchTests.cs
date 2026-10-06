using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class EventDispatchTests
{
    [Fact]
    public void AbilityGameplayEvents_AreRecordedAndDispatchedExactlyOnce()
    {
        var source =
            CreateActor(
                key:
                    "source",
                name:
                    "Source",
                teamKey:
                    "raid"
            );

        var target =
            CreateActor(
                key:
                    "target",
                name:
                    "Target",
                teamKey:
                    "enemy"
            );

        source.AddResource(
            new ResourceState(
                resourceKey:
                    "mana",

                maximum:
                    100m,

                startingValue:
                    100m,

                regenerationPerSecond:
                    0m
            )
        );

        source.AddAbility(
            new AbilityDefinition
            {
                Key =
                    "test-strike",

                Name =
                    "Test Strike",

                CastTimeSeconds =
                    0m,

                IsOffGlobalCooldown =
                    true,

                ResourceCosts =
                [
                    new AbilityResourceCost
                    {
                        ResourceKey =
                            "mana",

                        Amount =
                            10m
                    }
                ],

                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key =
                            "test-strike-damage",

                        EffectType =
                            AbilityEffectTypes.DirectDamage,

                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,

                        MitigationType =
                            DamageMitigationTypes.None,

                        CanMiss =
                            false,

                        CanCrit =
                            false,

                        MinimumValue =
                            250m,

                        MaximumValue =
                            250m
                    }
                ]
            }
        );

        var context =
            CreateContext(
                primaryActorKey:
                    source.Key,

                durationSeconds:
                    1m
            );

        context.AddActor(
            source
        );

        context.AddActor(
            target
        );

        var auraManager =
            new AuraManager();

        var abilityExecutor =
            CreateAbilityExecutor(
                auraManager
            );

        var observer =
            new RecordingEventProcessor();

        var engine =
            new SimulationEngine(
                [
                    abilityExecutor,
                    auraManager,
                    observer
                ]
            );

        var result =
            engine.Run(
                context,
                startedContext =>
                {
                    var startResult =
                        abilityExecutor.TryStartAbility(
                            startedContext,
                            source.Key,
                            target.Key,
                            "test-strike"
                        );

                    Assert.True(
                        startResult.Success
                    );
                }
            );

        Assert.Equal(
            750m,
            target.CurrentHealth
        );

        Assert.Equal(
            90m,
            source.Resources[
                "mana"
            ].Current
        );

        Assert.Single(
            observer.Events,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.AbilityCastStarted
        );

        Assert.Single(
            observer.Events,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.ResourceChanged
        );

        Assert.Single(
            observer.Events,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.Damage
        );

        Assert.Single(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.Damage
        );

        Assert.Equal(
            250m,
            result.Summary.DamageDone
        );

        Assert.Equal(
            250m,
            result.Summary.ActorSummaries[
                source.Key
            ].DamageDone
        );

        Assert.Equal(
            250m,
            result.Summary.ActorSummaries[
                target.Key
            ].DamageTaken
        );
    }

    [Fact]
    public void AuraAppliedAndRemoved_AreDispatchedToProcessors()
    {
        var target =
            CreateActor(
                key:
                    "target",
                name:
                    "Aura Target",
                teamKey:
                    "raid"
            );

        var context =
            CreateContext(
                primaryActorKey:
                    target.Key,

                durationSeconds:
                    2m
            );

        context.AddActor(
            target
        );

        var auraManager =
            new AuraManager();

        var observer =
            new RecordingEventProcessor();

        var engine =
            new SimulationEngine(
                [
                    auraManager,
                    observer
                ]
            );

        var result =
            engine.Run(
                context,
                startedContext =>
                {
                    auraManager.ApplyAura(
                        startedContext,
                        target,
                        new AuraDefinition
                        {
                            Key =
                                "test-aura",

                            Name =
                                "Test Aura",

                            DurationSeconds =
                                1m,

                            StackingMode =
                                AuraStackingMode.Refresh,

                            MaxStacks =
                                1
                        },
                        sourceActorKey:
                            "source",
                        abilityKey:
                            "test-ability",
                        effectKey:
                            "test-effect",
                        scheduleExpiration:
                            true
                    );
                }
            );

        Assert.Empty(
            target.ActiveAuras
        );

        var applied =
            Assert.Single(
                observer.Events,
                combatEvent =>
                    combatEvent.Type ==
                    CombatEventType.AuraApplied
            );

        var removed =
            Assert.Single(
                observer.Events,
                combatEvent =>
                    combatEvent.Type ==
                    CombatEventType.AuraRemoved
            );

        Assert.Equal(
            0m,
            applied.TimeSeconds
        );

        Assert.Equal(
            1m,
            removed.TimeSeconds
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.AuraApplied
        );

        Assert.Contains(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.AuraRemoved
        );

        Assert.DoesNotContain(
            result.Timeline,
            combatEvent =>
                combatEvent.Type ==
                CombatEventType.AuraExpiration
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string name,
        string teamKey)
    {
        var actor =
            new SimulationActorState
            {
                Key =
                    key,

                Name =
                    name,

                TeamKey =
                    teamKey
            };

        actor.InitializeHealth(
            maximumHealth:
                1000m
        );

        return actor;
    }

    private static SimulationContext CreateContext(
        string primaryActorKey,
        decimal durationSeconds)
    {
        return new SimulationContext(
            new SimulationRunOptions
            {
                Seed =
                    12345,

                DurationSeconds =
                    durationSeconds,

                PrimaryActorKey =
                    primaryActorKey,

                CaptureTimeline =
                    true
            }
        );
    }

    private static AbilityExecutor CreateAbilityExecutor(
        AuraManager auraManager)
    {
        return new AbilityExecutor(
            auraManager,
            new SimpleCombatRollResolver(),
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey =
                        "event-dispatch-tests",

                    Version =
                        "1"
                }
            )
        );
    }

    private sealed class RecordingEventProcessor :
        ICombatEventProcessor
    {
        public List<CombatEvent> Events { get; } =
            [];

        public void Process(
            SimulationContext context,
            CombatEvent combatEvent)
        {
            Events.Add(
                combatEvent
            );
        }
    }
}
