using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Abilities.Warrior;

namespace WarcraftSim.Api.Simulation;

/// <summary>Damage-to-threat foundation. Only damaging hits to live enemies
/// generate base threat; explicit ability Threat effects remain additive.</summary>
public sealed class WarriorDamageThreatProcessor : ICombatEventProcessor
{
    private readonly bool _enabled;
    private readonly int _defianceRank;
    private readonly bool _shield;

    public WarriorDamageThreatProcessor(bool enabled, WarriorSimRequest? request = null)
    {
        _enabled = enabled;
        _defianceRank = request is null ? 0 : ForeverWarriorTalentCatalog.Rank(request, "defiance");
        _shield = request?.ShieldEquipped == true;
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (!_enabled) return;
        if (ev.Type != CombatEventType.Damage || ev.SourceActorKey != "warrior" ||
            string.IsNullOrWhiteSpace(ev.TargetActorKey) || (ev.Amount ?? 0m) <= 0m) return;
        var target = context.GetActor(ev.TargetActorKey);
        if (target is null || target.TeamKey != "enemy") return;
        var source = context.GetActor("warrior");
        if (source is null) return;
        // Verified base stance modifiers; special ability bonus threat and
        // Defiance are additive through separate ability/talent systems.
        var defensive = source.ActiveAuras.Any(a =>
            a.Definition.Key == ForeverWarriorAuraKeys.DefensiveStance &&
            a.IsActiveAt(context.CurrentTimeSeconds));
        var battle = source.ActiveAuras.Any(a =>
            a.Definition.Key == ForeverWarriorAuraKeys.BattleStance &&
            a.IsActiveAt(context.CurrentTimeSeconds));
        var threatMultiplier = defensive ? 1.30m : battle ? 0.80m : 1m;
        if (defensive && _shield)
            threatMultiplier *= 1m + _defianceRank * 0.05m;
        var threat = ev.Amount!.Value * threatMultiplier;
        target.ThreatTable.AddThreat(source.Key, threat);
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.ThreatChanged,
            SourceActorKey = source.Key, TargetActorKey = target.Key,
            AbilityKey = ev.AbilityKey, Amount = threat,
            Description = $"{source.Name} generated {threat:0.##} base threat on {target.Name}."
        });
    }
}
