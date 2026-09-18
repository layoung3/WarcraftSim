using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AbilityExecutor : ICombatEventProcessor
{
    private readonly AuraManager _auraManager;

    private readonly ICombatRollResolver _combatRollResolver;

    private readonly IDamageMitigationResolver _damageMitigationResolver;

    public AbilityExecutor(
        AuraManager auraManager,
        ICombatRollResolver combatRollResolver,
        IDamageMitigationResolver damageMitigationResolver)
    {
        _auraManager = auraManager;
        _combatRollResolver = combatRollResolver;
        _damageMitigationResolver = damageMitigationResolver;
    }

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

        if (!source.IsCastReady(
                context.CurrentTimeSeconds))
        {
            return AbilityUseResult.Failed(
                $"{source.Name} is already casting."
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

        var execution =
            new AbilityExecutionState
            {
                SourceActorKey = sourceActorKey,
                TargetActorKey = targetActorKey,
                AbilityKey = ability.Key
            };

        context.AddAbilityExecution(
            execution
        );

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

        source.StartCast(
            context.CurrentTimeSeconds,
            ability.CastTimeSeconds
        );

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

                AbilityExecutionId =
                    execution.Id,

                Description =
                    $"{source.Name} started casting {ability.Name}."
            }
        );

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds +
                    Math.Max(
                        0m,
                        ability.CastTimeSeconds
                    ),

                Type =
                    CombatEventType.AbilityCastCompleted,

                SourceActorKey =
                    sourceActorKey,

                TargetActorKey =
                    targetActorKey,

                AbilityKey =
                    ability.Key,

                AbilityExecutionId =
                    execution.Id,

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

            case CombatEventType.AbilityEffectImpact:
                ProcessAbilityEffectImpact(
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

    private void ProcessAbilityCompletion(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        var source =
            GetSourceActor(
                context,
                combatEvent
            );

        var target =
            GetTargetActor(
                context,
                combatEvent
            );

        if (
            source is null ||
            target is null ||
            !source.IsAlive ||
            !target.IsAlive ||
            string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey) ||
            !combatEvent.AbilityExecutionId.HasValue
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

        var ability =
            abilityState.Definition;

        if (!TrySpendResourceCosts(
                context,
                source,
                ability))
        {
            return;
        }

        var orderedEffects =
            OrderEffectsByDependencies(
                ability.Effects
            );

        var effectLookup =
            ability.Effects
                .Where(effect =>
                    !string.IsNullOrWhiteSpace(
                        effect.Key))
                .ToDictionary(
                    effect => effect.Key,
                    StringComparer.OrdinalIgnoreCase
                );

        foreach (var effect in orderedEffects)
        {
            var travelTime =
                GetEffectiveTravelTime(
                    effect,
                    effectLookup,
                    []
                );

            if (travelTime <= 0m)
            {
                ApplyEffect(
                    context,
                    source,
                    target,
                    ability,
                    effect,
                    combatEvent.AbilityExecutionId.Value
                );

                continue;
            }

            context.ScheduleEvent(
                new CombatEvent
                {
                    TimeSeconds =
                        context.CurrentTimeSeconds +
                        travelTime,

                    Type =
                        CombatEventType.AbilityEffectImpact,

                    SourceActorKey =
                        source.Key,

                    TargetActorKey =
                        target.Key,

                    AbilityKey =
                        ability.Key,

                    AbilityExecutionId =
                        combatEvent.AbilityExecutionId,

                    EffectKey =
                        effect.Key,

                    SchoolKey =
                        effect.SchoolKey,

                    IsInternal =
                        true,

                    Description =
                        $"{ability.Name} effect {effect.Key} is traveling."
                }
            );
        }
    }

    private void ProcessAbilityEffectImpact(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        var source =
            GetSourceActor(
                context,
                combatEvent
            );

        var target =
            GetTargetActor(
                context,
                combatEvent
            );

        if (
            source is null ||
            target is null ||
            !source.IsAlive ||
            !target.IsAlive ||
            string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey) ||
            string.IsNullOrWhiteSpace(
                combatEvent.EffectKey) ||
            !combatEvent.AbilityExecutionId.HasValue
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

        ApplyEffect(
            context,
            source,
            target,
            abilityState.Definition,
            effect,
            combatEvent.AbilityExecutionId.Value
        );
    }

    private void ApplyEffect(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        Guid abilityExecutionId)
    {
        if (!IsDependencySatisfied(
                context,
                abilityExecutionId,
                effect))
        {
            return;
        }

        switch (effect.EffectType)
        {
            case AbilityEffectTypes.DirectDamage:
                ApplyDamage(
                    context,
                    source,
                    target,
                    ability,
                    effect,
                    abilityExecutionId,
                    isPeriodic: false
                );
                break;

            case AbilityEffectTypes.DirectHealing:
                ApplyHealing(
                    context,
                    source,
                    target,
                    ability,
                    effect,
                    abilityExecutionId,
                    isPeriodic: false
                );
                break;

            case AbilityEffectTypes.PeriodicDamage:
            case AbilityEffectTypes.PeriodicHealing:
                ApplyPeriodicEffect(
                    context,
                    source,
                    target,
                    ability,
                    effect,
                    abilityExecutionId
                );

                context.RecordAbilityEffectResult(
                    abilityExecutionId,
                    effect.Key,
                    CombatRollResult.Hit()
                );
                break;
        }
    }

    private void ApplyPeriodicEffect(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        Guid abilityExecutionId)
    {
        var duration =
            Math.Max(
                0m,
                effect.DurationSeconds ?? 0m
            );

        var tickInterval =
            Math.Max(
                0m,
                effect.TickIntervalSeconds ?? 0m
            );

        if (
            duration <= 0m ||
            tickInterval <= 0m)
        {
            return;
        }

        var auraDefinition =
            new AuraDefinition
            {
                Key =
                    string.IsNullOrWhiteSpace(
                        effect.AuraKey)
                        ? effect.Key
                        : effect.AuraKey,

                Name =
                    string.IsNullOrWhiteSpace(
                        effect.AuraKey)
                        ? effect.Key
                        : effect.AuraKey,

                DurationSeconds =
                    duration,

                StackingMode =
                    effect.AuraStackingMode,

                MaxStacks =
                    Math.Max(
                        1,
                        effect.MaxStacks
                    )
            };

        var aura =
            _auraManager.ApplyAura(
                context,
                target,
                auraDefinition,
                source.Key,
                ability.Key,
                effect.Key,
                scheduleExpiration: false
            );

        SchedulePeriodicTicks(
            context,
            source,
            target,
            ability,
            effect,
            aura,
            abilityExecutionId
        );

        _auraManager.ScheduleExpiration(
            context,
            aura
        );
    }

    private static void SchedulePeriodicTicks(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        AuraInstance aura,
        Guid abilityExecutionId)
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
                        $"{effect.Key} periodic tick."
                }
            );
        }
    }

    private void ProcessPeriodicTick(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        var source =
            GetSourceActor(
                context,
                combatEvent
            );

        var target =
            GetTargetActor(
                context,
                combatEvent
            );

        if (
            source is null ||
            target is null ||
            !source.IsAlive ||
            !target.IsAlive ||
            string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey) ||
            string.IsNullOrWhiteSpace(
                combatEvent.EffectKey) ||
            !combatEvent.AuraInstanceId.HasValue
        )
        {
            return;
        }

        var aura =
            target.ActiveAuras
                .FirstOrDefault(candidate =>
                    candidate.InstanceId ==
                    combatEvent.AuraInstanceId.Value
                );

        if (
            aura is null ||
            !aura.IsActiveAt(
                context.CurrentTimeSeconds))
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
                    combatEvent.AbilityExecutionId,
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
                    combatEvent.AbilityExecutionId,
                    isPeriodic: true
                );
                break;
        }
    }

    private void ApplyDamage(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        Guid? abilityExecutionId,
        bool isPeriodic)
    {
        var roll =
            _combatRollResolver.Resolve(
                context,
                source,
                target,
                ability,
                effect
            );

        if (
            abilityExecutionId.HasValue &&
            !isPeriodic)
        {
            context.RecordAbilityEffectResult(
                abilityExecutionId.Value,
                effect.Key,
                roll
            );
        }

        if (!roll.Landed)
        {
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

                    AbilityExecutionId =
                        abilityExecutionId,

                    EffectKey =
                        effect.Key,

                    SchoolKey =
                        effect.SchoolKey,

                    ResultKey =
                        roll.ResultKey,

                    RawAmount = 0m,

                    MitigatedAmount = 0m,

                    MitigationPercent = 0m,

                    Amount = 0m,

                    IsCritical = false,

                    IsPeriodic =
                        isPeriodic,

                    Description =
                        $"{ability.Name} missed {target.Name}."
                }
            );

            return;
        }

        var baseAmount =
            RollEffectValue(
                context,
                source,
                effect
            );

        var rawAmount =
            baseAmount *
            roll.AmountMultiplier;

        var mitigation =
            _damageMitigationResolver.Resolve(
                context,
                source,
                target,
                ability,
                effect,
                rawAmount
            );

        var targetWasAlive =
            target.IsAlive;

        var actualDamage =
            target.TakeDamage(
                mitigation.FinalAmount
            );

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

                AbilityExecutionId =
                    abilityExecutionId,

                EffectKey =
                    effect.Key,

                SchoolKey =
                    effect.SchoolKey,

                ResultKey =
                    roll.ResultKey,

                RawAmount =
                    mitigation.RawAmount,

                MitigatedAmount =
                    mitigation.MitigatedAmount,

                MitigationPercent =
                    mitigation.ReductionPercent,

                Amount =
                    actualDamage,

                IsCritical =
                    roll.IsCritical,

                IsPeriodic =
                    isPeriodic,

                Description =
                    roll.IsCritical
                        ? $"{ability.Name} critically hit {target.Name} for {actualDamage:0.##} damage."
                        : $"{ability.Name} dealt {actualDamage:0.##} damage to {target.Name}."
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

                    AbilityExecutionId =
                        abilityExecutionId,

                    EffectKey =
                        effect.Key,

                    Description =
                        $"{target.Name} died."
                }
            );
        }
    }

    private void ApplyHealing(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        Guid? abilityExecutionId,
        bool isPeriodic)
    {
        var roll =
            _combatRollResolver.Resolve(
                context,
                source,
                target,
                ability,
                effect
            );

        if (
            abilityExecutionId.HasValue &&
            !isPeriodic)
        {
            context.RecordAbilityEffectResult(
                abilityExecutionId.Value,
                effect.Key,
                roll
            );
        }

        if (!roll.Landed)
        {
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

                    AbilityExecutionId =
                        abilityExecutionId,

                    EffectKey =
                        effect.Key,

                    SchoolKey =
                        effect.SchoolKey,

                    ResultKey =
                        roll.ResultKey,

                    RawAmount = 0m,

                    Amount = 0m,

                    IsPeriodic =
                        isPeriodic,

                    Description =
                        $"{ability.Name} failed to affect {target.Name}."
                }
            );

            return;
        }

        var baseAmount =
            RollEffectValue(
                context,
                source,
                effect
            );

        var rawAmount =
            baseAmount *
            roll.AmountMultiplier;

        var effectiveHealing =
            target.Heal(
                rawAmount
            );

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

                AbilityExecutionId =
                    abilityExecutionId,

                EffectKey =
                    effect.Key,

                SchoolKey =
                    effect.SchoolKey,

                ResultKey =
                    roll.ResultKey,

                RawAmount =
                    rawAmount,

                Amount =
                    effectiveHealing,

                OverhealingAmount =
                    overhealing,

                IsCritical =
                    roll.IsCritical,

                IsPeriodic =
                    isPeriodic,

                Description =
                    roll.IsCritical
                        ? $"{ability.Name} critically healed {target.Name} for {effectiveHealing:0.##}."
                        : $"{ability.Name} healed {target.Name} for {effectiveHealing:0.##}."
            }
        );
    }

    private static bool IsDependencySatisfied(
        SimulationContext context,
        Guid abilityExecutionId,
        AbilityEffectDefinition effect)
    {
        if (string.IsNullOrWhiteSpace(
                effect.DependsOnEffectKey))
        {
            return true;
        }

        if (!context.TryGetAbilityEffectResult(
                abilityExecutionId,
                effect.DependsOnEffectKey,
                out var dependencyResult) ||
            dependencyResult is null)
        {
            return false;
        }

        var condition =
            string.IsNullOrWhiteSpace(
                effect.DependencyCondition)
                ? EffectDependencyConditions.Landed
                : effect.DependencyCondition;

        return condition.ToLowerInvariant() switch
        {
            EffectDependencyConditions.Landed =>
                dependencyResult.Landed,

            EffectDependencyConditions.Critical =>
                dependencyResult.IsCritical,

            EffectDependencyConditions.Missed =>
                !dependencyResult.Landed &&
                string.Equals(
                    dependencyResult.ResultKey,
                    CombatResultTypes.Miss,
                    StringComparison.OrdinalIgnoreCase
                ),

            _ => false
        };
    }

    private static IReadOnlyList<AbilityEffectDefinition>
        OrderEffectsByDependencies(
            IReadOnlyList<AbilityEffectDefinition> effects)
    {
        var lookup =
            effects
                .Where(effect =>
                    !string.IsNullOrWhiteSpace(
                        effect.Key))
                .ToDictionary(
                    effect => effect.Key,
                    StringComparer.OrdinalIgnoreCase
                );

        var ordered =
            new List<AbilityEffectDefinition>();

        var visited =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        var visiting =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (var effect in effects)
        {
            Visit(
                effect,
                lookup,
                ordered,
                visited,
                visiting
            );
        }

        return ordered;
    }

    private static void Visit(
        AbilityEffectDefinition effect,
        Dictionary<string, AbilityEffectDefinition> lookup,
        List<AbilityEffectDefinition> ordered,
        HashSet<string> visited,
        HashSet<string> visiting)
    {
        if (visited.Contains(effect.Key))
        {
            return;
        }

        if (!visiting.Add(effect.Key))
        {
            throw new InvalidOperationException(
                $"Effect dependency cycle detected at '{effect.Key}'."
            );
        }

        if (
            !string.IsNullOrWhiteSpace(
                effect.DependsOnEffectKey) &&
            lookup.TryGetValue(
                effect.DependsOnEffectKey,
                out var dependencyEffect)
        )
        {
            Visit(
                dependencyEffect,
                lookup,
                ordered,
                visited,
                visiting
            );
        }

        visiting.Remove(effect.Key);
        visited.Add(effect.Key);
        ordered.Add(effect);
    }

    private static decimal GetEffectiveTravelTime(
        AbilityEffectDefinition effect,
        Dictionary<string, AbilityEffectDefinition> lookup,
        HashSet<string> visiting)
    {
        if (!visiting.Add(effect.Key))
        {
            throw new InvalidOperationException(
                $"Effect dependency cycle detected at '{effect.Key}'."
            );
        }

        var effectiveTravelTime =
            Math.Max(
                0m,
                effect.TravelTimeSeconds
            );

        if (
            !string.IsNullOrWhiteSpace(
                effect.DependsOnEffectKey) &&
            lookup.TryGetValue(
                effect.DependsOnEffectKey,
                out var dependencyEffect)
        )
        {
            effectiveTravelTime =
                Math.Max(
                    effectiveTravelTime,
                    GetEffectiveTravelTime(
                        dependencyEffect,
                        lookup,
                        visiting
                    )
                );
        }

        visiting.Remove(effect.Key);

        return effectiveTravelTime;
    }

    private static bool TrySpendResourceCosts(
        SimulationContext context,
        SimulationActorState source,
        AbilityDefinition ability)
    {
        foreach (var resourceCost in ability.ResourceCosts)
        {
            if (!source.Resources.TryGetValue(
                    resourceCost.ResourceKey,
                    out var resource))
            {
                return false;
            }

            var cost =
                GetResourceCost(
                    resourceCost,
                    resource
                );

            if (!resource.CanSpend(cost))
            {
                return false;
            }
        }

        foreach (var resourceCost in ability.ResourceCosts)
        {
            var resource =
                source.Resources[
                    resourceCost.ResourceKey];

            var cost =
                GetResourceCost(
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
                        source.Key,

                    TargetActorKey =
                        source.Key,

                    AbilityKey =
                        ability.Key,

                    Amount =
                        -cost,

                    Description =
                        $"{source.Name} spent {cost:0.##} {resource.ResourceKey}."
                }
            );
        }

        return true;
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
