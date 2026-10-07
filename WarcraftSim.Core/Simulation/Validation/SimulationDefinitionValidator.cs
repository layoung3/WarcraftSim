using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
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
            AbilityEffectTypes.PeriodicHealing,
            AbilityEffectTypes.Threat,
            AbilityEffectTypes.Taunt,
            AbilityEffectTypes.ApplyAura,
            AbilityEffectTypes.RemoveAura,
            AbilityEffectTypes.ResourceChange,
            AbilityEffectTypes.Absorb
        };

    private static readonly HashSet<string>
        SupportedThreatManipulationOperations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ThreatManipulationOperationTypes.Add,
            ThreatManipulationOperationTypes.Set,
            ThreatManipulationOperationTypes.MatchHighest,
            ThreatManipulationOperationTypes.MatchHighestPlus
        };

    private static readonly HashSet<string>
        SupportedResourceChangeOperations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ResourceChangeOperationTypes.Gain,
            ResourceChangeOperationTypes.Spend,
            ResourceChangeOperationTypes.Set
        };

    private static readonly HashSet<string>
        SupportedAbilityTargetTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            AbilityTargetTypes.Enemy,
            AbilityTargetTypes.Friendly,
            AbilityTargetTypes.Self
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
        SupportedEncounterTargetModes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            EncounterTargetSelectionModes.FixedActor,
            EncounterTargetSelectionModes.AllMatchingActors,
            EncounterTargetSelectionModes.RandomMatchingActors,
            EncounterTargetSelectionModes.HighestThreatActor
        };

    private static readonly HashSet<string>
        SupportedRotationTargetModes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            RotationTargetSelectionModes.Default,
            RotationTargetSelectionModes.Self,
            RotationTargetSelectionModes.Fixed,
            RotationTargetSelectionModes.LowestHealthAlly,
            RotationTargetSelectionModes.FixedThenLowestHealthAlly,
            RotationTargetSelectionModes.FirstMatchingActor,
            RotationTargetSelectionModes.LowestHealthMatchingActor
        };

    private static readonly HashSet<string>
        SupportedActorRelationships =
        new(StringComparer.OrdinalIgnoreCase)
        {
            SimulationActorRelationshipTypes.Any,
            SimulationActorRelationshipTypes.Ally,
            SimulationActorRelationshipTypes.Enemy
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

            if (!SupportedAbilityTargetTypes.Contains(
                    effect.TargetType))
            {
                errors.Add(
                    $"Ability '{ability.Key}' effect '{effect.Key}' uses unsupported target type '{effect.TargetType}'."
                );
            }

            if (string.Equals(
                    effect.EffectType,
                    AbilityEffectTypes.Threat,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(
                        effect.TargetType,
                        AbilityTargetTypes.Enemy,
                        StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"Ability '{ability.Key}' threat effect '{effect.Key}' requires enemy targeting."
                    );
                }

                if (!SupportedThreatManipulationOperations.Contains(
                        effect.ThreatOperation))
                {
                    errors.Add(
                        $"Ability '{ability.Key}' threat effect '{effect.Key}' uses unknown threat operation '{effect.ThreatOperation}'."
                    );
                }
            }

            if (string.Equals(
                    effect.EffectType,
                    AbilityEffectTypes.Taunt,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(
                        effect.TargetType,
                        AbilityTargetTypes.Enemy,
                        StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"Ability '{ability.Key}' taunt effect '{effect.Key}' requires enemy targeting."
                    );
                }

                if (
                    !effect.DurationSeconds.HasValue ||
                    effect.DurationSeconds.Value <= 0m)
                {
                    errors.Add(
                        $"Ability '{ability.Key}' taunt effect '{effect.Key}' requires DurationSeconds greater than zero."
                    );
                }

                if (
                    !string.IsNullOrWhiteSpace(
                        effect.TauntThreatOperation) &&
                    !SupportedThreatManipulationOperations.Contains(
                        effect.TauntThreatOperation))
                {
                    errors.Add(
                        $"Ability '{ability.Key}' taunt effect '{effect.Key}' uses unknown taunt threat operation '{effect.TauntThreatOperation}'."
                    );
                }
            }

            if (string.Equals(
                    effect.EffectType,
                    AbilityEffectTypes.ApplyAura,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(
                        effect.AuraKey))
                {
                    errors.Add(
                        $"Ability '{ability.Key}' apply-aura effect '{effect.Key}' requires AuraKey."
                    );
                }

                if (
                    !effect.DurationSeconds.HasValue ||
                    effect.DurationSeconds.Value <= 0m)
                {
                    errors.Add(
                        $"Ability '{ability.Key}' apply-aura effect '{effect.Key}' requires DurationSeconds greater than zero."
                    );
                }

                if (effect.MaxStacks < 1)
                {
                    errors.Add(
                        $"Ability '{ability.Key}' apply-aura effect '{effect.Key}' requires MaxStacks to be at least 1."
                    );
                }
            }

            if (string.Equals(
                    effect.EffectType,
                    AbilityEffectTypes.RemoveAura,
                    StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(
                    effect.AuraKey))
            {
                errors.Add(
                    $"Ability '{ability.Key}' remove-aura effect '{effect.Key}' requires AuraKey."
                );
            }

            if (string.Equals(
                    effect.EffectType,
                    AbilityEffectTypes.ResourceChange,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(
                        effect.ResourceKey))
                {
                    errors.Add(
                        $"Ability '{ability.Key}' resource-change effect '{effect.Key}' requires ResourceKey."
                    );
                }

                if (!SupportedResourceChangeOperations.Contains(
                        effect.ResourceChangeOperation))
                {
                    errors.Add(
                        $"Ability '{ability.Key}' resource-change effect '{effect.Key}' uses unknown resource operation '{effect.ResourceChangeOperation}'."
                    );
                }

                if (
                    effect.MinimumValue < 0m ||
                    effect.MaximumValue < 0m)
                {
                    errors.Add(
                        $"Ability '{ability.Key}' resource-change effect '{effect.Key}' requires non-negative effect values; use ResourceChangeOperation to choose gain, spend, or set behavior."
                    );
                }
            }

            if (string.Equals(
                    effect.EffectType,
                    AbilityEffectTypes.Absorb,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(
                        effect.AbsorbKey))
                {
                    errors.Add(
                        $"Ability '{ability.Key}' absorb effect '{effect.Key}' requires AbsorbKey."
                    );
                }

                if (
                    !effect.DurationSeconds.HasValue ||
                    effect.DurationSeconds.Value <= 0m)
                {
                    errors.Add(
                        $"Ability '{ability.Key}' absorb effect '{effect.Key}' requires DurationSeconds greater than zero."
                    );
                }

                if (effect.MaxStacks < 1)
                {
                    errors.Add(
                        $"Ability '{ability.Key}' absorb effect '{effect.Key}' requires MaxStacks to be at least 1."
                    );
                }

                if (
                    effect.MinimumValue < 0m ||
                    effect.MaximumValue < 0m)
                {
                    errors.Add(
                        $"Ability '{ability.Key}' absorb effect '{effect.Key}' requires non-negative effect values."
                    );
                }
            }

            if (effect.MaxTargets < 1)
            {
                errors.Add(
                    $"Ability '{ability.Key}' effect '{effect.Key}' requires MaxTargets to be at least 1."
                );
            }

            if (
                string.Equals(
                    effect.TargetType,
                    AbilityTargetTypes.Self,
                    StringComparison.OrdinalIgnoreCase
                ) &&
                effect.MaxTargets != 1)
            {
                errors.Add(
                    $"Ability '{ability.Key}' effect '{effect.Key}' targets self and therefore requires MaxTargets=1."
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
            if (string.IsNullOrWhiteSpace(
                    effect.DependsOnEffectKey))
            {
                continue;
            }

            if (!effectLookup.TryGetValue(
                    effect.DependsOnEffectKey,
                    out var dependencyEffect))
            {
                errors.Add(
                    $"Ability '{ability.Key}' effect '{effect.Key}' depends on missing effect '{effect.DependsOnEffectKey}'."
                );

                continue;
            }

            if (dependencyEffect.MaxTargets != 1)
            {
                errors.Add(
                    $"Ability '{ability.Key}' effect '{effect.Key}' depends on multi-target effect '{dependencyEffect.Key}'. Multi-target dependency results are not supported yet."
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

        foreach (
            var pattern in
            context.Encounter.DamagePatterns)
        {
            var selection =
                pattern.TargetSelection;

            if (selection is null)
            {
                continue;
            }

            if (!SupportedEncounterTargetModes.Contains(
                    selection.Mode))
            {
                errors.Add(
                    $"Encounter damage pattern '{pattern.Key}' uses unknown target selection mode '{selection.Mode}'."
                );

                continue;
            }

            if (!SupportedActorRelationships.Contains(
                    selection.Relationship))
            {
                errors.Add(
                    $"Encounter damage pattern '{pattern.Key}' uses unknown target relationship '{selection.Relationship}'."
                );

                continue;
            }

            if (string.Equals(
                    selection.Mode,
                    EncounterTargetSelectionModes.HighestThreatActor,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(
                        pattern.SourceActorKey))
                {
                    errors.Add(
                        $"Encounter damage pattern '{pattern.Key}' uses highest-threat targeting but does not define a source actor key."
                    );

                    continue;
                }

                if (context.GetActor(
                        pattern.SourceActorKey) is null)
                {
                    errors.Add(
                        $"Encounter damage pattern '{pattern.Key}' highest-threat source actor '{pattern.SourceActorKey}' does not exist in the simulation context."
                    );

                    continue;
                }
            }

            if (
                !string.Equals(
                    selection.Relationship,
                    SimulationActorRelationshipTypes.Any,
                    StringComparison.OrdinalIgnoreCase
                ) &&
                string.IsNullOrWhiteSpace(
                    pattern.SourceActorKey)
            )
            {
                errors.Add(
                    $"Encounter damage pattern '{pattern.Key}' uses semantic relationship '{selection.Relationship}' but does not define a source actor key."
                );
            }
        }
    }

    private static void ValidateRotationTarget(
        SimulationContext context,
        RotationProfile rotation,
        RotationTargetDefinition target,
        string defaultTargetKey,
        ICollection<string> errors)
    {
        if (!SupportedActorRelationships.Contains(
                target.Relationship))
        {
            errors.Add(
                $"Rotation '{rotation.Name}' uses unknown target relationship '{target.Relationship}'."
            );
        }

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
