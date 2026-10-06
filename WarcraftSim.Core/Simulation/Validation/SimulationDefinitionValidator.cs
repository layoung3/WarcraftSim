using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Validation;

public static class SimulationDefinitionValidator
{
    private static readonly HashSet<string>
        SupportedAbilityEffectTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            AbilityEffectTypes.DirectDamage,
            AbilityEffectTypes.DirectHealing,
            AbilityEffectTypes.PeriodicDamage,
            AbilityEffectTypes.PeriodicHealing
        };

    private static readonly HashSet<string>
        SupportedDependencyConditions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            EffectDependencyConditions.Landed,
            EffectDependencyConditions.Critical,
            EffectDependencyConditions.Missed
        };

    private static readonly HashSet<string>
        SupportedRotationTargetModes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            RotationTargetSelectionModes.Default,
            RotationTargetSelectionModes.Self,
            RotationTargetSelectionModes.Fixed,
            RotationTargetSelectionModes.LowestHealthAlly,
            RotationTargetSelectionModes.FixedThenLowestHealthAlly
        };

    private static readonly HashSet<string>
        SupportedRotationConditionTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            RotationConditionTypes.CurrentTimeSeconds,
            RotationConditionTypes.SourceHealthPercent,
            RotationConditionTypes.TargetHealthPercent,
            RotationConditionTypes.SourceResourceCurrent,
            RotationConditionTypes.SourceResourcePercent,
            RotationConditionTypes.SourceAuraActive,
            RotationConditionTypes.SourceAuraMissing,
            RotationConditionTypes.TargetAuraActive,
            RotationConditionTypes.TargetAuraMissing,
            RotationConditionTypes.EncounterPhaseActive,
            RotationConditionTypes.EncounterPhaseInactive
        };

    public static void Validate(
        SimulationContext context,
        IEnumerable<ICombatEventProcessor> processors)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            processors
        );

        var errors =
            new List<string>();

        CollectAbilityErrors(
            context,
            errors
        );

        CollectUnsupportedEncounterErrors(
            context,
            errors
        );

        foreach (
            var participant in
            processors.OfType<ISimulationDefinitionValidationParticipant>())
        {
            participant.CollectValidationErrors(
                context,
                errors
            );
        }

        if (errors.Count > 0)
        {
            throw new SimulationDefinitionValidationException(
                errors
            );
        }
    }

    internal static void CollectRotationErrors(
        SimulationContext context,
        RotationProfile rotation,
        string actorKey,
        string defaultTargetKey,
        ICollection<string> errors)
    {
        var actor =
            context.GetActor(
                actorKey
            );

        if (actor is null)
        {
            errors.Add(
                $"Rotation '{rotation.Name}' references actor '{actorKey}', which does not exist in the simulation context."
            );

            return;
        }

        foreach (
            var entry in
            rotation.Entries.Where(entry =>
                entry is not null &&
                entry.IsEnabled))
        {
            if (string.IsNullOrWhiteSpace(
                    entry.AbilityKey))
            {
                errors.Add(
                    $"Rotation '{rotation.Name}' contains an enabled entry with no ability key."
                );
            }
            else if (!actor.Abilities.ContainsKey(
                         entry.AbilityKey))
            {
                errors.Add(
                    $"Rotation '{rotation.Name}' references ability '{entry.AbilityKey}', which actor '{actor.Key}' does not have."
                );
            }

            var target =
                entry.Target ??
                new RotationTargetDefinition();

            if (!SupportedRotationTargetModes.Contains(
                    target.Mode))
            {
                errors.Add(
                    $"Rotation '{rotation.Name}' uses unsupported target mode '{target.Mode}'."
                );
            }
            else
            {
                ValidateRotationTarget(
                    context,
                    rotation,
                    target,
                    defaultTargetKey,
                    errors
                );
            }

            foreach (
                var condition in
                entry.Conditions.Where(condition =>
                    condition is not null))
            {
                ValidateRotationCondition(
                    context,
                    actor,
                    rotation,
                    condition,
                    errors
                );
            }
        }
    }

    private static void CollectAbilityErrors(
        SimulationContext context,
        ICollection<string> errors)
    {
        foreach (
            var actor in
            context.Actors.Values)
        {
            foreach (
                var abilityState in
                actor.Abilities.Values)
            {
                ValidateAbility(
                    abilityState.Definition,
                    errors
                );
            }
        }
    }

    private static void ValidateAbility(
        AbilityDefinition ability,
        ICollection<string> errors)
    {
        var effectLookup =
            new Dictionary<string, AbilityEffectDefinition>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (
            var effect in
            ability.Effects)
        {
            if (effect is null)
            {
                errors.Add(
                    $"Ability '{ability.Key}' contains a null effect."
                );

                continue;
            }

            if (string.IsNullOrWhiteSpace(
                    effect.Key))
            {
                errors.Add(
                    $"Ability '{ability.Key}' contains an effect with no key."
                );
            }
            else if (!effectLookup.TryAdd(
                         effect.Key,
                         effect))
            {
                errors.Add(
                    $"Ability '{ability.Key}' contains duplicate effect key '{effect.Key}'."
                );
            }

            if (!SupportedAbilityEffectTypes.Contains(
                    effect.EffectType))
            {
                errors.Add(
                    $"Ability '{ability.Key}' effect '{effect.Key}' uses unsupported effect type '{effect.EffectType}'."
                );
            }

            if (effect.MaxTargets != 1)
            {
                errors.Add(
                    $"Ability '{ability.Key}' effect '{effect.Key}' requests MaxTargets={effect.MaxTargets}, but effect-level multi-targeting is not supported yet."
                );
            }

            if (!string.IsNullOrWhiteSpace(
                    effect.CustomMechanicKey))
            {
                errors.Add(
                    $"Ability '{ability.Key}' effect '{effect.Key}' uses custom mechanic '{effect.CustomMechanicKey}', but custom effect mechanics are not supported yet."
                );
            }

            if (
                !string.IsNullOrWhiteSpace(
                    effect.DependencyCondition) &&
                !SupportedDependencyConditions.Contains(
                    effect.DependencyCondition)
            )
            {
                errors.Add(
                    $"Ability '{ability.Key}' effect '{effect.Key}' uses unknown dependency condition '{effect.DependencyCondition}'."
                );
            }
        }

        foreach (
            var effect in
            effectLookup.Values)
        {
            if (
                !string.IsNullOrWhiteSpace(
                    effect.DependsOnEffectKey) &&
                !effectLookup.ContainsKey(
                    effect.DependsOnEffectKey)
            )
            {
                errors.Add(
                    $"Ability '{ability.Key}' effect '{effect.Key}' depends on missing effect '{effect.DependsOnEffectKey}'."
                );
            }
        }

        ValidateDependencyCycles(
            ability,
            effectLookup,
            errors
        );
    }

    private static void ValidateDependencyCycles(
        AbilityDefinition ability,
        IReadOnlyDictionary<string, AbilityEffectDefinition> effects,
        ICollection<string> errors)
    {
        var visited =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        var visiting =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (
            var effect in
            effects.Values)
        {
            VisitEffect(
                ability,
                effect,
                effects,
                visited,
                visiting,
                errors
            );
        }
    }

    private static void VisitEffect(
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        IReadOnlyDictionary<string, AbilityEffectDefinition> effects,
        HashSet<string> visited,
        HashSet<string> visiting,
        ICollection<string> errors)
    {
        if (visited.Contains(
                effect.Key))
        {
            return;
        }

        if (!visiting.Add(
                effect.Key))
        {
            errors.Add(
                $"Ability '{ability.Key}' contains an effect dependency cycle at '{effect.Key}'."
            );

            return;
        }

        if (
            !string.IsNullOrWhiteSpace(
                effect.DependsOnEffectKey) &&
            effects.TryGetValue(
                effect.DependsOnEffectKey,
                out var dependency)
        )
        {
            VisitEffect(
                ability,
                dependency,
                effects,
                visited,
                visiting,
                errors
            );
        }

        visiting.Remove(
            effect.Key
        );

        visited.Add(
            effect.Key
        );
    }

    private static void CollectUnsupportedEncounterErrors(
        SimulationContext context,
        ICollection<string> errors)
    {
        if (context.Encounter is null)
        {
            return;
        }

        if (context.Encounter.Targets.Count > 0)
        {
            errors.Add(
                $"Encounter '{context.Encounter.Name}' defines EncounterProfile.Targets, but encounter target materialization is not supported yet."
            );
        }

        if (context.Encounter.TargetProximityLinks.Count > 0)
        {
            errors.Add(
                $"Encounter '{context.Encounter.Name}' defines target proximity links, but target proximity is not supported yet."
            );
        }
    }

    private static void ValidateRotationTarget(
        SimulationContext context,
        RotationProfile rotation,
        RotationTargetDefinition target,
        string defaultTargetKey,
        ICollection<string> errors)
    {
        if (
            string.Equals(
                target.Mode,
                RotationTargetSelectionModes.Default,
                StringComparison.OrdinalIgnoreCase) &&
            context.GetActor(
                defaultTargetKey
            ) is null
        )
        {
            errors.Add(
                $"Rotation '{rotation.Name}' default target '{defaultTargetKey}' does not exist in the simulation context."
            );
        }

        if (
            !string.Equals(
                target.Mode,
                RotationTargetSelectionModes.Fixed,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                target.Mode,
                RotationTargetSelectionModes.FixedThenLowestHealthAlly,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
                target.ActorKey))
        {
            errors.Add(
                $"Rotation '{rotation.Name}' target mode '{target.Mode}' requires an actor key."
            );

            return;
        }

        if (context.GetActor(
                target.ActorKey) is null)
        {
            errors.Add(
                $"Rotation '{rotation.Name}' fixed target '{target.ActorKey}' does not exist in the simulation context."
            );
        }
    }

    private static void ValidateRotationCondition(
        SimulationContext context,
        SimulationActorState actor,
        RotationProfile rotation,
        RotationConditionDefinition condition,
        ICollection<string> errors)
    {
        if (!SupportedRotationConditionTypes.Contains(
                condition.ConditionType))
        {
            errors.Add(
                $"Rotation '{rotation.Name}' uses unknown condition type '{condition.ConditionType}'."
            );

            return;
        }

        if (
            string.Equals(
                condition.ConditionType,
                RotationConditionTypes.SourceResourceCurrent,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                condition.ConditionType,
                RotationConditionTypes.SourceResourcePercent,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            if (string.IsNullOrWhiteSpace(
                    condition.Key))
            {
                errors.Add(
                    $"Rotation '{rotation.Name}' resource condition requires a resource key."
                );
            }
            else if (!actor.Resources.ContainsKey(
                         condition.Key))
            {
                errors.Add(
                    $"Rotation '{rotation.Name}' references resource '{condition.Key}', which actor '{actor.Key}' does not have."
                );
            }
        }

        if (
            !string.Equals(
                condition.ConditionType,
                RotationConditionTypes.EncounterPhaseActive,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                condition.ConditionType,
                RotationConditionTypes.EncounterPhaseInactive,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(
                condition.Key))
        {
            errors.Add(
                $"Rotation '{rotation.Name}' encounter phase condition requires a phase key."
            );

            return;
        }

        if (
            context.Encounter is null ||
            !context.Encounter.Phases.Any(
                phase =>
                    string.Equals(
                        phase.Key,
                        condition.Key,
                        StringComparison.OrdinalIgnoreCase
                    )
            )
        )
        {
            errors.Add(
                $"Rotation '{rotation.Name}' references unknown encounter phase '{condition.Key}'."
            );
        }
    }
}
