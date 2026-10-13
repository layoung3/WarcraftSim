using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Spawns and despawns scripted DPS targets through the existing event queue.
/// Add health is intentionally very large; target availability is determined by
/// the scripted window, never by player DPS in a fixed-duration preview.</summary>
public sealed class WarriorAddLifecycleProcessor : ICombatEventProcessor
{
    public const string SpawnEffect = "warrior-add-spawn";
    public const string DespawnEffect = "warrior-add-despawn";
    public const decimal PreviewHealth = 1_000_000_000_000_000_000m;

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.Type != CombatEventType.RotationDecision ||
            ev.EffectKey is not (SpawnEffect or DespawnEffect) ||
            ev.TargetActorKey is null) return;
        var add = context.GetActor(ev.TargetActorKey);
        if (add is null || add.TeamKey != "enemy") return;
        if (ev.EffectKey == SpawnEffect)
            add.InitializeHealth(PreviewHealth);
        else
            add.InitializeHealth(PreviewHealth, 0m);

        // The boss remains the default target. Reevaluate the priority as
        // soon as available enemy count changes (including Cleave gating).
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds,
            Type = CombatEventType.RotationDecision,
            SourceActorKey = "warrior", TargetActorKey = "target",
            IsInternal = true,
            Description = ev.EffectKey == SpawnEffect ? "Add became active" : "Add left combat"
        });
    }
}
