using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Each landed Hurricane channel tick rolls separately on eligible adds.
/// Do not send a synthetic channel tick back through this processor.</summary>
public sealed class BalanceHurricaneCleaveProcessor(string druidKey,
    AbilityExecutor executor, int maximumSecondaryTargets) : ICombatEventProcessor
{
    public int SecondaryHits { get; private set; }
    public void Process(SimulationContext ctx,CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.Damage || ev.SourceActorKey!=druidKey ||
            ev.AbilityKey!="hurricane" || !ev.IsChannelTick ||
            ev.Amount is not >0m || ev.TargetActorKey is null) return;
        var caster=ctx.GetActor(druidKey);
        if(caster is null || !caster.Abilities.TryGetValue("hurricane",out var ability))return;
        var effect=ability.Definition.Effects.First(e=>e.Key=="hurricane-tick");
        foreach(var target in ctx.Actors.Values.Where(a=>a.IsAlive && a.TeamKey!=caster.TeamKey &&
            a.Key!=ev.TargetActorKey).OrderBy(a=>a.Key,StringComparer.Ordinal).Take(maximumSecondaryTargets))
        {
            executor.ExecuteBackgroundDirectDamage(ctx,caster,target,"hurricane-secondary",
                "Hurricane (additional target)",new AbilityEffectDefinition {
                    Key="hurricane-secondary-tick",EffectType=AbilityEffectTypes.DirectDamage,
                    TargetType=AbilityTargetTypes.Enemy,SchoolKey="nature",
                    ResolutionType=CombatResolutionTypes.Spell,CanMiss=true,CanCrit=true,
                    MinimumValue=effect.MinimumValue,MaximumValue=effect.MaximumValue
                });
            SecondaryHits++;
        }
    }
}
