using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AbilityExecutor : ICombatEventProcessor
{
    public AbilityUseResult TryStartAbility(
        SimulationContext context,
        string sourceActorKey,
        string targetActorKey,
        string abilityKey)
    {
        var source = context.GetActor(sourceActorKey);

        if (source is null)
        {
            return AbilityUseResult.Failed(
                $"Source actor '{sourceActorKey}' was not found."
            );
        }

        if (!source.IsAlive)
        {
            return AbilityUseResult.Failed(
                $"{source.Name} is dead."
            );
        }

        var target = context.GetActor(targetActorKey);

        if (target is null)
        {
            return AbilityUseResult.Failed(
                $"Target actor '{targetActorKey}' was not found."
            );
        }

        if (!target.IsAlive)
        {
            return AbilityUseResult.Failed(
                $"{target.Name} is dead."
            );
        }

        if (!source.Abilities.TryGetValue(
                abilityKey,
                out var abilityState))
        {
            return AbilityUseResult.Failed(
                $"{source.Name} does not have ability '{abilityKey}'."
            );
        }

        var ability = abilityState.Definition;

        if (!abilityState.IsReady(
                context.CurrentTimeSeconds))
        {
            return AbilityUseResult.Failed(
                $"{ability.Name} is on cooldown."
            );
        }

        if (
            !ability.IsOffGlobalCooldown &&
            !source.IsGlobalCooldownReady(
                context.CurrentTimeSeconds)
        )
        {
            return AbilityUseResult.Failed(
                "Global cooldown is active."
            );
        }

        foreach (var resourceCost in ability.ResourceCosts)
        {
            if (!source.Resources.TryGetValue(
                    resourceCost.ResourceKey,
                    out var resource))
            {
                return AbilityUseResult.Failed(
                    $"Required resource '{resourceCost.ResourceKey}' was not found."
                );
            }

            var cost = GetResourceCost(
                resourceCost,
                resource
            );

            if (!resource.CanSpend(cost))
            {
                return AbilityUseResult.Failed(
                    $"Not enough {resourceCost.ResourceKey}."
                );
            }
        }

        // All validation passed. Now actually commit the action.

        foreach (var resourceCost in ability.ResourceCosts)
        {
            var resource =
                source.Resources[resourceCost.ResourceKey];

            var cost = GetResourceCost(
                resourceCost,
                resource
            );

            resource.Spend(cost);

            context.RecordEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        context.CurrentTimeSeconds,

                    Type =
                        CombatEventType.ResourceChanged,

                    SourceActorKey =
                        sourceActorKey,

                    TargetActorKey =
                        sourceActorKey,

                    AbilityKey =
                        ability.Key,

                    Amount =
                        -cost,

                    Description =
                        $"{source.Name} spent {cost:0.##} {resource.ResourceKey}."
                }
            );
        }

        abilityState.ConsumeCharge(
            context.CurrentTimeSeconds
        );

        if (!ability.IsOffGlobalCooldown)
        {
            source.StartGlobalCooldown(
                context.CurrentTimeSeconds,
                ability.GlobalCooldownSeconds
            );
        }

        context.RecordEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.AbilityCastStarted,

                SourceActorKey =
                    sourceActorKey,

                TargetActorKey =
                    targetActorKey,

                AbilityKey =
                    ability.Key,

                Description =
                    $"{source.Name} started casting {ability.Name}."
            }
        );

        var completionTime =
            context.CurrentTimeSeconds +
            Math.Max(
                0m,
                ability.CastTimeSeconds
            );

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    completionTime,

                Type =
                    CombatEventType.AbilityCastCompleted,

                SourceActorKey =
                    sourceActorKey,

                TargetActorKey =
                    targetActorKey,

                AbilityKey =
                    ability.Key,

                Description =
                    $"{source.Name} completed {ability.Name}."
            }
        );

        return AbilityUseResult.Succeeded();
    }

    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        switch (combatEvent.Type)
        {
            case CombatEventType.AbilityCastCompleted:
                ProcessAbilityCompletion(
                    context,
                    combatEvent
                );
                break;

            case CombatEventType.PeriodicTick:
                ProcessPeriodicTick(
                    context,
                    combatEvent
                );
                break;
        }
    }

    private static void ProcessAbilityCompletion(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        var source =
            GetSourceActor(context, combatEvent);

        var target =
            GetTargetActor(context, combatEvent);

        if (
            source is null ||
            target is null ||
            !source.IsAlive ||
            !target.IsAlive ||
            string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey)
        )
        {
            return;
        }

        if (!source.Abilities.TryGetValue(
                combatEvent.AbilityKey,
                out var abilityState))
        {
            return;
        }

        foreach (
            var effect in
            abilityState.Definition.Effects)
        {
            switch (effect.EffectType)
            {
                case AbilityEffectTypes.DirectDamage:
                    ApplyDamage(
                        context,
                        source,
                        target,
                        abilityState.Definition,
                        effect,
                        isPeriodic: false
                    );
                    break;

                case AbilityEffectTypes.DirectHealing:
                    ApplyHealing(
                        context,
                        source,
                        target,
                        abilityState.Definition,
                        effect,
                        isPeriodic: false
                    );
                    break;

                case AbilityEffectTypes.PeriodicDamage:
                case AbilityEffectTypes.PeriodicHealing:
                    SchedulePeriodicTicks(
                        context,
                        combatEvent,
                        effect
                    );
                    break;
            }
        }
    }

    private static void ProcessPeriodicTick(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        var source =
            GetSourceActor(context, combatEvent);

        var target =
            GetTargetActor(context, combatEvent);

        if (
            source is null ||
            target is null ||
            !source.IsAlive ||
            !target.IsAlive ||
            string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey) ||
            string.IsNullOrWhiteSpace(
                combatEvent.EffectKey)
        )
        {
            return;
        }

        if (!source.Abilities.TryGetValue(
                combatEvent.AbilityKey,
                out var abilityState))
        {
            return;
        }

        var effect =
            abilityState.Definition.Effects
                .FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Key,
                        combatEvent.EffectKey,
                        StringComparison.OrdinalIgnoreCase
                    )
                );

        if (effect is null)
        {
            return;
        }

        switch (effect.EffectType)
        {
            case AbilityEffectTypes.PeriodicDamage:
                ApplyDamage(
                    context,
                    source,
                    target,
                    abilityState.Definition,
                    effect,
                    isPeriodic: true
                );
                break;

            case AbilityEffectTypes.PeriodicHealing:
                ApplyHealing(
                    context,
                    source,
                    target,
                    abilityState.Definition,
                    effect,
                    isPeriodic: true
                );
                break;
        }
    }

    private static void SchedulePeriodicTicks(
        SimulationContext context,
        CombatEvent castEvent,
        AbilityEffectDefinition effect)
    {
        var duration =
            effect.DurationSeconds ?? 0m;

        var tickInterval =
            effect.TickIntervalSeconds ?? 0m;

        if (
            duration <= 0m ||
            tickInterval <= 0m)
        {
            return;
        }

        var tickCount =
            (int)Math.Floor(
                duration / tickInterval
            );

        for (
            var tickNumber = 1;
            tickNumber <= tickCount;
            tickNumber++)
        {
            context.ScheduleEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        context.CurrentTimeSeconds +
                        tickInterval * tickNumber,

                    Type =
                        CombatEventType.PeriodicTick,

                    SourceActorKey =
                        castEvent.SourceActorKey,

                    TargetActorKey =
                        castEvent.TargetActorKey,

                    AbilityKey =
                        castEvent.AbilityKey,

                    EffectKey =
                        effect.Key,

                    SchoolKey =
                        effect.SchoolKey,

                    IsPeriodic =
                        true,

                    Description =
                        $"{effect.Key} periodic tick."
                }
            );
        }
    }

    private static void ApplyDamage(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        bool isPeriodic)
    {
        var rawAmount =
            RollEffectValue(
                context,
                source,
                effect
            );

        var targetWasAlive =
            target.IsAlive;

        // No mitigation system yet, so raw damage
        // and final damage are currently equal.
        var actualDamage =
            target.TakeDamage(rawAmount);

        context.RecordEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.Damage,

                SourceActorKey =
                    source.Key,

                TargetActorKey =
                    target.Key,

                AbilityKey =
                    ability.Key,

                EffectKey =
                    effect.Key,

                SchoolKey =
                    effect.SchoolKey,

                RawAmount =
                    rawAmount,

                Amount =
                    actualDamage,

                IsPeriodic =
                    isPeriodic,

                Description =
                    $"{ability.Name} dealt {actualDamage:0.##} damage to {target.Name}."
            }
        );

        if (
            targetWasAlive &&
            !target.IsAlive)
        {
            context.RecordEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        context.CurrentTimeSeconds,

                    Type =
                        CombatEventType.ActorDied,

                    SourceActorKey =
                        source.Key,

                    TargetActorKey =
                        target.Key,

                    AbilityKey =
                        ability.Key,

                    EffectKey =
                        effect.Key,

                    Description =
                        $"{target.Name} died."
                }
            );
        }
    }

    private static void ApplyHealing(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        bool isPeriodic)
    {
        var rawAmount =
            RollEffectValue(
                context,
                source,
                effect
            );

        var effectiveHealing =
            target.Heal(rawAmount);

        var overhealing =
            Math.Max(
                0m,
                rawAmount - effectiveHealing
            );

        context.RecordEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.Healing,

                SourceActorKey =
                    source.Key,

                TargetActorKey =
                    target.Key,

                AbilityKey =
                    ability.Key,

                EffectKey =
                    effect.Key,

                SchoolKey =
                    effect.SchoolKey,

                RawAmount =
                    rawAmount,

                Amount =
                    effectiveHealing,

                OverhealingAmount =
                    overhealing,

                IsPeriodic =
                    isPeriodic,

                Description =
                    $"{ability.Name} healed {target.Name} for {effectiveHealing:0.##}."
            }
        );
    }

    private static decimal RollEffectValue(
        SimulationContext context,
        SimulationActorState source,
        AbilityEffectDefinition effect)
    {
        var minimum =
            Math.Min(
                effect.MinimumValue,
                effect.MaximumValue
            );

        var maximum =
            Math.Max(
                effect.MinimumValue,
                effect.MaximumValue
            );

        decimal rolledValue;

        if (minimum == maximum)
        {
            rolledValue = minimum;
        }
        else
        {
            var randomValue =
                (decimal)context.Random.NextDouble();

            rolledValue =
                minimum +
                (maximum - minimum) *
                randomValue;
        }

        if (
            !string.IsNullOrWhiteSpace(
                effect.ScalingStatKey) &&
            effect.ScalingCoefficient != 0m)
        {
            rolledValue +=
                source.Stats.Get(
                    effect.ScalingStatKey
                ) *
                effect.ScalingCoefficient;
        }

        return Math.Max(
            0m,
            rolledValue
        );
    }

    private static decimal GetResourceCost(
        AbilityResourceCost resourceCost,
        ResourceState resource)
    {
        if (!resourceCost.IsPercentOfMaximum)
        {
            return Math.Max(
                0m,
                resourceCost.Amount
            );
        }

        return Math.Max(
            0m,
            resource.Maximum *
            resourceCost.Amount /
            100m
        );
    }

    private static SimulationActorState?
        GetSourceActor(
            SimulationContext context,
            CombatEvent combatEvent)
    {
        if (string.IsNullOrWhiteSpace(
                combatEvent.SourceActorKey))
        {
            return null;
        }

        return context.GetActor(
            combatEvent.SourceActorKey
        );
    }

    private static SimulationActorState?
        GetTargetActor(
            SimulationContext context,
            CombatEvent combatEvent)
    {
        if (string.IsNullOrWhiteSpace(
                combatEvent.TargetActorKey))
        {
            return null;
        }

        return context.GetActor(
            combatEvent.TargetActorKey
        );
    }
}