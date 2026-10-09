using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Converts resolved attack outcomes into target-specific reaction windows.
/// The source must own the corresponding gated ability. Event outcomes are
/// handled after combat resolution, never predicted from the attack table.
/// </summary>
public sealed class ReactiveAbilityOpportunityProcessor : ICombatEventProcessor
{
    public void Process(SimulationContext context, CombatEvent combatEvent)
    {
        if (combatEvent.Type != CombatEventType.Damage ||
            combatEvent.EffectDeliveryType != CombatEffectDeliveryType.Direct ||
            string.IsNullOrWhiteSpace(combatEvent.SourceActorKey) ||
            string.IsNullOrWhiteSpace(combatEvent.TargetActorKey) ||
            string.IsNullOrWhiteSpace(combatEvent.ResolutionType))
            return;

        var source = context.GetActor(combatEvent.SourceActorKey);
        var target = context.GetActor(combatEvent.TargetActorKey);
        if (source is null || target is null || !source.IsAlive || !target.IsAlive ||
            source.TeamKey == target.TeamKey)
            return;

        foreach (var definition in source.ReactiveOpportunityDefinitions)
        {
            if (!source.Abilities.ContainsKey(definition.AbilityKey) ||
                !definition.EligibleResolutionTypes.Contains(combatEvent.ResolutionType))
                continue;

            if (string.Equals(combatEvent.ResultKey, CombatResultTypes.Dodge,
                    StringComparison.OrdinalIgnoreCase))
            {
                // Only dodged weapon attacks provide this opportunity.
                if (string.Equals(combatEvent.WeaponHandKey, WeaponHandKeys.MainHand,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(combatEvent.WeaponHandKey, WeaponHandKeys.OffHand,
                        StringComparison.OrdinalIgnoreCase))
                    Grant(context, source, target, definition,
                        definition.DodgeWindowSeconds, "dodged attack");
                continue;
            }

            if (definition.ProcChancePercent <= 0m ||
                !string.Equals(combatEvent.WeaponHandKey, WeaponHandKeys.MainHand,
                    StringComparison.OrdinalIgnoreCase) ||
                !IsLanded(combatEvent.ResultKey) ||
                !target.ActiveAuras.Any(aura =>
                    aura.IsActiveAt(context.CurrentTimeSeconds) &&
                    string.Equals(aura.SourceActorKey, source.Key, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(aura.Definition.Key, definition.RequiredTargetAuraKey,
                        StringComparison.OrdinalIgnoreCase)))
                continue;

            // The proc is per landed main-hand strike/target. A queued Heroic
            // Strike or Cleave hit is a direct melee attack with the same hand.
            if (context.Random.NextDouble() * 100d < (double)definition.ProcChancePercent)
                Grant(context, source, target, definition,
                    definition.ProcWindowSeconds, "landed main-hand proc");
        }
    }

    private static bool IsLanded(string? resultKey) =>
        string.Equals(resultKey, CombatResultTypes.Hit, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(resultKey, CombatResultTypes.Critical, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(resultKey, CombatResultTypes.Glancing, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(resultKey, CombatResultTypes.Block, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(resultKey, CombatResultTypes.Crushing, StringComparison.OrdinalIgnoreCase);

    private static void Grant(SimulationContext context,
        SimulationActorState source, SimulationActorState target,
        ReactiveAbilityOpportunityDefinition definition,
        decimal durationSeconds, string reason)
    {
        source.GrantReactiveOpportunity(definition.OpportunityKey,
            target.Key, context.CurrentTimeSeconds, durationSeconds);
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds,
            Type = CombatEventType.ReactiveOpportunityGranted,
            SourceActorKey = source.Key,
            TargetActorKey = target.Key,
            AbilityKey = definition.AbilityKey,
            Description = $"{definition.OpportunityKey} available on {target.Key} for {durationSeconds:0.##} seconds ({reason})."
        });
    }
}
