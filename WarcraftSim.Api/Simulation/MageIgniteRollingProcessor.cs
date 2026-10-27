using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever Ignite per-Mage rolling damage. A second direct Fire crit carries
/// forward the premitigation damage not yet paid by the previous aura.
/// Uses the Stage 3 generic aura instance/tick scheduler; no second DoT engine.
/// </summary>
public sealed class MageIgniteRollingProcessor(AuraManager auras, int igniteRank)
    : ICombatEventProcessor
{
    public const string IgniteAbilityKey = "mage-ignite";

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (igniteRank <= 0 || ev.Type != CombatEventType.Damage ||
            !ev.IsCritical || ev.IsPeriodic || ev.IsChannelTick ||
            ev.Amount is not > 0m ||
            !string.Equals(ev.SourceActorKey,"mage",StringComparison.OrdinalIgnoreCase) ||
            ev.TargetActorKey is null ||
            ev.AbilityKey is not ("fireball" or "fire-blast" or "pyroblast" or "frostfire-bolt") ||
            !string.Equals(ev.SchoolKey,"fire",StringComparison.OrdinalIgnoreCase))
            return;

        var source=context.GetActor("mage");
        var victim=context.GetActor(ev.TargetActorKey);
        if(source is null || victim is not { IsAlive:true } ||
            !source.Abilities.TryGetValue(IgniteAbilityKey,out var abilityState))
            return;

        var ability=abilityState.Definition;
        var effect=ability.Effects.Single(e=>e.EffectType==AbilityEffectTypes.PeriodicDamage);
        var duration=effect.DurationSeconds!.Value;
        var interval=effect.TickIntervalSeconds!.Value;
        var key=effect.AuraKey ?? effect.Key;
        var prev=victim.ActiveAuras.FirstOrDefault(a=>
            a.Definition.Key==key && a.SourceActorKey==source.Key &&
            a.IsActiveAt(context.CurrentTimeSeconds));
        // Full critical strike damage has already been resolved and mitigated.
        // The next tick should not reapply school damage bonuses.
        var newPool=ev.Amount.Value*.08m*Math.Clamp(igniteRank,0,5);
        var carried=prev?.RollingDamageRemaining ?? 0m;
        var total=carried+newPool;
        if(total<=0m)return;

        var aura=auras.ApplyAura(context,victim,new AuraDefinition {
            Key=key,Name="Ignite",DurationSeconds=duration,
            PeriodicTickIntervalSeconds=interval,
            IncludeExpirationBoundaryTick=effect.IncludeExpirationBoundaryTick,
            StackingMode=AuraStackingMode.Refresh
        },source.Key,ability.Key,effect.Key,scheduleExpiration:false);
        aura.RollingDamageRemaining=total;
        aura.RollingTicksRemaining=PeriodicEffectScheduler.GetScheduledTickCount(
            duration,interval,effect.IncludeExpirationBoundaryTick);
        PeriodicEffectScheduler.ScheduleTicks(context,source,victim,ability,
            effect,aura,abilityExecutionId:null);
        auras.ScheduleExpiration(context,aura);
    }
}
