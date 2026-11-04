using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Scatter Shot (spell 19503). Combat damage and landed aura application
/// remain in the existing AbilityExecutor. This processor only handles the
/// two non-damage rules from the tooltip: stop Auto Shot when used, and
/// remove the four-second disorient when the victim takes later damage.
/// The marker does NOT implement target AI/cast interruption or 15-yard geometry.
/// </summary>
public sealed class HunterScatterShotProcessor(
    AuraManager auras, AutoAttackProcessor autoAttacks) : ICombatEventProcessor
{
    public const string DisorientAura="hunter-scatter-shot-disorient";
    public int AutoShotStops { get; private set; }
    public int DisorientsBroken { get; private set; }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if(ev.Type==CombatEventType.AbilityCastCompleted &&
           ev.SourceActorKey=="hunter" && ev.AbilityKey=="scatter-shot")
        {
            if(autoAttacks.Stop(context,"hunter","auto-shot"))
                AutoShotStops++;
            return;
        }
        if(ev.Type!=CombatEventType.Damage || ev.Amount is not >0m ||
           ev.TargetActorKey is not { } targetKey ||
           ev.AbilityKey=="scatter-shot")return;
        var target=context.GetActor(targetKey);
        if(target is null || !target.ActiveAuras.Any(a=>
           a.Definition.Key==DisorientAura && a.SourceActorKey=="hunter" &&
           a.IsActiveAt(context.CurrentTimeSeconds)))return;
        DisorientsBroken+=auras.RemoveAuras(context,target,DisorientAura,
            "hunter","damage broke Scatter Shot disorient");
    }
}
