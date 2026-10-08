using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class CombatRollDeliveryPropagationTests
{
    [Fact]
    public void PeriodicTicks_ForwardPeriodicDeliveryToCombatResolver()
    {
        var source = CreateActor("caster", "raid");
        var target = CreateActor("target", "enemy");

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "dot",
                Name = "dot",
                IsOffGlobalCooldown = true,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "dot-tick",
                        EffectType =
                            AbilityEffectTypes.PeriodicDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,
                        MitigationType =
                            DamageMitigationTypes.None,
                        CanMiss = false,
                        CanCrit = false,
                        DurationSeconds = 2m,
                        TickIntervalSeconds = 1m,
                        MinimumValue = 1m,
                        MaximumValue = 1m
                    }
                ]
            }
        );

        var recorder = new RecordingCombatRollResolver();
        var auraManager = new AuraManager();
        var executor = CreateExecutor(
            auraManager,
            recorder
        );

        RunAbility(
            source,
            target,
            durationSeconds: 2m,
            executor,
            auraManager
        );

        Assert.NotEmpty(recorder.Deliveries);
        Assert.All(
            recorder.Deliveries,
            delivery => Assert.Equal(
                CombatEffectDeliveryType.Periodic,
                delivery
            )
        );
    }

    [Fact]
    public void ChannelTicks_ForwardChannelTickDeliveryToCombatResolver()
    {
        var source = CreateActor("caster", "raid");
        var target = CreateActor("target", "enemy");

        source.AddAbility(
            new AbilityDefinition
            {
                Key = "channel",
                Name = "channel",
                IsOffGlobalCooldown = true,
                ChannelDurationSeconds = 2m,
                ChannelTickIntervalSeconds = 1m,
                Effects =
                [
                    new AbilityEffectDefinition
                    {
                        Key = "channel-tick",
                        EffectType =
                            AbilityEffectTypes.DirectDamage,
                        TargetType =
                            AbilityTargetTypes.Enemy,
                        ResolutionType =
                            CombatResolutionTypes.AlwaysHits,
                        MitigationType =
                            DamageMitigationTypes.None,
                        CanMiss = false,
                        CanCrit = false,
                        MinimumValue = 1m,
                        MaximumValue = 1m,
                        ApplyOnChannelTick = true
                    }
                ]
            }
        );

        var recorder = new RecordingCombatRollResolver();
        var auraManager = new AuraManager();
        var executor = CreateExecutor(
            auraManager,
            recorder
        );

        RunAbility(
            source,
            target,
            durationSeconds: 3m,
            executor,
            auraManager
        );

        Assert.NotEmpty(recorder.Deliveries);
        Assert.All(
            recorder.Deliveries,
            delivery => Assert.Equal(
                CombatEffectDeliveryType.ChannelTick,
                delivery
            )
        );
    }

    private static void RunAbility(
        SimulationActorState source,
        SimulationActorState target,
        decimal durationSeconds,
        AbilityExecutor executor,
        AuraManager auraManager)
    {
        var context = new SimulationContext(
            new SimulationRunOptions
            {
                Seed = 12345,
                DurationSeconds = durationSeconds,
                PrimaryActorKey = source.Key,
                CaptureTimeline = true
            }
        );

        context.AddActor(source);
        context.AddActor(target);

        new SimulationEngine(
            [
                executor,
                auraManager
            ])
            .Run(
                context,
                startedContext =>
                {
                    Assert.True(
                        executor.TryStartAbility(
                            startedContext,
                            source.Key,
                            target.Key,
                            source.Abilities.Values
                                .Single()
                                .Definition.Key
                        ).Success
                    );
                }
            );
    }

    private static AbilityExecutor CreateExecutor(
        AuraManager auraManager,
        ICombatRollResolver resolver)
    {
        return new AbilityExecutor(
            auraManager,
            resolver,
            new RulesetDamageMitigationResolver(
                new CombatRulesetDefinition
                {
                    RulesetKey = "delivery-tests",
                    Version = "1"
                }
            )
        );
    }

    private static SimulationActorState CreateActor(
        string key,
        string teamKey)
    {
        var actor = new SimulationActorState
        {
            Key = key,
            Name = key,
            TeamKey = teamKey,
            Level = 30
        };

        actor.InitializeHealth(
            maximumHealth: 5000m
        );

        actor.ConfigureActionTiming(
            initialActionDelaySeconds: 0m,
            inputDelaySeconds: 0m
        );

        return actor;
    }

    private sealed class RecordingCombatRollResolver :
        ICombatRollResolver
    {
        public List<CombatEffectDeliveryType> Deliveries { get; } = [];

        public CombatRollResult Resolve(
            SimulationContext context,
            SimulationActorState source,
            SimulationActorState target,
            AbilityDefinition ability,
            AbilityEffectDefinition effect)
        {
            Deliveries.Add(
                CombatEffectDeliveryType.Direct
            );

            return CombatRollResult.Hit();
        }

        public CombatRollResult Resolve(
            SimulationContext context,
            SimulationActorState source,
            SimulationActorState target,
            AbilityDefinition ability,
            AbilityEffectDefinition effect,
            CombatEffectDeliveryType deliveryType)
        {
            Deliveries.Add(deliveryType);

            return CombatRollResult.Hit();
        }
    }
}
