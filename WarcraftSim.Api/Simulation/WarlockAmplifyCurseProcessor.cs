using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Amplify Curse is consumed by the next successfully applied Bane of Agony.
/// Its +50% Bane damage is attached to the target's source-owned Bane aura
/// and survives only that application's 24s lifetime. An ordinary Bane
/// refresh clears the amplified status rather than inheriting stale damage.
/// </summary>
public sealed class WarlockAmplifyCurseProcessor(AuraManager auras)
    : ICombatEventProcessor
{
    public int AmplifiedBanes { get; private set; }
    public void Process(SimulationContext context,CombatEvent ev)
    {
        // Amplification belongs to a specific Agony application. Removing
        // that Bane must remove its multiplier; an old aura removal must not
        // delete a newly refreshed Agony's multiplier.
        if(ev.Type==CombatEventType.AuraRemoved &&
            ev.AbilityKey=="bane-of-agony" && ev.SourceActorKey=="warlock" &&
            ev.TargetActorKey is { } removedTarget &&
            context.GetActor(removedTarget) is { } affected &&
            !affected.ActiveAuras.Any(a=>a.Definition.Key=="bane-of-agony" &&
                a.SourceActorKey=="warlock" &&
                a.IsActiveAt(context.CurrentTimeSeconds)))
        {
            auras.RemoveAuras(context,affected,"warlock-amplified-agony",
                "warlock","Bane of Agony removed");
            return;
        }
        if(ev.Type!=CombatEventType.AuraApplied ||
            ev.AbilityKey!="bane-of-agony" || ev.SourceActorKey!="warlock" ||
            ev.TargetActorKey is not { } targetKey || targetKey=="warlock")
            return;
        var warlock=context.GetActor("warlock");
        var target=context.GetActor(targetKey);
        if(warlock is null || target is null ||
           !target.ActiveAuras.Any(a=>a.Definition.Key=="bane-of-agony" &&
                a.SourceActorKey==warlock.Key && a.IsActiveAt(context.CurrentTimeSeconds)))
            return;
        var empowered=warlock.ActiveAuras.Any(a=>
            a.Definition.Key=="warlock-amplify-curse" &&
            a.SourceActorKey==warlock.Key && a.IsActiveAt(context.CurrentTimeSeconds));
        auras.RemoveAuras(context,target,"warlock-amplified-agony",
            warlock.Key,"Bane refresh");
        if(!empowered)return;
        auras.ApplyAura(context,target,new AuraDefinition{
            Key="warlock-amplified-agony",Name="Amplified Bane of Agony",
            DurationSeconds=24m
        },warlock.Key,"amplify-curse","agony-amplification");
        auras.RemoveAuras(context,warlock,"warlock-amplify-curse",
            warlock.Key,"empowered Bane applied");
        AmplifiedBanes++;
    }
}
