using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class RulesetThreatGenerationResolver :
    IThreatGenerationResolver
{
    private readonly CombatRulesetDefinition
        _ruleset;

    public RulesetThreatGenerationResolver(
        CombatRulesetDefinition ruleset)
    {
        ArgumentNullException.ThrowIfNull(
            ruleset
        );

        ValidateRuleset(
            ruleset
        );

        _ruleset =
            ruleset;
    }

    public IReadOnlyList<ThreatGenerationContribution> Resolve(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            combatEvent
        );

        var eventType =
            GetThreatEventType(
                combatEvent.Type
            );

        if (eventType is null)
        {
            return [];
        }

        var rule =
            _ruleset.GetThreatGenerationRule(
                eventType
            );

        if (rule is null)
        {
            return [];
        }

        var source =
            GetRequiredSourceActor(
                context,
                combatEvent
            );

        var amountBasis =
            ResolveAmountBasis(
                combatEvent,
                rule
            );

        if (amountBasis <= 0m)
        {
            return [];
        }

        var owners =
            ResolveThreatOwners(
                context,
                combatEvent,
                source,
                rule
            );

        if (owners.Count == 0)
        {
            return [];
        }

        var totalThreat =
            amountBasis *
            rule.BaseMultiplier *
            ResolveSourceMultiplier(
                source,
                rule
            ) *
            ResolveAbilityMultiplier(
                combatEvent,
                rule
            );

        if (totalThreat <= 0m)
        {
            return [];
        }

        var threatPerOwner =
            string.Equals(
                rule.ThreatOwnerDistributionMode,
                ThreatOwnerDistributionModes.SplitEvenly,
                StringComparison.OrdinalIgnoreCase
            )
                ? totalThreat /
                  owners.Count
                : totalThreat;

        return owners
            .Select(
                owner =>
                    new ThreatGenerationContribution
                    {
                        ThreatOwnerActorKey =
                            owner.Key,

                        ThreatSourceActorKey =
                            source.Key,

                        Amount =
                            threatPerOwner
                    }
            )
            .ToList();
    }

    private static string? GetThreatEventType(
        CombatEventType eventType)
    {
        return eventType switch
        {
            CombatEventType.Damage =>
                ThreatGenerationEventTypes.Damage,

            CombatEventType.Healing =>
                ThreatGenerationEventTypes.Healing,

            _ =>
                null
        };
    }

    private static SimulationActorState GetRequiredSourceActor(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (string.IsNullOrWhiteSpace(
                combatEvent.SourceActorKey))
        {
            throw new InvalidOperationException(
                $"Threat-generating {combatEvent.Type} event requires a source actor key."
            );
        }

        return context.GetActor(
                combatEvent.SourceActorKey
            )
            ?? throw new InvalidOperationException(
                $"Threat-generating source actor '{combatEvent.SourceActorKey}' does not exist in the simulation."
            );
    }

    private static decimal ResolveAmountBasis(
        CombatEvent combatEvent,
        ThreatGenerationRuleDefinition rule)
    {
        if (string.Equals(
                rule.AmountBasis,
                ThreatAmountBasisTypes.EffectiveAmount,
                StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(
                0m,
                combatEvent.Amount ??
                0m
            );
        }

        return Math.Max(
            0m,
            combatEvent.RawAmount ??
            0m
        );
    }

    private static IReadOnlyList<SimulationActorState>
        ResolveThreatOwners(
            SimulationContext context,
            CombatEvent combatEvent,
            SimulationActorState source,
            ThreatGenerationRuleDefinition rule)
    {
        if (string.Equals(
                rule.ThreatOwnerSelectionMode,
                ThreatOwnerSelectionModes.EventTarget,
                StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(
                    combatEvent.TargetActorKey))
            {
                throw new InvalidOperationException(
                    $"Threat-generating {combatEvent.Type} event requires a target actor key for event-target threat ownership."
                );
            }

            var target =
                context.GetActor(
                    combatEvent.TargetActorKey
                )
                ?? throw new InvalidOperationException(
                    $"Threat owner actor '{combatEvent.TargetActorKey}' does not exist in the simulation."
                );

            return [target];
        }

        return SimulationActorSemanticSelector
            .ResolveMatching(
                context,
                source,
                SimulationActorRelationshipTypes.Enemy,
                includeSourceActor:
                    false
            );
    }

    private static decimal ResolveSourceMultiplier(
        SimulationActorState source,
        ThreatGenerationRuleDefinition rule)
    {
        if (string.IsNullOrWhiteSpace(
                rule.SourceMultiplierStatKey))
        {
            return 1m;
        }

        return source.Stats.TryGet(
            rule.SourceMultiplierStatKey,
            out var multiplier)
                ? multiplier
                : rule.MissingSourceMultiplier;
    }

    private static decimal ResolveAbilityMultiplier(
        CombatEvent combatEvent,
        ThreatGenerationRuleDefinition rule)
    {
        if (
            string.IsNullOrWhiteSpace(
                combatEvent.AbilityKey) ||
            !rule.AbilityMultipliers.TryGetValue(
                combatEvent.AbilityKey,
                out var multiplier)
        )
        {
            return 1m;
        }

        return multiplier;
    }

    private static void ValidateRuleset(
        CombatRulesetDefinition ruleset)
    {
        ArgumentNullException.ThrowIfNull(
            ruleset.ThreatGenerationRules
        );

        foreach (
            var pair in
            ruleset.ThreatGenerationRules)
        {
            ValidateEventType(
                pair.Key
            );

            ArgumentNullException.ThrowIfNull(
                pair.Value
            );

            ValidateRule(
                pair.Key,
                pair.Value
            );
        }
    }

    private static void ValidateEventType(
        string eventType)
    {
        if (
            string.Equals(
                eventType,
                ThreatGenerationEventTypes.Damage,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                eventType,
                ThreatGenerationEventTypes.Healing,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        throw new ArgumentException(
            $"Unsupported threat generation event type '{eventType}'."
        );
    }

    private static void ValidateRule(
        string eventType,
        ThreatGenerationRuleDefinition rule)
    {
        if (rule.BaseMultiplier < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rule.BaseMultiplier),
                rule.BaseMultiplier,
                $"Threat rule '{eventType}' base multiplier cannot be negative."
            );
        }

        if (rule.MissingSourceMultiplier < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rule.MissingSourceMultiplier),
                rule.MissingSourceMultiplier,
                $"Threat rule '{eventType}' missing-source multiplier cannot be negative."
            );
        }

        if (
            !string.Equals(
                rule.ThreatOwnerSelectionMode,
                ThreatOwnerSelectionModes.EventTarget,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                rule.ThreatOwnerSelectionMode,
                ThreatOwnerSelectionModes.AllEnemiesOfSource,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new ArgumentException(
                $"Threat rule '{eventType}' uses unsupported owner selection mode '{rule.ThreatOwnerSelectionMode}'."
            );
        }

        if (
            !string.Equals(
                rule.ThreatOwnerDistributionMode,
                ThreatOwnerDistributionModes.FullAmountPerOwner,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                rule.ThreatOwnerDistributionMode,
                ThreatOwnerDistributionModes.SplitEvenly,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new ArgumentException(
                $"Threat rule '{eventType}' uses unsupported owner distribution mode '{rule.ThreatOwnerDistributionMode}'."
            );
        }

        if (
            !string.Equals(
                rule.AmountBasis,
                ThreatAmountBasisTypes.EffectiveAmount,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                rule.AmountBasis,
                ThreatAmountBasisTypes.RawAmount,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new ArgumentException(
                $"Threat rule '{eventType}' uses unsupported amount basis '{rule.AmountBasis}'."
            );
        }

        ArgumentNullException.ThrowIfNull(
            rule.AbilityMultipliers
        );

        foreach (
            var abilityMultiplier in
            rule.AbilityMultipliers)
        {
            if (string.IsNullOrWhiteSpace(
                    abilityMultiplier.Key))
            {
                throw new ArgumentException(
                    $"Threat rule '{eventType}' contains a blank ability multiplier key."
                );
            }

            if (abilityMultiplier.Value < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rule.AbilityMultipliers),
                    abilityMultiplier.Value,
                    $"Threat rule '{eventType}' ability multiplier for '{abilityMultiplier.Key}' cannot be negative."
                );
            }
        }
    }
}
