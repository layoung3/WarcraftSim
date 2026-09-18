using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ScriptedEncounterDamageProcessor :
    ICombatEventProcessor
{
    private readonly IDamageMitigationResolver
        _damageMitigationResolver;

    public ScriptedEncounterDamageProcessor(
        IDamageMitigationResolver damageMitigationResolver)
    {
        _damageMitigationResolver =
            damageMitigationResolver;
    }

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

        var rawDamage =
            Math.Max(
                0m,
                combatEvent.RawAmount ??
                combatEvent.Amount ??
                0m
            );

        var mitigationType =
            string.IsNullOrWhiteSpace(
                combatEvent.MitigationType)
                ? DamageMitigationTypes.None
                : combatEvent.MitigationType;

        DamageMitigationResult mitigation;

        if (string.Equals(
                mitigationType,
                DamageMitigationTypes.None,
                StringComparison.OrdinalIgnoreCase))
        {
            mitigation =
                DamageMitigationResult.Unmitigated(
                    rawDamage
                );
        }
        else
        {
            if (string.IsNullOrWhiteSpace(
                    combatEvent.SourceActorKey))
            {
                throw new InvalidOperationException(
                    $"Scripted encounter damage '{combatEvent.EncounterEventKey}' " +
                    $"uses mitigation type '{mitigationType}' but has no source actor."
                );
            }

            var source =
                context.GetActor(
                    combatEvent.SourceActorKey
                );

            if (source is null)
            {
                throw new InvalidOperationException(
                    $"Source actor '{combatEvent.SourceActorKey}' for scripted " +
                    $"encounter damage '{combatEvent.EncounterEventKey}' was not found."
                );
            }

            // Reuse the same mitigation pipeline as normal ability damage.
            // The synthetic ability/effect exist only to supply the
            // school and mitigation metadata expected by the resolver.
            var syntheticAbility =
                new AbilityDefinition
                {
                    Key =
                        GetBreakdownKey(
                            combatEvent
                        ),

                    Name =
                        combatEvent.Description ??
                        combatEvent.EncounterEventKey ??
                        "Encounter Damage"
                };

            var syntheticEffect =
                new AbilityEffectDefinition
                {
                    Key =
                        $"{syntheticAbility.Key}-effect",

                    EffectType =
                        AbilityEffectTypes.DirectDamage,

                    TargetType =
                        AbilityTargetTypes.Enemy,

                    SchoolKey =
                        combatEvent.SchoolKey,

                    ResolutionType =
                        CombatResolutionTypes.AlwaysHits,

                    MitigationType =
                        mitigationType,

                    CanMiss =
                        false,

                    CanCrit =
                        false,

                    MinimumValue =
                        rawDamage,

                    MaximumValue =
                        rawDamage
                };

            mitigation =
                _damageMitigationResolver.Resolve(
                    context,
                    source,
                    target,
                    syntheticAbility,
                    syntheticEffect,
                    rawDamage
                );
        }

        var actualDamage =
            target.TakeDamage(
                mitigation.FinalAmount
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

                AbilityKey =
                    GetBreakdownKey(
                        combatEvent
                    ),

                EncounterEventKey =
                    combatEvent.EncounterEventKey,

                SchoolKey =
                    combatEvent.SchoolKey,

                MitigationType =
                    mitigationType,

                ResultKey =
                    CombatResultTypes.Hit,

                RawAmount =
                    mitigation.RawAmount,

                MitigatedAmount =
                    mitigation.MitigatedAmount,

                MitigationPercent =
                    mitigation.ReductionPercent,

                Amount =
                    actualDamage,

                Description =
                    $"{combatEvent.Description ?? "Encounter damage"} dealt " +
                    $"{actualDamage:0.##} damage to {target.Name}."
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

                    AbilityKey =
                        GetBreakdownKey(
                            combatEvent
                        ),

                    EncounterEventKey =
                        combatEvent.EncounterEventKey,

                    Description =
                        $"{target.Name} died."
                }
            );
        }
    }

    private static string GetBreakdownKey(
        CombatEvent combatEvent)
    {
        return string.IsNullOrWhiteSpace(
                combatEvent.EncounterEventKey)
            ? "encounter-damage"
            : $"encounter:{combatEvent.EncounterEventKey}";
    }
}
