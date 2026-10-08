using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Tests;

public sealed class AbilityCombatSummaryTests
{
    [Fact]
    public void DirectDamage_RecordsAmountResultAndDelivery()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "strike",
                ResultKey = CombatResultTypes.Hit,
                Amount = 125m,
                EffectDeliveryType = CombatEffectDeliveryType.Direct
            }
        );

        var summary = result.Summary.Abilities["strike"];

        Assert.Equal(1, summary.DamageOccurrenceCount);
        Assert.Equal(125m, summary.DamageDone);
        Assert.Equal(1, summary.DirectOccurrenceCount);
        Assert.Equal(0, summary.PeriodicOccurrenceCount);
        Assert.Equal(0, summary.ChannelTickOccurrenceCount);
        Assert.Equal(1, summary.ResultCounts[CombatResultTypes.Hit]);
    }

    [Fact]
    public void AvoidedDamage_StillRecordsOutcomeOccurrence()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "strike",
                ResultKey = CombatResultTypes.Miss,
                Amount = 0m
            }
        );

        var summary = result.Summary.Abilities["strike"];

        Assert.Equal(1, summary.DamageOccurrenceCount);
        Assert.Equal(0m, summary.DamageDone);
        Assert.Equal(1, summary.ResultCounts[CombatResultTypes.Miss]);
    }

    [Fact]
    public void CriticalDamage_RecordsCriticalCount()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "crit-spell",
                ResultKey = CombatResultTypes.Critical,
                Amount = 240m,
                IsCritical = true
            }
        );

        var summary = result.Summary.Abilities["crit-spell"];

        Assert.Equal(1, summary.CriticalDamageCount);
        Assert.Equal(1, summary.ResultCounts[CombatResultTypes.Critical]);
    }

    [Fact]
    public void PeriodicAndChannelTickOccurrences_AreSeparated()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "multi",
                ResultKey = CombatResultTypes.Hit,
                Amount = 10m,
                EffectDeliveryType = CombatEffectDeliveryType.Periodic
            },
            new CombatEvent
            {
                TimeSeconds = 0.2m,
                Type = CombatEventType.Damage,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "multi",
                ResultKey = CombatResultTypes.Hit,
                Amount = 20m,
                EffectDeliveryType = CombatEffectDeliveryType.ChannelTick
            }
        );

        var summary = result.Summary.Abilities["multi"];

        Assert.Equal(2, summary.DamageOccurrenceCount);
        Assert.Equal(30m, summary.DamageDone);
        Assert.Equal(1, summary.PeriodicOccurrenceCount);
        Assert.Equal(1, summary.ChannelTickOccurrenceCount);
        Assert.Equal(0, summary.DirectOccurrenceCount);
    }

    [Fact]
    public void Healing_RecordsCriticalOverhealingAndDelivery()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Healing,
                SourceActorKey = "player",
                TargetActorKey = "player",
                AbilityKey = "hot",
                ResultKey = CombatResultTypes.Critical,
                Amount = 80m,
                OverhealingAmount = 20m,
                IsCritical = true,
                EffectDeliveryType = CombatEffectDeliveryType.Periodic
            }
        );

        var summary = result.Summary.Abilities["hot"];

        Assert.Equal(1, summary.HealingOccurrenceCount);
        Assert.Equal(80m, summary.HealingDone);
        Assert.Equal(20m, summary.OverhealingDone);
        Assert.Equal(1, summary.CriticalHealingCount);
        Assert.Equal(1, summary.PeriodicOccurrenceCount);
    }

    [Fact]
    public void AbilityLifecycle_RecordsCastAndChannelCounts()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.05m,
                Type = CombatEventType.AbilityCastStarted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel"
            },
            new CombatEvent
            {
                TimeSeconds = 0.10m,
                Type = CombatEventType.AbilityCastCompleted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel"
            },
            new CombatEvent
            {
                TimeSeconds = 0.10m,
                Type = CombatEventType.AbilityChannelStarted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel"
            },
            new CombatEvent
            {
                TimeSeconds = 0.50m,
                Type = CombatEventType.AbilityChannelCompleted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel"
            }
        );

        var summary = result.Summary.Abilities["channel"];

        Assert.Equal(1, summary.CastStartedCount);
        Assert.Equal(1, summary.CastCompletedCount);
        Assert.Equal(1, summary.ChannelStartedCount);
        Assert.Equal(1, summary.ChannelCompletedCount);
    }

    [Fact]
    public void InternalChannelCompletionDriver_IsNotDoubleCounted()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.40m,
                Type = CombatEventType.AbilityChannelCompleted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel",
                IsInternal = true
            },
            new CombatEvent
            {
                TimeSeconds = 0.40m,
                Type = CombatEventType.AbilityChannelCompleted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "channel"
            }
        );

        Assert.Equal(
            1,
            result.Summary.Abilities["channel"].ChannelCompletedCount
        );
    }

    [Fact]
    public void CancelledExecution_StaleCastCompletionIsNotReported()
    {
        var context =
            CreateContext();

        var execution =
            new AbilityExecutionState
            {
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "cancelled-cast"
            };

        context.AddAbilityExecution(execution);
        context.CancelAbilityExecution(execution.Id, 0m);

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.AbilityCastCancelled,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "cancelled-cast",
                AbilityExecutionId = execution.Id
            }
        );

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = 0.2m,
                Type = CombatEventType.AbilityCastCompleted,
                SourceActorKey = "player",
                TargetActorKey = "target",
                AbilityKey = "cancelled-cast",
                AbilityExecutionId = execution.Id
            }
        );

        var result =
            new SimulationEngine().Run(context);

        var summary =
            result.Summary.Abilities["cancelled-cast"];

        Assert.Equal(1, summary.CastCancelledCount);
        Assert.Equal(0, summary.CastCompletedCount);
    }

    [Fact]
    public void AbsorbConsumption_RecordsActualAbsorptionDone()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.AbsorbConsumed,
                SourceActorKey = "player",
                TargetActorKey = "player",
                AbilityKey = "shield",
                Amount = 75m
            }
        );

        var summary = result.Summary.Abilities["shield"];

        Assert.Equal(1, summary.AbsorbConsumptionCount);
        Assert.Equal(75m, summary.AbsorptionDone);
    }

    [Fact]
    public void PrimaryAbilitySummary_IsolatedFromOtherActors()
    {
        var result = Run(
            new CombatEvent
            {
                TimeSeconds = 0.1m,
                Type = CombatEventType.Damage,
                SourceActorKey = "target",
                TargetActorKey = "player",
                AbilityKey = "enemy-hit",
                ResultKey = CombatResultTypes.Hit,
                Amount = 50m
            }
        );

        Assert.False(result.Summary.Abilities.ContainsKey("enemy-hit"));
        Assert.Equal(
            50m,
            result.Summary.ActorSummaries["target"]
                .Abilities["enemy-hit"]
                .DamageDone
        );
    }

    private static SimulationRunResult Run(
        params CombatEvent[] events)
    {
        var context =
            CreateContext();

        foreach (var combatEvent in events)
        {
            context.ScheduleEvent(combatEvent);
        }

        return new SimulationEngine().Run(context);
    }

    private static SimulationContext CreateContext()
    {
        var context =
            new SimulationContext(
                new SimulationRunOptions
                {
                    Seed = 12345,
                    DurationSeconds = 1m,
                    PrimaryActorKey = "player"
                }
            );

        var player =
            new SimulationActorState
            {
                Key = "player",
                Name = "Player"
            };

        player.InitializeHealth(1000m);

        var target =
            new SimulationActorState
            {
                Key = "target",
                Name = "Target"
            };

        target.InitializeHealth(1000m);

        context.AddActor(player);
        context.AddActor(target);

        return context;
    }
}
