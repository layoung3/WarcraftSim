using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AbsorbManager :
    ICombatEventProcessor
{
    public AbsorbInstance? ApplyAbsorb(
        SimulationContext context,
        SimulationActorState target,
        string absorbKey,
        string name,
        decimal amount,
        decimal durationSeconds,
        AbsorbStackingMode stackingMode,
        int maxStacks,
        string sourceActorKey,
        string? abilityKey,
        string? effectKey,
        bool scheduleExpiration = true)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            target
        );

        if (string.IsNullOrWhiteSpace(
                absorbKey))
        {
            throw new ArgumentException(
                "Absorb application requires an absorb key.",
                nameof(absorbKey)
            );
        }

        if (amount <= 0m)
        {
            return null;
        }

        if (durationSeconds <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationSeconds),
                durationSeconds,
                "Absorb duration must be greater than zero."
            );
        }

        if (maxStacks < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxStacks),
                maxStacks,
                "Absorb max stacks must be at least one."
            );
        }

        var now =
            context.CurrentTimeSeconds;

        var matching =
            target.ActiveAbsorbs
                .Where(
                    absorb =>
                        string.Equals(
                            absorb.AbsorbKey,
                            absorbKey,
                            StringComparison.OrdinalIgnoreCase
                        ) &&
                        string.Equals(
                            absorb.SourceActorKey,
                            sourceActorKey,
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .ToList();

        AbsorbInstance instance;
        decimal appliedAmount;

        switch (stackingMode)
        {
            case AbsorbStackingMode.Independent:
                instance =
                    CreateInstance(
                        absorbKey,
                        name,
                        amount,
                        durationSeconds,
                        stackingMode,
                        maxStacks,
                        sourceActorKey,
                        target.Key,
                        abilityKey,
                        effectKey,
                        now
                    );

                target.ActiveAbsorbs.Add(
                    instance
                );

                appliedAmount =
                    amount;
                break;

            case AbsorbStackingMode.Replace:
                foreach (
                    var existing in
                    matching)
                {
                    RemoveAbsorb(
                        context,
                        target,
                        existing,
                        "replaced"
                    );
                }

                instance =
                    CreateInstance(
                        absorbKey,
                        name,
                        amount,
                        durationSeconds,
                        stackingMode,
                        maxStacks,
                        sourceActorKey,
                        target.Key,
                        abilityKey,
                        effectKey,
                        now
                    );

                target.ActiveAbsorbs.Add(
                    instance
                );

                appliedAmount =
                    amount;
                break;

            case AbsorbStackingMode.Stack:
                instance =
                    matching.FirstOrDefault() ??
                    CreateInstance(
                        absorbKey,
                        name,
                        amount,
                        durationSeconds,
                        stackingMode,
                        maxStacks,
                        sourceActorKey,
                        target.Key,
                        abilityKey,
                        effectKey,
                        now
                    );

                if (!target.ActiveAbsorbs.Contains(
                        instance))
                {
                    target.ActiveAbsorbs.Add(
                        instance
                    );

                    appliedAmount =
                        amount;
                }
                else
                {
                    instance.InstanceId =
                        Guid.NewGuid();

                    instance.AppliedAtSeconds =
                        now;

                    instance.ExpiresAtSeconds =
                        now +
                        durationSeconds;

                    if (instance.Stacks <
                        maxStacks)
                    {
                        instance.Stacks++;

                        instance.MaximumAmount +=
                            amount;

                        instance.RemainingAmount +=
                            amount;

                        appliedAmount =
                            amount;
                    }
                    else
                    {
                        appliedAmount =
                            0m;
                    }
                }
                break;

            case AbsorbStackingMode.Refresh:
            default:
                instance =
                    matching.FirstOrDefault() ??
                    CreateInstance(
                        absorbKey,
                        name,
                        amount,
                        durationSeconds,
                        stackingMode,
                        maxStacks,
                        sourceActorKey,
                        target.Key,
                        abilityKey,
                        effectKey,
                        now
                    );

                if (!target.ActiveAbsorbs.Contains(
                        instance))
                {
                    target.ActiveAbsorbs.Add(
                        instance
                    );
                }
                else
                {
                    instance.InstanceId =
                        Guid.NewGuid();

                    instance.AppliedAtSeconds =
                        now;

                    instance.ExpiresAtSeconds =
                        now +
                        durationSeconds;

                    instance.MaximumAmount =
                        amount;

                    instance.RemainingAmount =
                        amount;

                    instance.Stacks =
                        1;
                }

                appliedAmount =
                    amount;
                break;
        }

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    now,

                Type =
                    CombatEventType.AbsorbApplied,

                SourceActorKey =
                    sourceActorKey,

                TargetActorKey =
                    target.Key,

                AbilityKey =
                    abilityKey,

                EffectKey =
                    effectKey,

                AbsorbInstanceId =
                    instance.InstanceId,

                Amount =
                    appliedAmount,

                Description =
                    $"{instance.Name} applied to {target.Name} with {instance.RemainingAmount:0.##} absorb remaining."
            }
        );

        if (scheduleExpiration)
        {
            ScheduleExpiration(
                context,
                instance
            );
        }

        return instance;
    }

    public DamageAbsorptionResult ApplyDamage(
        SimulationContext context,
        SimulationActorState target,
        decimal incomingDamage,
        string? incomingSourceActorKey = null,
        string? incomingAbilityKey = null,
        string? incomingEffectKey = null)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            target
        );

        var incoming =
            Math.Max(
                0m,
                incomingDamage
            );

        var remaining =
            incoming;

        var absorbed =
            0m;

        var candidates =
            target.ActiveAbsorbs
                .Where(
                    absorb =>
                        absorb.IsActiveAt(
                            context.CurrentTimeSeconds
                        )
                )
                .OrderBy(
                    absorb =>
                        absorb.AppliedAtSeconds
                )
                .ThenBy(
                    absorb =>
                        absorb.AbsorbKey,
                    StringComparer.OrdinalIgnoreCase
                )
                .ThenBy(
                    absorb =>
                        absorb.InstanceId
                )
                .ToList();

        foreach (
            var absorb in
            candidates)
        {
            if (remaining <= 0m)
            {
                break;
            }

            var consumed =
                Math.Min(
                    absorb.RemainingAmount,
                    remaining
                );

            if (consumed <= 0m)
            {
                continue;
            }

            absorb.RemainingAmount -=
                consumed;

            remaining -=
                consumed;

            absorbed +=
                consumed;

            context.EmitEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        context.CurrentTimeSeconds,

                    Type =
                        CombatEventType.AbsorbConsumed,

                    SourceActorKey =
                        absorb.SourceActorKey,

                    TargetActorKey =
                        target.Key,

                    AbilityKey =
                        absorb.AbilityKey,

                    EffectKey =
                        absorb.EffectKey,

                    AbsorbInstanceId =
                        absorb.InstanceId,

                    Amount =
                        consumed,

                    Description =
                        $"{absorb.Name} absorbed {consumed:0.##} damage from {incomingAbilityKey ?? "incoming damage"} on {target.Name}; {absorb.RemainingAmount:0.##} remains."
                }
            );

            if (absorb.RemainingAmount <= 0m)
            {
                RemoveAbsorb(
                    context,
                    target,
                    absorb,
                    "depleted"
                );
            }
        }

        var healthDamage =
            target.TakeDamage(
                remaining
            );

        return new DamageAbsorptionResult
        {
            IncomingDamage =
                incoming,

            AbsorbedDamage =
                absorbed,

            HealthDamage =
                healthDamage
        };
    }

    public bool RemoveAbsorb(
        SimulationContext context,
        SimulationActorState target,
        AbsorbInstance absorb,
        string reason = "removed")
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            target
        );

        ArgumentNullException.ThrowIfNull(
            absorb
        );

        if (!target.ActiveAbsorbs.Remove(
                absorb))
        {
            return false;
        }

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.AbsorbRemoved,

                SourceActorKey =
                    absorb.SourceActorKey,

                TargetActorKey =
                    target.Key,

                AbilityKey =
                    absorb.AbilityKey,

                EffectKey =
                    absorb.EffectKey,

                AbsorbInstanceId =
                    absorb.InstanceId,

                Description =
                    $"{absorb.Name} {reason} on {target.Name}."
            }
        );

        return true;
    }

    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (
            combatEvent.Type !=
                CombatEventType.AbsorbExpiration ||
            !combatEvent.AbsorbInstanceId.HasValue ||
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

        if (target is null)
        {
            return;
        }

        var absorb =
            target.ActiveAbsorbs
                .FirstOrDefault(
                    candidate =>
                        candidate.InstanceId ==
                        combatEvent.AbsorbInstanceId.Value
                );

        if (absorb is null)
        {
            return;
        }

        if (context.CurrentTimeSeconds <
            absorb.ExpiresAtSeconds)
        {
            return;
        }

        RemoveAbsorb(
            context,
            target,
            absorb,
            "expired"
        );
    }

    private static AbsorbInstance CreateInstance(
        string absorbKey,
        string name,
        decimal amount,
        decimal durationSeconds,
        AbsorbStackingMode stackingMode,
        int maxStacks,
        string sourceActorKey,
        string targetActorKey,
        string? abilityKey,
        string? effectKey,
        decimal now)
    {
        return new AbsorbInstance
        {
            InstanceId =
                Guid.NewGuid(),

            AbsorbKey =
                absorbKey,

            Name =
                string.IsNullOrWhiteSpace(
                    name)
                    ? absorbKey
                    : name,

            SourceActorKey =
                sourceActorKey,

            TargetActorKey =
                targetActorKey,

            AbilityKey =
                abilityKey,

            EffectKey =
                effectKey,

            AppliedAtSeconds =
                now,

            ExpiresAtSeconds =
                now +
                durationSeconds,

            MaximumAmount =
                amount,

            RemainingAmount =
                amount,

            Stacks =
                1,

            StackingMode =
                stackingMode,

            MaxStacks =
                maxStacks
        };
    }

    private static void ScheduleExpiration(
        SimulationContext context,
        AbsorbInstance absorb)
    {
        if (absorb.ExpiresAtSeconds <=
            context.CurrentTimeSeconds)
        {
            return;
        }

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    absorb.ExpiresAtSeconds,

                Type =
                    CombatEventType.AbsorbExpiration,

                SourceActorKey =
                    absorb.SourceActorKey,

                TargetActorKey =
                    absorb.TargetActorKey,

                AbilityKey =
                    absorb.AbilityKey,

                EffectKey =
                    absorb.EffectKey,

                AbsorbInstanceId =
                    absorb.InstanceId,

                IsInternal =
                    true,

                Description =
                    $"{absorb.Name} expiration check."
            }
        );
    }
}
