using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Drives independently-timed background weapon swings. Auto-attacks do not
/// consume player input, do not start the GCD, and do not require cast readiness.
/// </summary>
public sealed class AutoAttackProcessor :
    ICombatEventProcessor
{
    private readonly AbilityExecutor
        _abilityExecutor;

    private readonly IAutoAttackTimingProvider
        _timingProvider;

    public AutoAttackProcessor(
        AbilityExecutor abilityExecutor,
        IAutoAttackTimingProvider? timingProvider = null)
    {
        _abilityExecutor =
            abilityExecutor ??
            throw new ArgumentNullException(
                nameof(abilityExecutor)
            );

        _timingProvider =
            timingProvider ??
            BaseAutoAttackTimingProvider.Instance;
    }

    public bool Start(
        SimulationContext context,
        string sourceActorKey,
        string targetActorKey,
        AutoAttackDefinition definition,
        decimal? firstSwingDelaySeconds = null)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ValidateDefinition(
            definition
        );

        var source =
            context.GetActor(
                sourceActorKey
            );

        var target =
            context.GetActor(
                targetActorKey
            );

        if (
            source is null ||
            target is null ||
            !source.IsAlive ||
            !target.IsAlive)
        {
            return false;
        }

        var resolvedSwingInterval =
            ResolveSwingInterval(
                context,
                source,
                definition
            );

        var firstDelay =
            firstSwingDelaySeconds ??
            resolvedSwingInterval;

        if (firstDelay < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstSwingDelaySeconds),
                firstSwingDelaySeconds,
                "The first auto-attack swing delay cannot be negative."
            );
        }

        if (!source.AutoAttacks.TryGetValue(
                definition.Key,
                out var state))
        {
            state =
                new AutoAttackState(
                    definition
                );

            source.AutoAttacks.Add(
                definition.Key,
                state
            );
        }
        else if (!ReferenceEquals(
                     state.Definition,
                     definition))
        {
            throw new InvalidOperationException(
                $"Auto-attack key '{definition.Key}' is already registered with a different definition on actor '{source.Key}'."
            );
        }

        var firstSwingAt =
            context.CurrentTimeSeconds +
            firstDelay;

        state.Start(
            target.Key,
            firstSwingAt
        );

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.AutoAttackStarted,

                SourceActorKey =
                    source.Key,

                TargetActorKey =
                    target.Key,

                AbilityKey =
                    definition.Key,

                AutoAttackInstanceId =
                    state.InstanceId,

                Description =
                    $"{source.Name} started {definition.Name} against {target.Name}."
            }
        );

        ScheduleSwing(
            context,
            source,
            state,
            firstSwingAt
        );

        return true;
    }

    public bool Stop(
        SimulationContext context,
        string sourceActorKey,
        string autoAttackKey)
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
            !source.AutoAttacks.TryGetValue(
                autoAttackKey,
                out var state) ||
            !state.IsActive)
        {
            return false;
        }

        var stoppedInstanceId =
            state.InstanceId;

        var targetActorKey =
            state.TargetActorKey;

        state.Stop();

        context.EmitEvent(
            new CombatEvent
            {
                TimeSeconds =
                    context.CurrentTimeSeconds,

                Type =
                    CombatEventType.AutoAttackStopped,

                SourceActorKey =
                    source.Key,

                TargetActorKey =
                    targetActorKey,

                AbilityKey =
                    state.Definition.Key,

                AutoAttackInstanceId =
                    stoppedInstanceId,

                Description =
                    $"{source.Name} stopped {state.Definition.Name}."
            }
        );

        return true;
    }

    public bool QueueNextSwingReplacement(
        SimulationContext context,
        string sourceActorKey,
        string autoAttackKey,
        NextSwingReplacementDefinition replacement)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ValidateReplacement(
            replacement
        );

        var source =
            context.GetActor(
                sourceActorKey
            );

        if (
            source is null ||
            !source.IsAlive ||
            !source.AutoAttacks.TryGetValue(
                autoAttackKey,
                out var state) ||
            !state.IsActive)
        {
            return false;
        }

        if (!string.Equals(
                state.Definition.WeaponHandKey,
                replacement.WeaponHandKey,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Queued replacement '{replacement.Key}' targets weapon hand '{replacement.WeaponHandKey}', but auto-attack '{state.Definition.Key}' owns '{state.Definition.WeaponHandKey}'."
            );
        }

        source.RefreshResources(
            context.CurrentTimeSeconds
        );

        if (!CanAffordResourceCosts(
                source,
                replacement.ResourceCosts))
        {
            return false;
        }

        state.QueueNextSwingReplacement(
            replacement
        );

        return true;
    }

    public bool CancelNextSwingReplacement(
        SimulationContext context,
        string sourceActorKey,
        string autoAttackKey)
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
            !source.AutoAttacks.TryGetValue(
                autoAttackKey,
                out var state))
        {
            return false;
        }

        return state.CancelNextSwingReplacement();
    }

    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (
            combatEvent.Type !=
                CombatEventType.AutoAttackSwing ||
            string.IsNullOrWhiteSpace(
                combatEvent.SourceActorKey) ||
            string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey) ||
            !combatEvent.AutoAttackInstanceId.HasValue)
        {
            return;
        }

        var source =
            context.GetActor(
                combatEvent.SourceActorKey
            );

        if (
            source is null ||
            !source.AutoAttacks.TryGetValue(
                combatEvent.AbilityKey,
                out var state) ||
            !state.IsActive ||
            state.InstanceId !=
                combatEvent.AutoAttackInstanceId.Value)
        {
            return;
        }

        var target =
            context.GetActor(
                state.TargetActorKey
            );

        if (
            !source.IsAlive ||
            target is null ||
            !target.IsAlive)
        {
            Stop(
                context,
                source.Key,
                state.Definition.Key
            );

            return;
        }

        state.NextSwingAtSeconds =
            null;

        var replacement =
            state.ConsumeNextSwingReplacement();

        source.RefreshResources(
            context.CurrentTimeSeconds
        );

        var paidResourceCosts =
            new Dictionary<string, PaidResourceCost>(
                StringComparer.OrdinalIgnoreCase
            );

        var replacementCanExecute =
            replacement is not null &&
            TrySpendResourceCosts(
                context,
                source,
                replacement.Key,
                replacement.ResourceCosts,
                out paidResourceCosts
            );

        if (!replacementCanExecute)
        {
            _abilityExecutor.ExecuteBackgroundDirectDamage(
                context,
                source,
                target,
                state.Definition.Key,
                state.Definition.Name,
                state.Definition.DamageEffect,
                ResolveDamageMultiplier(
                    source,
                    state.Definition.DamageMultiplier,
                    state.Definition.DamageMultiplierStatKey
                )
            );
        }
        else
        {
            var replacementRoll =
                _abilityExecutor.ExecuteBackgroundDirectDamage(
                    context,
                    source,
                    target,
                    replacement!.Key,
                    replacement.Name,
                    replacement.DamageEffect,
                    ResolveDamageMultiplier(
                        source,
                        replacement.DamageMultiplier,
                        replacement.DamageMultiplierStatKey
                    )
                );

            ApplyOutcomeResourceRefunds(
                context,
                source,
                replacement,
                paidResourceCosts,
                replacementRoll.ResultKey
            );
        }

        if (!state.IsActive)
        {
            return;
        }

        if (
            !source.IsAlive ||
            !target.IsAlive)
        {
            Stop(
                context,
                source.Key,
                state.Definition.Key
            );

            return;
        }

        var nextSwingInterval =
            ResolveSwingInterval(
                context,
                source,
                state.Definition
            );

        var nextSwingAt =
            context.CurrentTimeSeconds +
            nextSwingInterval;

        state.NextSwingAtSeconds =
            nextSwingAt;

        ScheduleSwing(
            context,
            source,
            state,
            nextSwingAt
        );
    }


    private static bool CanAffordResourceCosts(
        SimulationActorState source,
        IReadOnlyCollection<AbilityResourceCost> resourceCosts)
    {
        foreach (var resourceCost in resourceCosts)
        {
            if (!source.Resources.TryGetValue(
                    resourceCost.ResourceKey,
                    out var resource))
            {
                return false;
            }

            var cost =
                ResolveResourceCost(
                    resourceCost,
                    resource
                );

            if (!resource.CanSpend(cost))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySpendResourceCosts(
        SimulationContext context,
        SimulationActorState source,
        string abilityKey,
        IReadOnlyCollection<AbilityResourceCost> resourceCosts,
        out Dictionary<string, PaidResourceCost> paidResourceCosts)
    {
        paidResourceCosts =
            new Dictionary<string, PaidResourceCost>(
                StringComparer.OrdinalIgnoreCase
            );

        if (!CanAffordResourceCosts(
                source,
                resourceCosts))
        {
            return false;
        }

        foreach (var resourceCost in resourceCosts)
        {
            var resource =
                source.Resources[
                    resourceCost.ResourceKey];

            var cost =
                ResolveResourceCost(
                    resourceCost,
                    resource
                );

            resource.Spend(cost);

            paidResourceCosts.Add(
                resourceCost.ResourceKey,
                new PaidResourceCost(
                    resource,
                    cost
                )
            );

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
                        abilityKey,

                    Amount =
                        -cost,

                    Description =
                        $"{source.Name} spent {cost:0.##} {resource.ResourceKey}."
                }
            );
        }

        return true;
    }

    private static void ApplyOutcomeResourceRefunds(
        SimulationContext context,
        SimulationActorState source,
        NextSwingReplacementDefinition replacement,
        IReadOnlyDictionary<string, PaidResourceCost> paidResourceCosts,
        string resultKey)
    {
        if (
            replacement.ResourceRefunds.Count == 0 ||
            string.IsNullOrWhiteSpace(resultKey))
        {
            return;
        }

        foreach (var refundRule in replacement.ResourceRefunds)
        {
            if (
                !refundRule.ResultKeys.Contains(
                    resultKey,
                    StringComparer.OrdinalIgnoreCase) ||
                !paidResourceCosts.TryGetValue(
                    refundRule.ResourceKey,
                    out var paidResourceCost))
            {
                continue;
            }

            var requestedRefund =
                paidResourceCost.AmountPaid *
                refundRule.RefundPercent /
                100m;

            if (requestedRefund <= 0m)
            {
                continue;
            }

            var previousValue =
                paidResourceCost.Resource.Current;

            paidResourceCost.Resource.Gain(
                requestedRefund
            );

            var actualRefund =
                paidResourceCost.Resource.Current -
                previousValue;

            if (actualRefund <= 0m)
            {
                continue;
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
                        source.Key,

                    AbilityKey =
                        replacement.Key,

                    Amount =
                        actualRefund,

                    ResultKey =
                        resultKey,

                    Description =
                        $"{source.Name} refunded {actualRefund:0.##} {paidResourceCost.Resource.ResourceKey} after {replacement.Name} resulted in {resultKey}."
                }
            );
        }
    }

    private sealed record PaidResourceCost(
        ResourceState Resource,
        decimal AmountPaid);

    private static decimal ResolveResourceCost(
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

    private static decimal ResolveDamageMultiplier(
        SimulationActorState source,
        decimal baseMultiplier,
        string? damageMultiplierStatKey)
    {
        var multiplier =
            baseMultiplier;

        if (!string.IsNullOrWhiteSpace(
                damageMultiplierStatKey))
        {
            var bonusPercent =
                source.Stats.Get(
                    damageMultiplierStatKey
                );

            multiplier *=
                1m +
                bonusPercent / 100m;
        }

        return Math.Max(
            0m,
            multiplier
        );
    }

    private decimal ResolveSwingInterval(
        SimulationContext context,
        SimulationActorState source,
        AutoAttackDefinition definition)
    {
        var timing =
            _timingProvider.Resolve(
                context,
                source,
                definition
            ) ??
            throw new InvalidOperationException(
                "Auto-attack timing providers must return a timing snapshot."
            );

        if (timing.SwingIntervalSeconds <= 0m)
        {
            throw new InvalidOperationException(
                $"Auto-attack timing provider resolved a non-positive swing interval ({timing.SwingIntervalSeconds}) for '{definition.Key}'."
            );
        }

        return timing.SwingIntervalSeconds;
    }

    private static void ScheduleSwing(
        SimulationContext context,
        SimulationActorState source,
        AutoAttackState state,
        decimal timeSeconds)
    {
        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds =
                    timeSeconds,

                Type =
                    CombatEventType.AutoAttackSwing,

                SourceActorKey =
                    source.Key,

                TargetActorKey =
                    state.TargetActorKey,

                AbilityKey =
                    state.Definition.Key,

                AutoAttackInstanceId =
                    state.InstanceId,

                IsInternal =
                    true,

                Description =
                    $"Internal swing driver for {state.Definition.Name}."
            }
        );
    }

    private static void ValidateReplacement(
        NextSwingReplacementDefinition replacement)
    {
        ArgumentNullException.ThrowIfNull(
            replacement
        );

        if (string.IsNullOrWhiteSpace(
                replacement.Key))
        {
            throw new ArgumentException(
                "Queued next-swing replacements require a key.",
                nameof(replacement)
            );
        }

        if (string.IsNullOrWhiteSpace(
                replacement.WeaponHandKey))
        {
            throw new ArgumentException(
                "Queued next-swing replacements require a weapon-hand key.",
                nameof(replacement)
            );
        }

        if (replacement.DamageMultiplier < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(replacement),
                replacement.DamageMultiplier,
                "Queued next-swing replacement damage multipliers cannot be negative."
            );
        }

        if (replacement.ResourceCosts is null)
        {
            throw new ArgumentException(
                "Queued next-swing replacement resource costs cannot be null.",
                nameof(replacement)
            );
        }

        if (replacement.ResourceRefunds is null)
        {
            throw new ArgumentException(
                "Queued next-swing replacement resource refunds cannot be null.",
                nameof(replacement)
            );
        }

        var resourceKeys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (var resourceCost in replacement.ResourceCosts)
        {
            if (string.IsNullOrWhiteSpace(
                    resourceCost.ResourceKey))
            {
                throw new ArgumentException(
                    "Queued next-swing replacement resource costs require a resource key.",
                    nameof(replacement)
                );
            }

            if (resourceCost.Amount < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(replacement),
                    resourceCost.Amount,
                    "Queued next-swing replacement resource costs cannot be negative."
                );
            }

            if (!resourceKeys.Add(
                    resourceCost.ResourceKey))
            {
                throw new ArgumentException(
                    $"Queued next-swing replacement '{replacement.Key}' contains duplicate resource cost key '{resourceCost.ResourceKey}'.",
                    nameof(replacement)
                );
            }
        }

        var refundResourceKeys =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (var refundRule in replacement.ResourceRefunds)
        {
            if (string.IsNullOrWhiteSpace(
                    refundRule.ResourceKey))
            {
                throw new ArgumentException(
                    "Queued next-swing resource refunds require a resource key.",
                    nameof(replacement)
                );
            }

            if (!resourceKeys.Contains(
                    refundRule.ResourceKey))
            {
                throw new ArgumentException(
                    $"Queued next-swing refund resource '{refundRule.ResourceKey}' does not match a configured resource cost on '{replacement.Key}'.",
                    nameof(replacement)
                );
            }

            if (!refundResourceKeys.Add(
                    refundRule.ResourceKey))
            {
                throw new ArgumentException(
                    $"Queued next-swing replacement '{replacement.Key}' contains duplicate refund rules for resource '{refundRule.ResourceKey}'.",
                    nameof(replacement)
                );
            }

            if (
                refundRule.RefundPercent < 0m ||
                refundRule.RefundPercent > 100m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(replacement),
                    refundRule.RefundPercent,
                    "Queued next-swing resource refund percentages must be between 0 and 100."
                );
            }

            if (refundRule.ResultKeys is null)
            {
                throw new ArgumentException(
                    "Queued next-swing resource refund result keys cannot be null.",
                    nameof(replacement)
                );
            }

            var resultKeys =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (var resultKey in refundRule.ResultKeys)
            {
                if (string.IsNullOrWhiteSpace(resultKey))
                {
                    throw new ArgumentException(
                        "Queued next-swing resource refund result keys cannot be blank.",
                        nameof(replacement)
                    );
                }

                if (!resultKeys.Add(resultKey))
                {
                    throw new ArgumentException(
                        $"Queued next-swing resource refund for '{refundRule.ResourceKey}' contains duplicate result key '{resultKey}'.",
                        nameof(replacement)
                    );
                }
            }
        }

        if (
            replacement.DamageEffect is null ||
            !string.Equals(
                replacement.DamageEffect.EffectType,
                AbilityEffectTypes.DirectDamage,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Queued next-swing replacements require one direct-damage effect.",
                nameof(replacement)
            );
        }

        if (
            string.IsNullOrWhiteSpace(
                replacement.DamageEffect.WeaponHandKey) ||
            !string.Equals(
                replacement.DamageEffect.WeaponHandKey,
                replacement.WeaponHandKey,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Queued next-swing replacement damage must use the same explicit weapon-hand identity as the replacement.",
                nameof(replacement)
            );
        }
    }

    private static void ValidateDefinition(
        AutoAttackDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition
        );

        if (string.IsNullOrWhiteSpace(
                definition.Key))
        {
            throw new ArgumentException(
                "Auto-attacks require a key.",
                nameof(definition)
            );
        }

        if (definition.SwingIntervalSeconds <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                definition.SwingIntervalSeconds,
                "Auto-attack swing intervals must be greater than zero."
            );
        }

        if (definition.DamageMultiplier < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(definition),
                definition.DamageMultiplier,
                "Auto-attack damage multipliers cannot be negative."
            );
        }

        if (string.IsNullOrWhiteSpace(
                definition.WeaponHandKey))
        {
            throw new ArgumentException(
                "Auto-attacks require a weapon-hand key.",
                nameof(definition)
            );
        }

        if (
            definition.DamageEffect is null ||
            !string.Equals(
                definition.DamageEffect.EffectType,
                AbilityEffectTypes.DirectDamage,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Auto-attacks require one direct-damage effect.",
                nameof(definition)
            );
        }
    }
}
