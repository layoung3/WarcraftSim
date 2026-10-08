using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class DamageTakenResourceGenerationProcessor :
    ICombatEventProcessor
{
    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(combatEvent);

        if (
            combatEvent.Type != CombatEventType.Damage ||
            string.IsNullOrWhiteSpace(combatEvent.TargetActorKey))
        {
            return;
        }

        var target =
            context.GetActor(
                combatEvent.TargetActorKey
            );

        if (
            target is null ||
            target.DamageTakenResourceGenerations.Count == 0)
        {
            return;
        }

        foreach (
            var definition in
            target.DamageTakenResourceGenerations.Values)
        {
            ApplyGeneration(
                context,
                target,
                combatEvent,
                definition
            );
        }
    }

    private static void ApplyGeneration(
        SimulationContext context,
        SimulationActorState target,
        CombatEvent combatEvent,
        DamageTakenResourceGenerationDefinition definition)
    {
        if (
            definition.RequiresExternalSourceActor &&
            (
                string.IsNullOrWhiteSpace(combatEvent.SourceActorKey) ||
                string.Equals(
                    combatEvent.SourceActorKey,
                    target.Key,
                    StringComparison.OrdinalIgnoreCase
                ) ||
                context.GetActor(combatEvent.SourceActorKey) is null
            ))
        {
            return;
        }

        if (!target.Resources.TryGetValue(
                definition.ResourceKey,
                out var resource))
        {
            throw new InvalidOperationException(
                $"Damage-taken resource generation '{definition.Key}' requires resource '{definition.ResourceKey}', but actor '{target.Key}' does not have that resource."
            );
        }

        if (target.MaximumHealth <= 0m)
        {
            return;
        }

        var eligibleDamage =
            ResolveEligibleDamage(
                combatEvent,
                definition
            );

        if (eligibleDamage <= 0m)
        {
            return;
        }

        var requestedAmount =
            eligibleDamage /
            target.MaximumHealth *
            definition.ResourcePerMaximumHealthOfEligibleDamage;

        if (requestedAmount <= 0m)
        {
            return;
        }

        var actualAmount =
            resource.Gain(
                requestedAmount
            );

        if (actualAmount <= 0m)
        {
            return;
        }

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.ResourceChanged,

                SourceActorKey =
                    target.Key,

                TargetActorKey =
                    target.Key,

                AbilityKey =
                    definition.Key,

                ResultKey =
                    combatEvent.ResultKey,

                Amount =
                    actualAmount,

                Description =
                    $"{target.Name} generated {actualAmount:0.##} {resource.ResourceKey} from {definition.Name}."
            }
        );
    }

    internal static decimal ResolveEligibleDamage(
        CombatEvent combatEvent,
        DamageTakenResourceGenerationDefinition definition)
    {
        var actualHealthDamage =
            Math.Max(
                0m,
                combatEvent.Amount ?? 0m
            );

        var absorbedDamage =
            Math.Max(
                0m,
                combatEvent.AbsorbedAmount ?? 0m
            );

        var ignoresArmorForThisEvent =
            definition.IgnoreArmorMitigation &&
            string.Equals(
                combatEvent.MitigationType,
                DamageMitigationTypes.Armor,
                StringComparison.OrdinalIgnoreCase
            );

        if (!ignoresArmorForThisEvent)
        {
            return
                actualHealthDamage +
                (definition.IgnoreAbsorbs
                    ? absorbedDamage
                    : 0m);
        }

        var rawDamage =
            Math.Max(
                0m,
                combatEvent.RawAmount ??
                actualHealthDamage +
                absorbedDamage
            );

        var mitigationPercent =
            Math.Clamp(
                combatEvent.MitigationPercent ?? 0m,
                0m,
                100m
            );

        var postMitigationFraction =
            1m -
            mitigationPercent /
            100m;

        var excludedPreMitigationDamage =
            0m;

        if (definition.BlockReducesEligibleDamage)
        {
            excludedPreMitigationDamage +=
                ConvertPostMitigationToPreMitigation(
                    Math.Max(
                        0m,
                        combatEvent.BlockedAmount ?? 0m
                    ),
                    postMitigationFraction
                );
        }

        if (!definition.IgnoreAbsorbs)
        {
            excludedPreMitigationDamage +=
                ConvertPostMitigationToPreMitigation(
                    absorbedDamage,
                    postMitigationFraction
                );
        }

        return Math.Max(
            0m,
            rawDamage -
            Math.Min(
                rawDamage,
                excludedPreMitigationDamage
            )
        );
    }

    private static decimal ConvertPostMitigationToPreMitigation(
        decimal amount,
        decimal postMitigationFraction)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        return
            postMitigationFraction > 0m
                ? amount /
                  postMitigationFraction
                : amount;
    }
}
