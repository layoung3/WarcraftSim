using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Optional target-owned Fury -> Battle -> Overpower -> Fury sequence. Decisions
/// still use the shared AbilityExecutor, with real GCD and Rage retention.
/// Never predicts proc rolls and does not switch merely because a boss can dodge.
/// </summary>
public sealed class WarriorStanceDanceProcessor : ICombatEventProcessor
{
    private const string Warrior = "warrior";
    private const string Target = "target";
    private readonly AbilityExecutor _executor;
    private readonly decimal _maximumRage;
    private readonly decimal _retainedRage;
    private bool _returnToBerserker;
    private const string StanceWakeupKey = "warrior-fury-stance-ready";
    private readonly HashSet<decimal> _scheduledWakeups = [];

    public WarriorStanceDanceProcessor(AbilityExecutor executor,
        decimal maximumRage, decimal retainedRage)
    {
        _executor = executor;
        _maximumRage = maximumRage;
        _retainedRage = retainedRage;
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.RotationDecision || ev.SourceActorKey != Warrior)
            return;
        if (ev.EffectKey == StanceWakeupKey)
            _scheduledWakeups.Remove(context.CurrentTimeSeconds);
        var actor = context.GetActor(Warrior);
        var target = context.GetActor(Target);
        if (actor is null || target is null || !actor.IsAlive ||
            actor.IsCasting(context.CurrentTimeSeconds)) return;

        bool InStance(string auraKey) => actor.ActiveAuras.Any(aura =>
            aura.IsActiveAt(context.CurrentTimeSeconds) &&
            string.Equals(aura.Definition.Key, auraKey, StringComparison.OrdinalIgnoreCase));
        var opportunity = actor.HasReactiveOpportunity(
            ForeverOverpowerFactory.OpportunityKey, Target, context.CurrentTimeSeconds);
        if (_returnToBerserker)
        {
            if (InStance(ForeverWarriorAuraKeys.BattleStance) && opportunity &&
                actor.Abilities.TryGetValue(ForeverOverpowerFactory.AbilityKey, out var overpower) &&
                overpower.IsReady(context.CurrentTimeSeconds))
            {
                if (actor.Resources.TryGetValue("rage", out var rageForOverpower) &&
                    rageForOverpower.Current >= 5m)
                {
                    // A stance change never spends the normal ability GCD.
                    // Do not leave Battle just because that GCD is still running.
                    if (!actor.IsGlobalCooldownReady(context.CurrentTimeSeconds)) return;
                    if (_executor.TryStartAbility(context, Warrior, Target,
                            ForeverOverpowerFactory.AbilityKey).Success) return;
                }
            }
            if (InStance(ForeverWarriorAuraKeys.BattleStance))
            {
                if (TryChangeStance(context, actor, "enter-berserker-stance"))
                    _returnToBerserker = false;
            }
            else if (InStance(ForeverWarriorAuraKeys.BerserkerStance))
                _returnToBerserker = false;
            return;
        }
        if (!InStance(ForeverWarriorAuraKeys.BerserkerStance) || !opportunity ||
            !actor.Abilities.TryGetValue(ForeverOverpowerFactory.AbilityKey, out var ability) ||
            !ability.IsReady(context.CurrentTimeSeconds)) return;
        if (!actor.Resources.TryGetValue("rage", out var rage) ||
            rage.Current < 5m || rage.Current > _maximumRage ||
            _retainedRage < 5m) return;
        if (TryChangeStance(context, actor, "enter-battle-stance"))
            _returnToBerserker = true;
    }

    private bool TryChangeStance(SimulationContext context, SimulationActorState actor, string abilityKey)
    {
        if (!actor.IsSharedCooldownReady("warrior-stance", context.CurrentTimeSeconds))
        {
            ScheduleDecision(context, actor.GetSharedCooldownReadyAt("warrior-stance", context.CurrentTimeSeconds));
            return false;
        }
        var success = _executor.TryStartAbility(context, Warrior, Warrior, abilityKey).Success;
        if (success)
            ScheduleDecision(context, actor.GetSharedCooldownReadyAt("warrior-stance", context.CurrentTimeSeconds));
        return success;
    }

    private void ScheduleDecision(SimulationContext context, decimal at)
    {
        if (at <= context.CurrentTimeSeconds || at >= context.Options.DurationSeconds) return;
        if (!_scheduledWakeups.Add(at)) return;
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = at, Type = CombatEventType.RotationDecision,
            SourceActorKey = Warrior, TargetActorKey = Target, EffectKey = StanceWakeupKey, IsInternal = true,
            Description = "Stance change available again."
        });
    }
}
