using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Stance-aware Arms priority support. Only dances for an actually eligible
/// Whirlwind rotation row, never merely because Whirlwind is off cooldown.
/// The stance shared cooldown does not lock out normal GCD abilities.
/// </summary>
public sealed class WarriorWhirlwindStanceDanceProcessor : ICombatEventProcessor
{
    private readonly AbilityExecutor _executor;
    private readonly RotationProfile _rotation;
    private readonly decimal _retainedRage;
    private bool _needsWhirlwind;
    private bool _returnToBattle;

    public WarriorWhirlwindStanceDanceProcessor(AbilityExecutor executor,
        RotationProfile rotation, decimal retainedRage)
    {
        _executor = executor;
        _rotation = rotation;
        _retainedRage = retainedRage;
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.RotationDecision || ev.SourceActorKey != "warrior") return;
        var actor = context.GetActor("warrior");
        var target = context.GetActor("target");
        if (actor is null || target is null || !actor.IsAlive || actor.IsCasting(context.CurrentTimeSeconds)) return;
        bool InStance(string key) => actor.ActiveAuras.Any(a =>
            a.IsActiveAt(context.CurrentTimeSeconds) && a.Definition.Key == key);

        // Use the same conditions as the custom rotation (including cooldown
        // dependencies, Rage/AP thresholds and whether the row was omitted).
        bool WantsWhirlwind() => actor.Abilities.TryGetValue("whirlwind", out var ability) &&
            ability.IsReady(context.CurrentTimeSeconds) &&
            _rotation.Entries.Any(row => row.IsEnabled && row.AbilityKey == "whirlwind" &&
                RotationConditionEvaluator.AreSatisfied(context, actor, target, row.Conditions));

        if (_returnToBattle)
        {
            if (InStance(ForeverWarriorAuraKeys.BerserkerStance) && _needsWhirlwind)
            {
                if (_executor.TryStartAbility(context, "warrior", "target", "whirlwind").Success)
                {
                    _needsWhirlwind = false;
                    return;
                }
                return; // wait for the ability GCD or Rage, not a premature return
            }
            if (!InStance(ForeverWarriorAuraKeys.BerserkerStance))
            {
                if (InStance(ForeverWarriorAuraKeys.BattleStance)) _returnToBattle = false;
                return;
            }
            // Avoid throwing Rage away if a priority ability can spend the
            // excess before returning. Never stay just for uncastable actions.
            if (actor.Resources.TryGetValue("rage", out var rage) &&
                rage.Current > _retainedRage && HasUsefulStanceSpend(context, actor, target, ForeverWarriorAuraKeys.BerserkerStance))
                return;
            if (TryChangeStance(context, actor, "enter-battle-stance"))
                _returnToBattle = false;
            return;
        }

        if (!InStance(ForeverWarriorAuraKeys.BattleStance) || !WantsWhirlwind()) return;
        // Spend useful high-priority Rage in Battle first instead of losing it
        // on the outgoing stance switch.
        if (actor.Resources.TryGetValue("rage", out var battleRage) &&
            battleRage.Current > _retainedRage &&
            HasUsefulStanceSpend(context, actor, target, ForeverWarriorAuraKeys.BattleStance))
            return;
        if (TryChangeStance(context, actor, "enter-berserker-stance"))
        {
            _needsWhirlwind = true;
            _returnToBattle = true;
        }
    }

    private bool HasUsefulStanceSpend(SimulationContext context,
        SimulationActorState actor, SimulationActorState target, string stance)
    {
        foreach (var row in _rotation.Entries.Where(row => row.IsEnabled &&
                     row.ActionType == RotationActionTypes.Ability && row.AbilityKey != "whirlwind"))
        {
            if (!actor.Abilities.TryGetValue(row.AbilityKey, out var ability) ||
                !ability.IsReady(context.CurrentTimeSeconds) ||
                ability.Definition.ForbiddenSourceAuraKeys.Contains(stance) ||
                ability.Definition.RequiredSourceAuraKeys.Any(key => key != stance) ||
                !RotationConditionEvaluator.AreSatisfied(context, actor, target, row.Conditions)) continue;
            if (!actor.Resources.TryGetValue("rage", out var rage)) continue;
            if (ability.Definition.ResourceCosts.Where(c => c.ResourceKey == "rage")
                    .Sum(c => c.Amount) <= rage.Current)
                return true;
        }
        return false;
    }

    private bool TryChangeStance(SimulationContext context, SimulationActorState actor, string abilityKey)
    {
        if (!actor.IsSharedCooldownReady("warrior-stance", context.CurrentTimeSeconds))
        {
            ScheduleAt(context, actor.GetSharedCooldownReadyAt("warrior-stance", context.CurrentTimeSeconds));
            return false;
        }
        var succeeded = _executor.TryStartAbility(context, "warrior", "warrior", abilityKey).Success;
        if (succeeded)
            ScheduleAt(context, actor.GetSharedCooldownReadyAt("warrior-stance", context.CurrentTimeSeconds));
        return succeeded;
    }

    private static void ScheduleAt(SimulationContext context, decimal time)
    {
        if (time <= context.CurrentTimeSeconds) return;
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = time, Type = CombatEventType.RotationDecision,
            SourceActorKey = "warrior", TargetActorKey = "target", IsInternal = true,
            Description = "Shared stance cooldown ready: reevaluate priority."
        });
    }
}
