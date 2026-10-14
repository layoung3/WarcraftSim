using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Core.Simulation.Healing;

/// <summary>
/// Shared healer decision driver. It chooses the living, most injured ally
/// and tries priority-ordered abilities through the normal AbilityExecutor,
/// which remains authoritative for cast locks, costs, cooldowns and impacts.
/// No Priest-specific spell knowledge is embedded here.
/// </summary>
public sealed class HealerPriorityProcessor : ICombatEventProcessor
{
    public const string PulseEffectKey = "healer-priority-pulse";
    private readonly HealerPriorityDefinition _definition;
    private readonly AbilityExecutor _executor;

    public HealerPriorityProcessor(HealerPriorityDefinition definition, AbilityExecutor executor)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        if (string.IsNullOrWhiteSpace(definition.HealerActorKey) ||
            string.IsNullOrWhiteSpace(definition.EligibleTeamKey) ||
            definition.DecisionIntervalSeconds <= 0m ||
            definition.Rules.Count == 0 ||
            definition.Rules.Any(rule => string.IsNullOrWhiteSpace(rule.AbilityKey) ||
                rule.CastAtOrBelowHealthPercent is < 0m or > 100m ||
                rule.MinimumMissingHealth < 0m || rule.MinimumInjuredTeamMembers < 1))
            throw new ArgumentException("Invalid healer priority configuration.", nameof(definition));
    }

    public void ScheduleFirst(SimulationContext context) => Schedule(context, 0m);

    public void Process(SimulationContext context, CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.RotationDecision ||
            !combatEvent.IsInternal ||
            !string.Equals(combatEvent.EffectKey, PulseEffectKey, StringComparison.Ordinal) ||
            !string.Equals(combatEvent.SourceActorKey, _definition.HealerActorKey,
                StringComparison.OrdinalIgnoreCase))
            return;

        var healer = context.GetActor(_definition.HealerActorKey);
        if (healer is { IsAlive: true })
        {
            healer.RefreshResources(context.CurrentTimeSeconds);
            var injured = context.Actors.Values
                .Where(actor => actor.IsAlive &&
                    string.Equals(actor.TeamKey, _definition.EligibleTeamKey,
                        StringComparison.OrdinalIgnoreCase) &&
                    actor.MaximumHealth > 0m && actor.CurrentHealth < actor.MaximumHealth)
                .OrderBy(actor => actor.CurrentHealth / actor.MaximumHealth)
                .ThenBy(actor => actor.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var rule in _definition.Rules.OrderBy(rule => rule.Priority))
            {
                if (!healer.Abilities.ContainsKey(rule.AbilityKey)) continue;
                var qualified = injured.Where(actor =>
                    actor.CurrentHealth * 100m / actor.MaximumHealth <=
                        rule.CastAtOrBelowHealthPercent &&
                    actor.MaximumHealth - actor.CurrentHealth >= rule.MinimumMissingHealth &&
                    (string.IsNullOrWhiteSpace(rule.SkipWhenTargetHasAuraKey) ||
                     !actor.ActiveAuras.Any(aura => aura.IsActiveAt(context.CurrentTimeSeconds) &&
                         string.Equals(aura.Definition.Key, rule.SkipWhenTargetHasAuraKey,
                             StringComparison.OrdinalIgnoreCase))) &&
                    (string.IsNullOrWhiteSpace(rule.SkipWhenTargetHasAbsorbKey) ||
                     !actor.ActiveAbsorbs.Any(absorb =>
                         string.Equals(absorb.AbsorbKey, rule.SkipWhenTargetHasAbsorbKey,
                             StringComparison.OrdinalIgnoreCase) &&
                         absorb.ExpiresAtSeconds > context.CurrentTimeSeconds)) )
                    .ToList();
                if (qualified.Count < rule.MinimumInjuredTeamMembers) continue;
                var target = qualified.FirstOrDefault();
                if (target is null) continue;
                if (_executor.TryStartAbility(context, healer.Key, target.Key,
                        rule.AbilityKey).Success)
                    break;
            }
        }
        Schedule(context, context.CurrentTimeSeconds + _definition.DecisionIntervalSeconds);
    }

    private void Schedule(SimulationContext context, decimal time)
    {
        if (time >= context.Options.DurationSeconds) return;
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = time, Type = CombatEventType.RotationDecision,
            IsInternal = true, EffectKey = PulseEffectKey,
            SourceActorKey = _definition.HealerActorKey
        });
    }
}
