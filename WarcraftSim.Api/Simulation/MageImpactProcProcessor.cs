using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Provisional Forever 70707 talent Impact: 3/7/10% on landed direct Fire
/// spells; the 2-second stun is observable, but the stationary solo boss has
/// no enemy cast or movement AI to interrupt. Lower-level rank penalty unknown.
/// https://www.wowhead.com/forever/spell=11103/impact
/// </summary>
public sealed class MageImpactProcProcessor : ICombatEventProcessor
{
    private readonly SimulationActorState _mage;
    private readonly AuraManager _auras;
    private readonly int _ranks;
    private readonly HashSet<Guid> _rolledExecutions=[];
    public int Procs {get;private set;}
    public MageImpactProcProcessor(SimulationActorState mage,AuraManager auras,int ranks)
    {
        if(ranks is <0 or >3)throw new ArgumentOutOfRangeException(nameof(ranks));
        _mage=mage;_auras=auras;_ranks=ranks;
    }
    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(_ranks==0||ev.Type!=CombatEventType.Damage ||
           ev.SourceActorKey!=_mage.Key || ev.TargetActorKey is not { } victim ||
           ev.SchoolKey!="fire" || ev.IsPeriodic || ev.IsChannelTick ||
           ev.Amount is not >0m || ev.ResultKey is not ("hit" or "critical") ||
           ev.AbilityKey is not ("fireball" or "fire-blast" or "scorch" or
               "pyroblast" or "frostfire-bolt" or "flamestrike" or "blast-wave"))
            return;
        if(ev.AbilityExecutionId is { } id && !_rolledExecutions.Add(id))return;
        var enemy=context.GetActor(victim);
        if(enemy is null || enemy.TeamKey==_mage.TeamKey || !enemy.IsAlive)return;
        var chance=_ranks switch {1=>3m,2=>7m,3=>10m,_=>0m};
        if((decimal)context.Random.NextDouble()*100m>=chance)return;
        _auras.ApplyAura(context,enemy,new AuraDefinition {
            Key="mage-impact-stun",Name="Impact (stunned)",DurationSeconds=2m
        },_mage.Key,"impact","impact-stun");
        Procs++;
    }
}
