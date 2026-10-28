using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Bane of Havoc copies 15% of the owner's effective damage to OTHER targets
/// to the single target carrying the owner's Bane. It never echoes its own
/// echoes, another actor's damage, or a hit against the Baned victim.
/// </summary>
public sealed class WarlockBaneOfHavocProcessor(AuraManager auras, AbilityExecutor executor)
    : ICombatEventProcessor
{
    public const string AuraKey = "bane-of-havoc";
    public const string EchoKey = "bane-of-havoc-echo";

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (!string.Equals(ev.SourceActorKey, "warlock", StringComparison.OrdinalIgnoreCase))
            return;
        if (ev.Type == CombatEventType.AuraApplied &&
            ev.TargetActorKey is { } appliedTarget)
        {
            if (ev.AbilityKey == "bane-of-havoc")
            {
                // At most one Bane of Havoc target per owner, including on
                // refreshes and target swaps.
                foreach (var other in context.Actors.Values.Where(a =>
                    a.Key != appliedTarget &&
                    !string.Equals(a.TeamKey, context.GetActor("warlock")?.TeamKey,
                        StringComparison.OrdinalIgnoreCase)))
                    auras.RemoveAuras(context, other, AuraKey, "warlock",
                        "Bane of Havoc moved to a new target");
                // Banes are exclusive on one target for the same Warlock.
                if (context.GetActor(appliedTarget) is { } target)
                {
                    auras.RemoveAuras(context,target,"bane-of-agony","warlock",
                        "Bane of Havoc replaced Bane of Agony");
                    auras.RemoveAuras(context,target,"bane-of-doom","warlock",
                        "Bane of Havoc replaced Bane of Doom");
                }
            }
            else if (ev.AbilityKey == "bane-of-agony" &&
                context.GetActor(appliedTarget) is { } agonyTarget)
            {
                auras.RemoveAuras(context,agonyTarget,AuraKey,"warlock",
                    "Bane of Agony replaced Bane of Havoc");
                auras.RemoveAuras(context,agonyTarget,"bane-of-doom","warlock",
                    "Bane of Agony replaced Bane of Doom");
            }
            else if (ev.AbilityKey == "bane-of-doom" &&
                context.GetActor(appliedTarget) is { } doomTarget)
            {
                auras.RemoveAuras(context,doomTarget,"bane-of-agony","warlock",
                    "Bane of Doom replaced Bane of Agony");
                auras.RemoveAuras(context,doomTarget,AuraKey,"warlock",
                    "Bane of Doom replaced Bane of Havoc");
            }
            return;
        }
        if (ev.Type != CombatEventType.Damage || ev.Amount is not > 0m ||
            ev.TargetActorKey is null || ev.AbilityKey == EchoKey ||
            context.GetActor("warlock") is not { IsAlive: true } caster ||
            context.GetActor(ev.TargetActorKey) is not { } originalVictim ||
            string.Equals(originalVictim.TeamKey, caster.TeamKey,
                StringComparison.OrdinalIgnoreCase))
            return;

        var victim=context.Actors.Values.FirstOrDefault(a =>
            a.IsAlive && a.Key!=ev.TargetActorKey &&
            a.ActiveAuras.Any(buff => buff.Definition.Key == AuraKey &&
                buff.SourceActorKey=="warlock" &&
                buff.IsActiveAt(context.CurrentTimeSeconds)));
        if(victim is null)return;
        var reflected=ev.Amount.Value*.15m;
        if(reflected<=0m)return;
        executor.ExecuteBackgroundDirectDamage(context,caster,victim,
            EchoKey,"Bane of Havoc",new AbilityEffectDefinition {
                Key="havoc-echo",EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,
                SchoolKey=ev.SchoolKey ?? "shadow",
                ResolutionType=CombatResolutionTypes.AlwaysHits,
                MitigationType=DamageMitigationTypes.None,
                CanMiss=false,CanCrit=false,
                MinimumValue=reflected,MaximumValue=reflected
            });
    }
}
