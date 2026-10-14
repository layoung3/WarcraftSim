using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Encounters;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Encounter-owned boss casts, cooldowns, channels and dispellable timed DoTs.
/// Only writes ScriptedDamage events: mitigation, absorption and health policy
/// stay with the configured damage processor, not with any particular class.
/// All per-run state is in the event queue and in each actor's auras.
/// </summary>
public sealed class EncounterBossAbilityProcessor : ICombatEventProcessor
{
    private const string Channel = "encounter-channel";
    private const string Dot = "encounter-dot";
    private readonly AuraManager _auras;
    // Processor instance is scoped to a single SimulationEngine.Run iteration.
    // Tokens invalidate stale scheduled cast completions after an interrupt.
    private readonly Dictionary<string, Guid> _activeCasts = new(StringComparer.OrdinalIgnoreCase);

    public EncounterBossAbilityProcessor(AuraManager auras) =>
        _auras = auras ?? throw new ArgumentNullException(nameof(auras));

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (context.Encounter is null) return;
        if (ev.Type == CombatEventType.SimulationStarted)
        {
            Validate(context);
            foreach (var scheduledAbility in context.Encounter.BossAbilities)
                ScheduleStart(context, scheduledAbility, scheduledAbility.FirstUseSeconds);
            return;
        }
        if (ev.Type == CombatEventType.EncounterBossInterruptRequested)
        {
            Interrupt(context, ev);
            return;
        }
        if (ev.Type is not (CombatEventType.EncounterBossAbilityStart or
            CombatEventType.EncounterBossAbilityImpact or
            CombatEventType.EncounterBossAbilityTick) || !ev.IsInternal)
            return;

        var ability = context.Encounter.BossAbilities.FirstOrDefault(a =>
            string.Equals(a.Key, ev.EncounterEventKey, StringComparison.OrdinalIgnoreCase));
        if (ability is null || !string.Equals(ev.SourceActorKey, ability.SourceActorKey, StringComparison.OrdinalIgnoreCase)) return;
        var source = context.GetActor(ability.SourceActorKey);
        if (source is null || !source.IsAlive) return;

