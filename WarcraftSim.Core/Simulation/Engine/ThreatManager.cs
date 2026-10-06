namespace WarcraftSim.Core.Simulation.Engine;

public sealed class ThreatManager :
    ICombatEventProcessor
{
    private readonly IThreatGenerationResolver
        _resolver;

    public ThreatManager(
        IThreatGenerationResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(
            resolver
        );

        _resolver =
            resolver;
    }

    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            combatEvent
        );

        if (
            combatEvent.Type !=
                CombatEventType.Damage &&
            combatEvent.Type !=
                CombatEventType.Healing
        )
        {
            return;
        }

        var contributions =
            _resolver.Resolve(
                context,
                combatEvent
            );

        ArgumentNullException.ThrowIfNull(
            contributions
        );

        foreach (
            var contribution in
            contributions)
        {
            ApplyContribution(
                context,
                combatEvent,
                contribution
            );
        }
    }

    private static void ApplyContribution(
        SimulationContext context,
        CombatEvent sourceEvent,
        ThreatGenerationContribution? contribution)
    {
        if (contribution is null)
        {
            throw new InvalidOperationException(
                "Threat resolver returned a null contribution."
            );
        }

        if (string.IsNullOrWhiteSpace(
                contribution.ThreatOwnerActorKey))
        {
            throw new InvalidOperationException(
                "Threat contribution requires a threat-owner actor key."
            );
        }

        if (string.IsNullOrWhiteSpace(
                contribution.ThreatSourceActorKey))
        {
            throw new InvalidOperationException(
                "Threat contribution requires a threat-source actor key."
            );
        }

        if (contribution.Amount < 0m)
        {
            throw new InvalidOperationException(
                "Threat resolver returned a negative threat contribution."
            );
        }

        if (contribution.Amount == 0m)
        {
            return;
        }

        var threatOwner =
            context.GetActor(
                contribution.ThreatOwnerActorKey
            )
            ?? throw new InvalidOperationException(
                $"Threat owner actor '{contribution.ThreatOwnerActorKey}' does not exist in the simulation."
            );

        var threatSource =
            context.GetActor(
                contribution.ThreatSourceActorKey
            )
            ?? throw new InvalidOperationException(
                $"Threat source actor '{contribution.ThreatSourceActorKey}' does not exist in the simulation."
            );

        var totalThreat =
            threatOwner.ThreatTable.AddThreat(
                threatSource.Key,
                contribution.Amount
            );

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.ThreatChanged,

                SourceActorKey =
                    threatSource.Key,

                TargetActorKey =
                    threatOwner.Key,

                AbilityKey =
                    sourceEvent.AbilityKey,

                AbilityExecutionId =
                    sourceEvent.AbilityExecutionId,

                EffectKey =
                    sourceEvent.EffectKey,

                Amount =
                    contribution.Amount,

                Description =
                    $"{threatSource.Name} generated {contribution.Amount} threat on {threatOwner.Name}; total threat is {totalThreat}."
            }
        );
    }
}
