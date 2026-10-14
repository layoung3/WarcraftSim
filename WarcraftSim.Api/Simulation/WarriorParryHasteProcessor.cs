using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Player-side parry haste; applies to an existing in-flight main-hand
/// auto-attack only. Boss-side parry haste is scheduled by the boss processor.</summary>
public sealed class WarriorParryHasteProcessor : ICombatEventProcessor
{
    private readonly AutoAttackProcessor _autos;
    private readonly bool _enabled;

    public WarriorParryHasteProcessor(AutoAttackProcessor autos, bool enabled)
    {
        _autos = autos;
        _enabled = enabled;
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (!_enabled || ev.Type != CombatEventType.Damage ||
            ev.AbilityKey != "boss-main-hand-auto" ||
            ev.TargetActorKey != "warrior" || ev.ResultKey != "parry") return;
        _autos.ApplyParryHaste(context, "warrior", "main-hand-auto");
    }
}
