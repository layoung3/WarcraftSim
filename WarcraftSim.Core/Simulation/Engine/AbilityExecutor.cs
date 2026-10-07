using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AbilityExecutor : ICombatEventProcessor
{
    private readonly AuraManager _auraManager;

    private readonly ICombatRollResolver _combatRollResolver;

    private readonly IDamageMitigationResolver _damageMitigationResolver;

    private readonly ForcedTargetManager _forcedTargetManager;

    private readonly AbsorbManager _absorbManager;

    public AbilityExecutor(
        AuraManager auraManager,
        ICombatRollResolver combatRollResolver,
        IDamageMitigationResolver damageMitigationResolver,
        ForcedTargetManager? forcedTargetManager = null,
        AbsorbManager? absorbManager = null)
    {
        _auraManager = auraManager;
        _combatRollResolver = combatRollResolver;
        _damageMitigationResolver = damageMitigationResolver;
        _forcedTargetManager =
            forcedTargetManager ??
            new ForcedTargetManager();

        _absorbManager =
            absorbManager ??
            new AbsorbManager();
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

        if (!source.IsInputReady(
                context.CurrentTimeSeconds))
        {
            return AbilityUseResult.Failed(
                $"{source.Name} is not ready for another action."
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

        var totalActionDurationSeconds =
            Math.Max(
                0m,
                ability.CastTimeSeconds
            ) +
            Math.Max(
                0m,
                ability.ChannelDurationSeconds
            );

        source.StartCast(
            context.CurrentTimeSeconds,
            totalActionDurationSeconds
        );

        source.TrackCurrentCast(
            execution.Id,
            ability.Key,
            totalActionDurationSeconds
        );

        source.RegisterActionStarted(
            context.CurrentTimeSeconds,
            totalActionDurationSeconds,
            ability.IsOffGlobalCooldown
                ? 0m
                : ability.GlobalCooldownSeconds
        );

        context.EmitEvent(
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

    public bool TryCancelCurrentCast(
        SimulationContext context,
        string sourceActorKey)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        var source =
            context.GetActor(
                sourceActorKey
            );

        if (
            source is null ||
            !source.CurrentCastExecutionId.HasValue
        )
        {
            return false;
        }

        var executionId =
            source.CurrentCastExecutionId.Value;

        var abilityKey =
            source.CurrentCastAbilityKey;

        var execution =
            context.GetAbilityExecution(
                executionId
            );

        var wasChanneling =
            execution?.IsChanneling ==
            true;

        var cancelledExecutionId =
            source.CancelCurrentCast(
                context.CurrentTimeSeconds
            );

        if (!cancelledExecutionId.HasValue)
        {
            return false;
        }

        context.CancelAbilityExecution(
            cancelledExecutionId.Value,
            context.CurrentTimeSeconds
        );

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    wasChanneling
                        ? CombatEventType.AbilityChannelCancelled
                        : CombatEventType.AbilityCastCancelled,

                SourceActorKey =
                    source.Key,

                TargetActorKey =
                    execution?.TargetActorKey,

                AbilityKey =
                    abilityKey,

                AbilityExecutionId =
                    cancelledExecutionId,

                Description =
                    wasChanneling
                        ? $"{source.Name} cancelled channeling {abilityKey}."
                        : $"{source.Name} cancelled {abilityKey}."
            }
        );

        return true;
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

            case CombatEventType.ForcedTargetExpiration:
                _forcedTargetManager.Process(
                    context,
                    combatEvent
                );
                break;

            case CombatEventType.AbsorbExpiration:
                _absorbManager.Process(
                    context,
                    combatEvent
                );
                break;

            case CombatEventType.AbilityChannelTick:
                ProcessAbilityChannelTick(
                    context,
                    combatEvent
                );
                break;

            case CombatEventType.AbilityChannelCompleted:
                if (combatEvent.IsInternal)
                {
                    ProcessAbilityChannelCompletion(
                        context,
                        combatEvent
                    );
                }
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

        var execution =
            context.GetAbilityExecution(
                combatEvent.AbilityExecutionId.Value
            );

        if (
            execution is null ||
            execution.IsCancelled)
        {
            return;
        }

        if (!ability.IsChanneled)
        {
            source.CompleteCurrentCast(
                combatEvent.AbilityExecutionId
            );
        }

        if (!TrySpendResourceCosts(
                context,
                source,
                ability))
        {
            if (ability.IsChanneled)
            {
                source.CancelCurrentCast(
                    context.CurrentTimeSeconds
                );

                context.CancelAbilityExecution(
                    combatEvent.AbilityExecutionId.Value,
                    context.CurrentTimeSeconds
                );
            }

            return;
        }

        if (ability.IsChanneled)
        {
            StartChannel(
                context,
                source,
                target,
                ability,
                execution
            );
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
            if (
                ability.IsChanneled &&
                effect.ApplyOnChannelTick)
            {
                continue;
            }

            var effectTargets =
                EffectTargetResolver.Resolve(
                    context,
                    source,
                    target,
                    effect
                );

            if (effectTargets.Count == 0)
            {
                continue;
            }

            var travelTime =
                GetEffectiveTravelTime(
                    effect,
                    effectLookup,
                    []
                );

            foreach (
                var effectTarget in
                effectTargets)
            {
                if (travelTime <= 0m)
                {
                    ApplyEffect(
                        context,
                        source,
                        effectTarget,
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
                            effectTarget.Key,

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
                            $"{ability.Name} effect {effect.Key} is traveling to {effectTarget.Name}."
                    }
                );
            }
        }
    }

    private void StartChannel(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityExecutionState execution)
    {
        execution.StartChannel(
            context.CurrentTimeSeconds,
            ability.ChannelDurationSeconds
        );

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.AbilityChannelStarted,

                SourceActorKey =
                    source.Key,

                TargetActorKey =
                    target.Key,

                AbilityKey =
                    ability.Key,

                AbilityExecutionId =
                    execution.Id,

                Description =
                    $"{source.Name} started channeling {ability.Name}."
            }
        );

        var tickCount =
            (int)Math.Floor(
                ability.ChannelDurationSeconds /
                ability.ChannelTickIntervalSeconds
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
                        ability.ChannelTickIntervalSeconds *
                        tickNumber,

                    Type =
                        CombatEventType.AbilityChannelTick,

                    SourceActorKey =
                        source.Key,

                    TargetActorKey =
                        target.Key,

                    AbilityKey =
                        ability.Key,

                    AbilityExecutionId =
                        execution.Id,

                    IsInternal =
                        true,

                    Description =
                        $"{ability.Name} channel tick {tickNumber}."
                }
            );
        }

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds +
                    ability.ChannelDurationSeconds,

                Type =
                    CombatEventType.AbilityChannelCompleted,

                SourceActorKey =
                    source.Key,

                TargetActorKey =
                    target.Key,

                AbilityKey =
                    ability.Key,

                AbilityExecutionId =
                    execution.Id,

                IsInternal =
                    true,

                Description =
                    $"{ability.Name} channel completion check."
            }
        );
    }

    private void ProcessAbilityChannelTick(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (
            !combatEvent.AbilityExecutionId.HasValue ||
            string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey))
        {
            return;
        }

        var execution =
            context.GetAbilityExecution(
                combatEvent.AbilityExecutionId.Value
            );

        if (
            execution is null ||
            execution.IsCancelled ||
            !execution.IsChanneling)
        {
            return;
        }

        var source =
            context.GetActor(
                execution.SourceActorKey
            );

        var primaryTarget =
            context.GetActor(
                execution.TargetActorKey
            );

        if (
            source is null ||
            primaryTarget is null ||
            !source.IsAlive ||
            !source.Abilities.TryGetValue(
                combatEvent.AbilityKey,
                out var abilityState))
        {
            return;
        }

        var ability =
            abilityState.Definition;

        var channelEffects =
            OrderEffectsByDependencies(
                ability.Effects
                    .Where(
                        effect =>
                            effect.ApplyOnChannelTick
                    )
                    .ToList()
            );

        foreach (
            var effect in
            channelEffects)
        {
            var effectTargets =
                EffectTargetResolver.Resolve(
                    context,
                    source,
                    primaryTarget,
                    effect
                );

            foreach (
                var effectTarget in
                effectTargets)
            {
                ApplyChannelTickEffect(
                    context,
                    source,
                    effectTarget,
                    ability,
                    effect,
                    execution.Id
                );
            }
        }
    }

    private void ApplyChannelTickEffect(
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
                    isPeriodic:
                        true
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
                    isPeriodic:
                        true
                );
                break;

            default:
                ApplyEffect(
                    context,
                    source,
                    target,
                    ability,
                    effect,
                    abilityExecutionId
                );
                break;
        }
    }

    private void ProcessAbilityChannelCompletion(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (!combatEvent.AbilityExecutionId.HasValue)
        {
            return;
        }

        var execution =
            context.GetAbilityExecution(
                combatEvent.AbilityExecutionId.Value
            );

        if (
            execution is null ||
            execution.IsCancelled ||
            !execution.IsChanneling)
        {
            return;
        }

        var source =
            context.GetActor(
                execution.SourceActorKey
            );

        if (source is null)
        {
            return;
        }

        source.CompleteCurrentCast(
            execution.Id
        );

        execution.CompleteChannel(
            context.CurrentTimeSeconds
        );

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.AbilityChannelCompleted,

                SourceActorKey =
                    execution.SourceActorKey,

                TargetActorKey =
                    execution.TargetActorKey,

                AbilityKey =
                    execution.AbilityKey,

                AbilityExecutionId =
                    execution.Id,

                Description =
                    $"{source.Name} completed channeling {execution.AbilityKey}."
            }
        );
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

            case AbilityEffectTypes.Threat:
                ApplyThreat(
                    context,
                    source,
                    target,
                    ability,
                    effect,
                    abilityExecutionId
                );
                break;

            case AbilityEffectTypes.Taunt:
                ApplyTaunt(
                    context,
                    source,
                    target,
                    ability,
                    effect,
                    abilityExecutionId
                );
                break;

            case AbilityEffectTypes.ApplyAura:
                ApplyExplicitAura(
                    context,
                    source,
                    target,
                    ability,
                    effect,
                    abilityExecutionId
                );
                break;

            case AbilityEffectTypes.RemoveAura:
                ApplyExplicitAuraRemoval(
                    context,
                    source,
                    target,
                    ability,
                    effect,
                    abilityExecutionId
                );
                break;

            case AbilityEffectTypes.ResourceChange:
                ApplyResourceChange(
                    context,
                    source,
                    target,
                    ability,
                    effect,
                    abilityExecutionId
                );
                break;

            case AbilityEffectTypes.Absorb:
                ApplyAbsorb(
                    context,
                    source,
                    target,
                    ability,
                    effect,
                    abilityExecutionId
                );
                break;
        }
    }

    private void ApplyAbsorb(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        Guid abilityExecutionId)
    {
        var roll =
            _combatRollResolver.Resolve(
                context,
                source,
                target,
                ability,
                effect
            );

        context.RecordAbilityEffectResult(
            abilityExecutionId,
            effect.Key,
            roll
        );

        if (!roll.Landed)
        {
            return;
        }

        var amount =
            RollEffectValue(
                context,
                source,
                effect
            ) *
            roll.AmountMultiplier;

        _absorbManager.ApplyAbsorb(
            context,
            target,
            effect.AbsorbKey!,
            effect.AbsorbKey!,
            amount,
            effect.DurationSeconds!.Value,
            effect.AbsorbStackingMode,
            effect.MaxStacks,
            source.Key,
            ability.Key,
            effect.Key
        );
    }

    private void ApplyResourceChange(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        Guid abilityExecutionId)
    {
        var roll =
            _combatRollResolver.Resolve(
                context,
                source,
                target,
                ability,
                effect
            );

        context.RecordAbilityEffectResult(
            abilityExecutionId,
            effect.Key,
            roll
        );

        if (!roll.Landed)
        {
            return;
        }

        if (!target.Resources.TryGetValue(
                effect.ResourceKey!,
                out var resource))
        {
            throw new InvalidOperationException(
                $"Resource change effect '{effect.Key}' on ability '{ability.Key}' requires resource '{effect.ResourceKey}' on target actor '{target.Key}'."
            );
        }

        var amount =
            RollEffectValue(
                context,
                source,
                effect
            );

        var result =
            ResourceManipulator.Apply(
                resource,
                effect.ResourceChangeOperation,
                amount,
                effect.ResourceAmountIsPercentOfMaximum
            );

        if (result.Delta == 0m)
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
                    source.Key,

                TargetActorKey =
                    target.Key,

                AbilityKey =
                    ability.Key,

                AbilityExecutionId =
                    abilityExecutionId,

                EffectKey =
                    effect.Key,

                Amount =
                    result.Delta,

                Description =
                    $"{ability.Name} changed {target.Name}'s {resource.ResourceKey} from {result.PreviousValue} to {result.CurrentValue} using '{effect.ResourceChangeOperation}'."
            }
        );
    }

    private void ApplyExplicitAura(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        Guid abilityExecutionId)
    {
        var roll =
            _combatRollResolver.Resolve(
                context,
                source,
                target,
                ability,
                effect
            );

        context.RecordAbilityEffectResult(
            abilityExecutionId,
            effect.Key,
            roll
        );

        if (!roll.Landed)
        {
            return;
        }

        var auraDefinition =
            new AuraDefinition
            {
                Key =
                    effect.AuraKey!,

                Name =
                    effect.AuraKey!,

                DurationSeconds =
                    effect.DurationSeconds!.Value,

                StackingMode =
                    effect.AuraStackingMode,

                MaxStacks =
                    effect.MaxStacks,

                Tags =
                    effect.Tags.ToList()
            };

        _auraManager.ApplyAura(
            context,
            target,
            auraDefinition,
            source.Key,
            ability.Key,
            effect.Key
        );
    }

    private void ApplyExplicitAuraRemoval(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        Guid abilityExecutionId)
    {
        var roll =
            _combatRollResolver.Resolve(
                context,
                source,
                target,
                ability,
                effect
            );

        context.RecordAbilityEffectResult(
            abilityExecutionId,
            effect.Key,
            roll
        );

        if (!roll.Landed)
        {
            return;
        }

        _auraManager.RemoveAuras(
            context,
            target,
            effect.AuraKey!,
            effect.RemoveAuraOnlyFromSource
                ? source.Key
                : null,
            $"removed by {ability.Name}"
        );
    }

    private void ApplyTaunt(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        Guid abilityExecutionId)
    {
        var roll =
            _combatRollResolver.Resolve(
                context,
                source,
                target,
                ability,
                effect
            );

        context.RecordAbilityEffectResult(
            abilityExecutionId,
            effect.Key,
            roll
        );

        if (!roll.Landed)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(
                effect.TauntThreatOperation))
        {
            var amount =
                string.Equals(
                    effect.TauntThreatOperation,
                    ThreatManipulationOperationTypes.MatchHighest,
                    StringComparison.OrdinalIgnoreCase)
                    ? 0m
                    : RollEffectValue(
                        context,
                        source,
                        effect
                    );

            var threatResult =
                ThreatManipulator.Apply(
                    context,
                    target,
                    source,
                    effect.TauntThreatOperation,
                    amount
                );

            if (threatResult.Delta != 0m)
            {
                context.EmitEvent(
                    new CombatEvent
                    {
                        TimeSeconds =
                            context.CurrentTimeSeconds,

                        Type =
                            CombatEventType.ThreatChanged,

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

                        Amount =
                            threatResult.Delta,

                        Description =
                            $"{ability.Name} changed {source.Name}'s threat on {target.Name} from {threatResult.PreviousThreat} to {threatResult.CurrentThreat} using taunt operation '{effect.TauntThreatOperation}'."
                    }
                );
            }
        }

        _forcedTargetManager.ApplyForcedTarget(
            context,
            target,
            source,
            effect.DurationSeconds!.Value,
            source.Key,
            ability.Key,
            effect.Key
        );
    }

    private static void ApplyThreat(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        Guid abilityExecutionId)
    {
        var amount =
            string.Equals(
                effect.ThreatOperation,
                ThreatManipulationOperationTypes.MatchHighest,
                StringComparison.OrdinalIgnoreCase)
                ? 0m
                : RollEffectValue(
                    context,
                    source,
                    effect
                );

        var result =
            ThreatManipulator.Apply(
                context,
                target,
                source,
                effect.ThreatOperation,
                amount
            );

        context.RecordAbilityEffectResult(
            abilityExecutionId,
            effect.Key,
            CombatRollResult.Hit()
        );

        if (result.Delta == 0m)
        {
            return;
        }

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.ThreatChanged,

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

                Amount =
                    result.Delta,

                Description =
                    $"{ability.Name} changed {source.Name}'s threat on {target.Name} from {result.PreviousThreat} to {result.CurrentThreat} using '{effect.ThreatOperation}'."
            }
        );
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

                PeriodicTickIntervalSeconds =
                    tickInterval,

                IncludeExpirationBoundaryTick =
                    effect.IncludeExpirationBoundaryTick,

                StackingMode =
                    effect.AuraStackingMode,

                MaxStacks =
                    Math.Max(
                        1,
                        effect.MaxStacks
                    ),

                Tags =
                    effect.Tags.ToList()
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

        PeriodicEffectScheduler.ScheduleTicks(
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
            !aura.CanProcessPeriodicTickAt(
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
            context.EmitEvent(
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
                        DescribeAvoidedDamage(
                            ability,
                            target,
                            roll
                        )
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

        var blockedAmount =
            roll.IsBlocked
                ? Math.Min(
                    mitigation.FinalAmount,
                    roll.BlockValue
                )
                : 0m;

        var postBlockAmount =
            Math.Max(
                0m,
                mitigation.FinalAmount -
                blockedAmount
            );

        var damageResult =
            _absorbManager.ApplyDamage(
                context,
                target,
                postBlockAmount,
                source.Key,
                ability.Key,
                effect.Key
            );

        var actualDamage =
            damageResult.HealthDamage;

        context.EmitEvent(
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

                BlockedAmount =
                    blockedAmount,

                AbsorbedAmount =
                    damageResult.AbsorbedDamage,

                Amount =
                    actualDamage,

                IsCritical =
                    roll.IsCritical,

                IsPeriodic =
                    isPeriodic,

                Description =
                    roll.IsCritical
                        ? $"{ability.Name} critically hit {target.Name} for {actualDamage:0.##} damage."
                        : roll.IsCrushing
                            ? $"{ability.Name} dealt a crushing blow to {target.Name} for {actualDamage:0.##} damage."
                            : roll.IsGlancing
                                ? $"{ability.Name} landed a glancing blow on {target.Name} for {actualDamage:0.##} damage."
                                : roll.IsBlocked
                                    ? $"{target.Name} blocked {blockedAmount:0.##} damage from {ability.Name} and took {actualDamage:0.##} damage."
                                    : $"{ability.Name} dealt {actualDamage:0.##} damage to {target.Name}."
            }
        );

        if (
            targetWasAlive &&
            !target.IsAlive)
        {
            context.EmitEvent(
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
            context.EmitEvent(
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

        context.EmitEvent(
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

    private static string DescribeAvoidedDamage(
        AbilityDefinition ability,
        SimulationActorState target,
        CombatRollResult roll)
    {
        if (string.Equals(
                roll.ResultKey,
                CombatResultTypes.Dodge,
                StringComparison.OrdinalIgnoreCase))
        {
            return
                $"{target.Name} dodged {ability.Name}.";
        }

        if (string.Equals(
                roll.ResultKey,
                CombatResultTypes.Parry,
                StringComparison.OrdinalIgnoreCase))
        {
            return
                $"{target.Name} parried {ability.Name}.";
        }

        if (string.Equals(
                roll.ResultKey,
                CombatResultTypes.Miss,
                StringComparison.OrdinalIgnoreCase))
        {
            return
                $"{ability.Name} missed {target.Name}.";
        }

        return
            $"{ability.Name} was avoided by {target.Name} ({roll.ResultKey}).";
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

            EffectDependencyConditions.Avoided =>
                !dependencyResult.Landed,

            EffectDependencyConditions.Dodged =>
                !dependencyResult.Landed &&
                string.Equals(
                    dependencyResult.ResultKey,
                    CombatResultTypes.Dodge,
                    StringComparison.OrdinalIgnoreCase
                ),

            EffectDependencyConditions.Parried =>
                !dependencyResult.Landed &&
                string.Equals(
                    dependencyResult.ResultKey,
                    CombatResultTypes.Parry,
                    StringComparison.OrdinalIgnoreCase
                ),

            EffectDependencyConditions.Blocked =>
                dependencyResult.Landed &&
                string.Equals(
                    dependencyResult.ResultKey,
                    CombatResultTypes.Block,
                    StringComparison.OrdinalIgnoreCase
                ),

            EffectDependencyConditions.Glancing =>
                dependencyResult.Landed &&
                string.Equals(
                    dependencyResult.ResultKey,
                    CombatResultTypes.Glancing,
                    StringComparison.OrdinalIgnoreCase
                ),

            EffectDependencyConditions.Crushing =>
                dependencyResult.Landed &&
                string.Equals(
                    dependencyResult.ResultKey,
                    CombatResultTypes.Crushing,
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

            context.EmitEvent(
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
