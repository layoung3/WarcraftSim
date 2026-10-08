using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

public static class PeriodicEffectScheduler
{
    public static int ScheduleTicks(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        AuraInstance aura,
        Guid abilityExecutionId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(aura);

        var duration =
            Math.Max(
                0m,
                aura.Definition.DurationSeconds
            );

        var tickInterval =
            Math.Max(
                0m,
                aura.Definition.PeriodicTickIntervalSeconds ??
                0m
            );

        if (
            duration <= 0m ||
            tickInterval <= 0m)
        {
            return 0;
        }

        var scheduledCount =
            GetScheduledTickCount(
                duration,
                tickInterval,
                aura.Definition.IncludeExpirationBoundaryTick
            );

        for (
            var tickNumber = 1;
            tickNumber <= scheduledCount;
            tickNumber++)
        {
            var offset =
                tickInterval *
                tickNumber;

            context.ScheduleEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        aura.AppliedAtSeconds +
                        offset,

                    Type =
                        CombatEventType.PeriodicTick,

                    SourceActorKey =
                        source.Key,

                    TargetActorKey =
                        target.Key,

                    AbilityKey =
                        ability.Key,

                    AbilityExecutionId =
                        abilityExecutionId,

                    EffectKey =
                        effect.Key,

                    SchoolKey =
                        effect.SchoolKey,

                    AuraInstanceId =
                        aura.InstanceId,

                    EffectDeliveryType =
                        CombatEffectDeliveryType.Periodic,

                    IsInternal =
                        true,

                    Description =
                        $"{effect.Key} periodic tick {tickNumber}."
                }
            );
        }

        return scheduledCount;
    }

    public static int GetScheduledTickCount(
        decimal durationSeconds,
        decimal tickIntervalSeconds,
        bool includeExpirationBoundaryTick)
    {
        var duration =
            Math.Max(
                0m,
                durationSeconds
            );

        var tickInterval =
            Math.Max(
                0m,
                tickIntervalSeconds
            );

        if (
            duration <= 0m ||
            tickInterval <= 0m)
        {
            return 0;
        }

        var tickCount =
            (int)Math.Floor(
                duration /
                tickInterval
            );

        if (
            !includeExpirationBoundaryTick &&
            tickCount > 0 &&
            tickInterval * tickCount ==
                duration)
        {
            tickCount--;
        }

        return Math.Max(
            0,
            tickCount
        );
    }
}
