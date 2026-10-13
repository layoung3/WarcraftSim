using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Arms prefers Berserker Stance for its crit bonus, but moves to Battle Stance
/// when a currently eligible, affordable Battle-only priority action is due.
/// Stance switches use their own shared cooldown and actual Rage retention.
/// No stance action pretends to spend a normal ability GCD.
/// </summary>
public sealed class WarriorArmsStancePriorityProcessor : ICombatEventProcessor
{
    private const string Warrior = "warrior";
    private const string Target = "target";
    private readonly AbilityExecutor _executor;
    private readonly RotationProfile _rotation;
    private readonly decimal _retainedRage;
    private const string StanceWakeupKey = "warrior-arms-stance-ready";
    private readonly HashSet<decimal> _scheduledWakeups = [];

    public WarriorArmsStancePriorityProcessor(AbilityExecutor executor,
        RotationProfile rotation, decimal retainedRage)
    {
        _executor = executor;
        _rotation = rotation;
        _retainedRage = retainedRage;
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.RotationDecision || ev.SourceActorKey != Warrior) return;
        if (ev.EffectKey == StanceWakeupKey)
            _scheduledWakeups.Remove(context.CurrentTimeSeconds);
        var actor = context.GetActor(Warrior);
        var target = context.GetActor(Target);
        if (actor is null || target is null || !actor.IsAlive || !target.IsAlive ||
            actor.IsCasting(context.CurrentTimeSeconds)) return;

        var now = context.CurrentTimeSeconds;
        bool HasStance(string key) => actor.ActiveAuras.Any(a =>
            a.IsActiveAt(now) && string.Equals(a.Definition.Key, key, StringComparison.OrdinalIgnoreCase));
        var battle = HasStance(ForeverWarriorAuraKeys.BattleStance);
        var berserker = HasStance(ForeverWarriorAuraKeys.BerserkerStance);
        if (!battle && !berserker) return;

        // Evaluate the same ordered ability rows as the rotation. A lower-
        // priority Rend must not force a stance change while Mortal Strike
        // (or another higher-priority Berserker-usable ability) is ready.
        var nextEligible = _rotation.Entries
            .Where(row => row.IsEnabled && row.ActionType == RotationActionTypes.Ability)
            .OrderBy(row => row.Priority)
            .FirstOrDefault(row => EligibleForStance(context, actor, target, row,
                    ForeverWarriorAuraKeys.BerserkerStance, null) ||
                EligibleForStance(context, actor, target, row,
                    ForeverWarriorAuraKeys.BattleStance, battle ? null : _retainedRage));
        var wantsBattle = nextEligible is not null &&
            EligibleForStance(context, actor, target, nextEligible,
                ForeverWarriorAuraKeys.BattleStance, battle ? null : _retainedRage) &&
            !EligibleForStance(context, actor, target, nextEligible,
                ForeverWarriorAuraKeys.BerserkerStance, null);

        if (battle)
        {
            // Cast Battle-only actions first. If Rage exceeds retention, spend
            // it on an immediately eligible Battle spell before returning.
            if (wantsBattle || (Rage(actor) > _retainedRage &&
                _rotation.Entries.Any(row => EligibleForStance(context, actor, target,
                    row, ForeverWarriorAuraKeys.BattleStance, null)))) return;
            ChangeStance(context, actor, "enter-berserker-stance");
        }
        else if (wantsBattle)
        {
            // Don't lose Rage if an eligible spell can spend it first, but
            // never dance unless the target Battle ability stays affordable
            // after retention is applied.
            if (Rage(actor) > _retainedRage &&
                _rotation.Entries.Any(row => EligibleForStance(context, actor, target,
                    row, ForeverWarriorAuraKeys.BerserkerStance, null))) return;
            ChangeStance(context, actor, "enter-battle-stance");
        }
    }

    private static decimal Rage(SimulationActorState actor) =>
        actor.Resources.TryGetValue("rage", out var resource) ? resource.Current : 0m;

    private static bool EligibleForStance(SimulationContext context,
        SimulationActorState actor, SimulationActorState target,
        RotationEntry row, string stance, decimal? rageCap)
    {
        if (!row.IsEnabled || row.ActionType != RotationActionTypes.Ability ||
            !actor.Abilities.TryGetValue(row.AbilityKey, out var state) ||
            !state.IsReady(context.CurrentTimeSeconds) ||
            !RotationConditionEvaluator.AreSatisfied(context, actor, target, row.Conditions)) return false;
        var definition = state.Definition;
        if (definition.ForbiddenSourceAuraKeys.Any(key =>
            string.Equals(key, stance, StringComparison.OrdinalIgnoreCase))) return false;
        if (definition.RequiredSourceAuraKeys.Any(key =>
            !string.Equals(key, stance, StringComparison.OrdinalIgnoreCase))) return false;
        if (!string.IsNullOrWhiteSpace(definition.RequiredTargetOpportunityKey) &&
            !actor.HasReactiveOpportunity(definition.RequiredTargetOpportunityKey,
                target.Key, context.CurrentTimeSeconds)) return false;
        return definition.ResourceCosts.Where(cost => cost.ResourceKey == "rage")
            .Sum(cost => cost.Amount) <= Math.Min(Rage(actor), rageCap ?? Rage(actor));
    }

    private void ChangeStance(SimulationContext context, SimulationActorState actor, string key)
    {
        if (!actor.IsSharedCooldownReady("warrior-stance", context.CurrentTimeSeconds))
        {
            ScheduleAt(context, actor.GetSharedCooldownReadyAt("warrior-stance", context.CurrentTimeSeconds));
            return;
        }
        if (_executor.TryStartAbility(context, Warrior, Warrior, key).Success)
            ScheduleAt(context, actor.GetSharedCooldownReadyAt("warrior-stance", context.CurrentTimeSeconds));
    }

    private void ScheduleAt(SimulationContext context, decimal at)
    {
        if (at <= context.CurrentTimeSeconds || at >= context.Options.DurationSeconds) return;
        // Multiple rotation decisions at the same timestamp must not enqueue
        // repeated shared-cooldown wakeups (a growing event storm).
        if (!_scheduledWakeups.Add(at)) return;
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = at, Type = CombatEventType.RotationDecision,
            SourceActorKey = Warrior, TargetActorKey = Target, EffectKey = StanceWakeupKey, IsInternal = true,
            Description = "Arms stance policy: shared stance cooldown ready."
        });
    }
}
