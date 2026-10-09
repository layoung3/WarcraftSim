using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Listens for resolved direct critical damage and applies per-source/per-target
/// rolling DoTs. This processor intentionally knows nothing about Warrior,
/// talent ranks, or the Forever ruleset.
/// </summary>
public sealed class CriticalStrikeRollingDamageProcessor : ICombatEventProcessor
{
    private readonly AuraManager _auraManager;

    public CriticalStrikeRollingDamageProcessor(AuraManager auraManager)
    {
        _auraManager = auraManager ?? throw new ArgumentNullException(nameof(auraManager));
    }

    public void Process(SimulationContext context, CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.Damage ||
            !combatEvent.IsCritical ||
            combatEvent.EffectDeliveryType != CombatEffectDeliveryType.Direct ||
            string.IsNullOrWhiteSpace(combatEvent.SourceActorKey) ||
            string.IsNullOrWhiteSpace(combatEvent.TargetActorKey) ||
            string.IsNullOrWhiteSpace(combatEvent.WeaponHandKey) ||
            string.IsNullOrWhiteSpace(combatEvent.ResolutionType))
        {
            return;
        }

        var source = context.GetActor(combatEvent.SourceActorKey);
        var target = context.GetActor(combatEvent.TargetActorKey);
        if (source is null || target is null || !source.IsAlive || !target.IsAlive)
        {
            return;
        }

        foreach (var proc in source.CriticalStrikeRollingDamageProcs.Values)
        {
            if (!proc.EligibleResolutionTypes.Contains(combatEvent.ResolutionType) ||
                !proc.AverageBaseWeaponDamageByHand.TryGetValue(
                    combatEvent.WeaponHandKey, out var averageWeaponDamage))
            {
                continue;
            }

            var effect = proc.PeriodicAbility.Effects.First(candidate =>
                string.Equals(candidate.Key, proc.PeriodicEffectKey,
                    StringComparison.OrdinalIgnoreCase));

            var duration = effect.DurationSeconds!.Value;
            var interval = effect.TickIntervalSeconds!.Value;
            var tickCount = PeriodicEffectScheduler.GetScheduledTickCount(
                duration, interval, effect.IncludeExpirationBoundaryTick);

            // Carry only remaining, pre-mitigation damage; previous ticks
            // (including blocked/absorbed/overkill portions) are never repaid.
            var auraKey = string.IsNullOrWhiteSpace(effect.AuraKey)
                ? effect.Key : effect.AuraKey;

            var previous = target.ActiveAuras.FirstOrDefault(candidate =>
                candidate.IsActiveAt(context.CurrentTimeSeconds) &&
                string.Equals(candidate.SourceActorKey, source.Key,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.Definition.Key, auraKey,
                    StringComparison.OrdinalIgnoreCase));

            var carried = previous?.RollingDamageRemaining ?? 0m;
            var added = Math.Max(0m, averageWeaponDamage) *
                        proc.DamageFractionOfWeaponAverage;
            if (added <= 0m && carried <= 0m)
            {
                continue;
            }

            var auraDefinition = new AuraDefinition
            {
                Key = auraKey,
                Name = proc.PeriodicAbility.Name,
                DurationSeconds = duration,
                PeriodicTickIntervalSeconds = interval,
                IncludeExpirationBoundaryTick = effect.IncludeExpirationBoundaryTick,
                StackingMode = AuraStackingMode.Refresh,
                Tags = effect.Tags.ToList()
            };

            var aura = _auraManager.ApplyAura(
                context, target, auraDefinition, source.Key,
                proc.PeriodicAbility.Key, effect.Key, scheduleExpiration: false);

            aura.RollingDamageRemaining = carried + added;
            aura.RollingTicksRemaining = tickCount;

            PeriodicEffectScheduler.ScheduleTicks(
                context, source, target, proc.PeriodicAbility, effect,
                aura, abilityExecutionId: null);
            _auraManager.ScheduleExpiration(context, aura);
        }
    }
}