        switch (ev.Type)
        {
            case CombatEventType.EncounterBossAbilityStart:
                Start(context, ability);
                break;
            case CombatEventType.EncounterBossAbilityImpact:
                Impact(context, ability, ev);
                break;
            case CombatEventType.EncounterBossAbilityTick:
                Tick(context, ability, ev);
                break;
        }
    }

    private static void Validate(SimulationContext context)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in context.Encounter!.BossAbilities)
        {
            if (string.IsNullOrWhiteSpace(a.Key) || !keys.Add(a.Key) ||
                string.IsNullOrWhiteSpace(a.SourceActorKey) ||
                context.GetActor(a.SourceActorKey) is null)
                throw new ArgumentException($"Encounter boss ability '{a.Key}' requires a unique key and an existing source actor.");
            if ((a.SourceHealthPercentAtOrBelow is < 0m or > 100m) ||
                (a.SourceHealthPercentAtOrAbove is < 0m or > 100m) ||
                (a.SourceHealthPercentAtOrBelow is { } upper &&
                 a.SourceHealthPercentAtOrAbove is { } lower && lower > upper) ||
                a.RequiredPhaseKeys is null ||
                a.RequiredPhaseKeys.Any(key => string.IsNullOrWhiteSpace(key) ||
                    !context.Encounter.Phases.Any(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))))
                throw new ArgumentException($"Encounter boss ability '{a.Key}' has an invalid health or phase gate.");
            if (a.FirstUseSeconds < 0m || a.CooldownSeconds < 0m ||
                a.CastTimeSeconds < 0m || a.DirectDamage < 0m ||
                a.ChannelTickDamage < 0m || a.DotTickDamage < 0m ||
                a.DebuffDurationSeconds < 0m || a.AreaExposurePercent is < 0m or > 100m)
                throw new ArgumentException($"Encounter boss ability '{a.Key}' has negative timing/damage or invalid exposure.");
            if (a.ChannelTickCount is < 0 or > 100 ||
                (a.ChannelTickCount > 0 && a.ChannelTickIntervalSeconds <= 0m) ||
                (a.DotTickDamage > 0m && (a.DotTickIntervalSeconds <= 0m ||
                    a.DebuffDurationSeconds <= 0m || string.IsNullOrWhiteSpace(a.DebuffKey))))
                throw new ArgumentException($"Encounter boss ability '{a.Key}' has an invalid channel or DoT configuration.");
            // Bound authored mechanics before scheduling a potentially massive queue.
            if (a.DotTickDamage > 0m &&
                a.DebuffDurationSeconds / a.DotTickIntervalSeconds > 100m)
                throw new ArgumentException($"Encounter boss ability '{a.Key}' exceeds 100 DoT ticks per application.");
            if (a.CooldownSeconds is > 0m and < 0.5m)
                throw new ArgumentException($"Encounter boss ability '{a.Key}' must have at least 0.5s between starts.");
            if (a.CooldownSeconds > 0m && a.CooldownSeconds < a.CastTimeSeconds +
                    a.ChannelTickCount * a.ChannelTickIntervalSeconds)
                throw new ArgumentException($"Encounter boss ability '{a.Key}' cooldown overlaps its cast/channel.");
            if (a.TargetSelection is not null &&
                a.TargetSelection.Mode is not (EncounterTargetSelectionModes.FixedActor or
                    EncounterTargetSelectionModes.AllMatchingActors or
                    EncounterTargetSelectionModes.RandomMatchingActors or
                    EncounterTargetSelectionModes.HighestThreatActor))
                throw new ArgumentException($"Encounter boss ability '{a.Key}' has an unknown target selection mode.");
        }
    }

    private static void ScheduleStart(SimulationContext context, EncounterBossAbilityDefinition ability, decimal at)
    {
        if (at >= context.Options.DurationSeconds) return;
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = at, Type = CombatEventType.EncounterBossAbilityStart,
            SourceActorKey = ability.SourceActorKey, EncounterEventKey = ability.Key,
            IsInternal = true
        });
    }

    private static IReadOnlyList<SimulationActorState> Select(
        SimulationContext context, EncounterBossAbilityDefinition ability)
    {
        // Reuse the existing semantic, role, threat, taunt and RNG selector.
        return EncounterTargetSelector.Resolve(context, new EncounterDamagePatternDefinition
        {
            Key = ability.Key, SourceActorKey = ability.SourceActorKey,
            TargetActorKey = ability.TargetActorKey,
            TargetSelection = ability.TargetSelection
        });
    }

    private void Start(SimulationContext context, EncounterBossAbilityDefinition ability)
    {
        // Retain the original cooldown cadence even when a phase gate is closed.
        // A skipped slot does not create a cast, aura, damage, or channel.
        if (ability.CooldownSeconds > 0m)
            ScheduleStart(context, ability, context.CurrentTimeSeconds + ability.CooldownSeconds);
        var boss = context.GetActor(ability.SourceActorKey)!;
        var hp = context.GetHealthPercentForConditions(boss);
        if (ability.RequiredPhaseKeys.Count > 0 && !ability.RequiredPhaseKeys.Any(key =>
                context.Encounter!.Phases.Any(phase =>
                    string.Equals(phase.Key, key, StringComparison.OrdinalIgnoreCase) &&
                    phase.IsActiveAt(context.CurrentTimeSeconds)))) return;
        if (ability.SourceHealthPercentAtOrBelow is { } max && hp > max) return;
        if (ability.SourceHealthPercentAtOrAbove is { } min && hp < min) return;

        // No overlapping casts of the same configured ability.
        if (_activeCasts.ContainsKey(ability.Key)) return;
        var token = Guid.NewGuid();
        _activeCasts[ability.Key] = token;
        List<string> locked = ability.LockTargetsOnCastStart
            ? Select(context, ability).Select(a => a.Key).ToList() : [];
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds,
            Type = CombatEventType.EncounterBossCastStarted,
            SourceActorKey = ability.SourceActorKey,
            EncounterEventKey = ability.Key, AbilityKey = $"encounter:{ability.Key}",
            AbilityExecutionId = token,
            EncounterSequenceSelectedActorKeys = locked.ToList(),
            Description = $"{ability.Name} cast started ({ability.CastTimeSeconds:0.##}s)."
        });
        var impact = context.CurrentTimeSeconds + ability.CastTimeSeconds;
        if (impact < context.Options.DurationSeconds)
            context.ScheduleEvent(new CombatEvent
            {
                TimeSeconds = impact, Type = CombatEventType.EncounterBossAbilityImpact,
                SourceActorKey = ability.SourceActorKey, EncounterEventKey = ability.Key,
                AbilityExecutionId = token,
                EncounterSequenceSelectedActorKeys = locked.ToList(), IsInternal = true
            });
        // If impact falls outside encounter duration, the token expires with the run.
    }

    private void Interrupt(SimulationContext context, CombatEvent ev)
    {
        // SourceActorKey = interrupter, TargetActorKey = boss caster.
        // EncounterEventKey optionally targets one named ability.
        if (string.IsNullOrWhiteSpace(ev.SourceActorKey) ||
            string.IsNullOrWhiteSpace(ev.TargetActorKey) ||
            context.GetActor(ev.SourceActorKey) is not { IsAlive: true } interrupter ||
            context.GetActor(ev.TargetActorKey) is not { IsAlive: true } caster ||
            string.Equals(interrupter.TeamKey, caster.TeamKey, StringComparison.OrdinalIgnoreCase)) return;
        foreach (var ability in context.Encounter!.BossAbilities)
        {
            if (!ability.Interruptible ||
                !string.Equals(ability.SourceActorKey, caster.Key, StringComparison.OrdinalIgnoreCase) ||
                (ev.EncounterEventKey is not null &&
                 !string.Equals(ev.EncounterEventKey, ability.Key, StringComparison.OrdinalIgnoreCase)) ||
                !_activeCasts.Remove(ability.Key, out var token)) continue;
            context.EmitEvent(new CombatEvent
            {
                TimeSeconds = context.CurrentTimeSeconds,
                Type = CombatEventType.EncounterBossCastInterrupted,
                SourceActorKey = interrupter.Key, TargetActorKey = caster.Key,
                EncounterEventKey = ability.Key, AbilityKey = $"encounter:{ability.Key}",
                AbilityExecutionId = token,
                Description = $"{interrupter.Name} interrupted {ability.Name}."
            });
        }
    }

    private void Impact(SimulationContext context, EncounterBossAbilityDefinition ability, CombatEvent ev)
    {
        if (!_activeCasts.TryGetValue(ability.Key, out var token) ||
            ev.AbilityExecutionId != token) return;
        _activeCasts.Remove(ability.Key);
        var targets = ability.LockTargetsOnCastStart
            ? ev.EncounterSequenceSelectedActorKeys.Select(context.GetActor)
                .Where(a => a is { IsAlive: true }).Cast<SimulationActorState>().ToList()
            : Select(context, ability).ToList();
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds,
            Type = CombatEventType.EncounterBossCastCompleted,
            SourceActorKey = ability.SourceActorKey, EncounterEventKey = ability.Key,
            AbilityKey = $"encounter:{ability.Key}", AbilityExecutionId = token,
            EncounterSequenceSelectedActorKeys = targets.Select(a => a.Key).ToList(),
            Description = $"{ability.Name} cast completed; {targets.Count} target(s)."
        });
        foreach (var target in targets)
        {
            ScheduleDamage(context, ability, target.Key, ability.DirectDamage, context.CurrentTimeSeconds, "impact");
            for (var i = 1; i <= ability.ChannelTickCount; i++)
            {
                var at = context.CurrentTimeSeconds + i * ability.ChannelTickIntervalSeconds;
                ScheduleTick(context, ability, target.Key, at, Channel, null);
            }
            if (!string.IsNullOrWhiteSpace(ability.DebuffKey) && ability.DebuffDurationSeconds > 0m)
            {
                var aura = _auras.ApplyAura(context, target, new AuraDefinition
                {
                    Key = ability.DebuffKey, Name = ability.Name + " debuff",
                    DurationSeconds = ability.DebuffDurationSeconds,
                    Tags = string.IsNullOrWhiteSpace(ability.DispelType)
                        ? new List<string>() : new List<string> { $"dispel:{ability.DispelType}" }
                }, ability.SourceActorKey, $"encounter:{ability.Key}", ability.DebuffKey);
                if (ability.DotTickDamage > 0m)
                    for (var at = context.CurrentTimeSeconds + ability.DotTickIntervalSeconds;
                         at < context.CurrentTimeSeconds + ability.DebuffDurationSeconds;
                         at += ability.DotTickIntervalSeconds)
                        ScheduleTick(context, ability, target.Key, at, Dot, aura.InstanceId);
            }
        }
    }

    private static void ScheduleTick(SimulationContext context, EncounterBossAbilityDefinition ability,
        string targetKey, decimal at, string effect, Guid? auraId)
    {
        if (at >= context.Options.DurationSeconds) return;
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = at, Type = CombatEventType.EncounterBossAbilityTick,
            SourceActorKey = ability.SourceActorKey, TargetActorKey = targetKey,
            EncounterEventKey = ability.Key, EffectKey = effect, AuraInstanceId = auraId,
            IsInternal = true
        });
    }

    private static void Tick(SimulationContext context, EncounterBossAbilityDefinition ability, CombatEvent ev)
    {
        var target = context.GetActor(ev.TargetActorKey ?? "");
        if (target is null || !target.IsAlive) return;
        if (ev.EffectKey == Dot)
        {
            if (ev.AuraInstanceId is not { } id ||
                !target.ActiveAuras.Any(a => a.InstanceId == id && a.IsActiveAt(context.CurrentTimeSeconds)))
                return; // Dispelled, expired or refreshed: queued tick is stale.
            ScheduleDamage(context, ability, target.Key, ability.DotTickDamage, context.CurrentTimeSeconds, "dot");
        }
        else if (ev.EffectKey == Channel)
            ScheduleDamage(context, ability, target.Key, ability.ChannelTickDamage, context.CurrentTimeSeconds, "channel");
    }

    private static void ScheduleDamage(SimulationContext context, EncounterBossAbilityDefinition ability,
        string targetKey, decimal damage, decimal at, string delivery)
    {
        var raw = damage * ability.AreaExposurePercent / 100m;
        if (raw <= 0m || at >= context.Options.DurationSeconds) return;
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = at, Type = CombatEventType.ScriptedDamage,
            SourceActorKey = ability.SourceActorKey, TargetActorKey = targetKey,
            EncounterEventKey = ability.Key, EffectKey = delivery,
            SchoolKey = ability.SchoolKey, MitigationType = ability.MitigationType,
            RawAmount = raw, Amount = raw, IsInternal = true,
            EffectDeliveryType = delivery == "dot" ? CombatEffectDeliveryType.Periodic :
                delivery == "channel" ? CombatEffectDeliveryType.ChannelTick : CombatEffectDeliveryType.Direct,
            Description = $"{ability.Name} ({delivery})"
        });
    }
}
