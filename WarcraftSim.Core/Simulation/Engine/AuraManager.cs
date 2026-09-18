using WarcraftSim.Core.Auras;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class AuraManager : ICombatEventProcessor
{
    public AuraInstance ApplyAura(
        SimulationContext context,
        SimulationActorState target,
        AuraDefinition definition,
        string sourceActorKey,
        string? abilityKey,
        string? effectKey,
        bool scheduleExpiration = true)
    {
        var now = context.CurrentTimeSeconds;

        var matchingAuras = target.ActiveAuras
            .Where(aura =>
                string.Equals(
                    aura.Definition.Key,
                    definition.Key,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    aura.SourceActorKey,
                    sourceActorKey,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        AuraInstance aura;

        switch (definition.StackingMode)
        {
            case AuraStackingMode.Independent:
                aura = CreateAura(
                    definition,
                    sourceActorKey,
                    target.Key,
                    abilityKey,
                    effectKey,
                    now,
                    1);

                target.ActiveAuras.Add(aura);
                break;

            case AuraStackingMode.Replace:
                foreach (var existingAura in matchingAuras)
                {
                    RemoveAura(
                        context,
                        target,
                        existingAura,
                        "replaced");
                }

                aura = CreateAura(
                    definition,
                    sourceActorKey,
                    target.Key,
                    abilityKey,
                    effectKey,
                    now,
                    1);

                target.ActiveAuras.Add(aura);
                break;

            case AuraStackingMode.Stack:
                aura = matchingAuras.FirstOrDefault()
                    ?? CreateAura(
                        definition,
                        sourceActorKey,
                        target.Key,
                        abilityKey,
                        effectKey,
                        now,
                        0);

                if (!target.ActiveAuras.Contains(aura))
                {
                    target.ActiveAuras.Add(aura);
                }

                aura.InstanceId = Guid.NewGuid();
                aura.Definition = definition;
                aura.AppliedAtSeconds = now;
                aura.ExpiresAtSeconds =
                    now + Math.Max(0m, definition.DurationSeconds);

                aura.Stacks = Math.Min(
                    Math.Max(1, definition.MaxStacks),
                    Math.Max(1, aura.Stacks + 1));
                break;

            case AuraStackingMode.Refresh:
            default:
                aura = matchingAuras.FirstOrDefault()
                    ?? CreateAura(
                        definition,
                        sourceActorKey,
                        target.Key,
                        abilityKey,
                        effectKey,
                        now,
                        1);

                if (!target.ActiveAuras.Contains(aura))
                {
                    target.ActiveAuras.Add(aura);
                }

                // Give the refreshed application a new runtime identity.
                // Any previously queued ticks/expiration events become stale
                // and will be ignored automatically.
                aura.InstanceId = Guid.NewGuid();
                aura.Definition = definition;
                aura.AppliedAtSeconds = now;
                aura.ExpiresAtSeconds =
                    now + Math.Max(0m, definition.DurationSeconds);
                aura.Stacks = Math.Max(1, aura.Stacks);
                break;
        }

        context.RecordEvent(
            new CombatEvent
            {
                TimeSeconds = now,
                Type = CombatEventType.AuraApplied,
                SourceActorKey = sourceActorKey,
                TargetActorKey = target.Key,
                AbilityKey = abilityKey,
                EffectKey = effectKey,
                AuraInstanceId = aura.InstanceId,
                Description =
                    $"{definition.Name} applied to {target.Name} (stacks: {aura.Stacks})."
            });

        if (scheduleExpiration)
        {
            ScheduleExpiration(
                context,
                aura);
        }

        return aura;
    }

    public void ScheduleExpiration(
        SimulationContext context,
        AuraInstance aura)
    {
        if (aura.ExpiresAtSeconds <= context.CurrentTimeSeconds)
        {
            return;
        }

        context.ScheduleEvent(
            new CombatEvent
            {
                TimeSeconds = aura.ExpiresAtSeconds,
                Type = CombatEventType.AuraExpiration,
                SourceActorKey = aura.SourceActorKey,
                TargetActorKey = aura.TargetActorKey,
                AbilityKey = aura.AbilityKey,
                EffectKey = aura.EffectKey,
                AuraInstanceId = aura.InstanceId,
                IsInternal = true,
                Description =
                    $"{aura.Definition.Name} expiration check."
            });
    }

    public void Process(
        SimulationContext context,
        CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.AuraExpiration ||
            !combatEvent.AuraInstanceId.HasValue ||
            string.IsNullOrWhiteSpace(combatEvent.TargetActorKey))
        {
            return;
        }

        var target =
            context.GetActor(combatEvent.TargetActorKey);

        if (target is null)
        {
            return;
        }

        var aura = target.ActiveAuras
            .FirstOrDefault(candidate =>
                candidate.InstanceId ==
                combatEvent.AuraInstanceId.Value);

        // Missing means this was an old expiration event for an aura
        // that was refreshed, replaced, or already removed.
        if (aura is null)
        {
            return;
        }

        if (context.CurrentTimeSeconds < aura.ExpiresAtSeconds)
        {
            return;
        }

        RemoveAura(
            context,
            target,
            aura,
            "expired");
    }

    private static AuraInstance CreateAura(
        AuraDefinition definition,
        string sourceActorKey,
        string targetActorKey,
        string? abilityKey,
        string? effectKey,
        decimal now,
        int stacks)
    {
        return new AuraInstance
        {
            InstanceId = Guid.NewGuid(),
            Definition = definition,
            SourceActorKey = sourceActorKey,
            TargetActorKey = targetActorKey,
            AbilityKey = abilityKey,
            EffectKey = effectKey,
            AppliedAtSeconds = now,
            ExpiresAtSeconds =
                now + Math.Max(0m, definition.DurationSeconds),
            Stacks = Math.Max(1, stacks)
        };
    }

    private static void RemoveAura(
        SimulationContext context,
        SimulationActorState target,
        AuraInstance aura,
        string reason)
    {
        if (!target.ActiveAuras.Remove(aura))
        {
            return;
        }

        context.RecordEvent(
            new CombatEvent
            {
                TimeSeconds = context.CurrentTimeSeconds,
                Type = CombatEventType.AuraRemoved,
                SourceActorKey = aura.SourceActorKey,
                TargetActorKey = aura.TargetActorKey,
                AbilityKey = aura.AbilityKey,
                EffectKey = aura.EffectKey,
                AuraInstanceId = aura.InstanceId,
                Description =
                    $"{aura.Definition.Name} {reason} on {target.Name}."
            });
    }
}
