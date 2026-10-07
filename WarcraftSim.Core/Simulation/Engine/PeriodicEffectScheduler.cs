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
            0;

        for (
            var tickNumber = 1;
            ;
            tickNumber++)
        {
            var offset =
                tickInterval *
                tickNumber;

            if (offset > duration)
            {
                break;
            }

            if (
                offset == duration &&
                !aura.Definition.IncludeExpirationBoundaryTick)
            {
                break;
            }

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

                    IsPeriodic =
                        true,

                    IsInternal =
                        true,

                    Description =
                        $"{effect.Key} periodic tick {tickNumber}."
                }
            );

            scheduledCount++;
        }

        return scheduledCount;
    }
}
