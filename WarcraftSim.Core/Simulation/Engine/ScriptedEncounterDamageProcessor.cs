namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ScriptedEncounterDamageProcessor :
    ICombatEventProcessor
{
    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (
            combatEvent.Type !=
            CombatEventType.ScriptedDamage ||
            string.IsNullOrWhiteSpace(
                combatEvent.TargetActorKey)
        )
        {
            return;
        }

        var target =
            context.GetActor(
                combatEvent.TargetActorKey
            );

        if (
            target is null ||
            !target.IsAlive
        )
        {
            return;
        }

        var requestedDamage =
            Math.Max(
                0m,
                combatEvent.Amount ?? 0m
            );

        var actualDamage =
            target.TakeDamage(
                requestedDamage
            );

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.Damage,

                SourceActorKey =
                    combatEvent.SourceActorKey,

                TargetActorKey =
                    target.Key,

                EncounterEventKey =
                    combatEvent.EncounterEventKey,

                SchoolKey =
                    combatEvent.SchoolKey,

                ResultKey =
                    CombatResultTypes.Hit,

                RawAmount =
                    requestedDamage,

                MitigatedAmount =
                    0m,

                MitigationPercent =
                    0m,

                Amount =
                    actualDamage,

                Description =
                    $"{combatEvent.Description ?? "Encounter damage"} dealt {actualDamage:0.##} damage to {target.Name}."
            }
        );

        if (!target.IsAlive)
        {
            context.ScheduleEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        context.CurrentTimeSeconds,

                    Type =
                        CombatEventType.ActorDied,

                    SourceActorKey =
                        combatEvent.SourceActorKey,

                    TargetActorKey =
                        target.Key,

                    EncounterEventKey =
                        combatEvent.EncounterEventKey,

                    Description =
                        $"{target.Name} died."
                }
            );
        }
    }
}
