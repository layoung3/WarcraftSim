using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Provisional Forever 70707: Frostbite 5/10/15% after a landed Chill,
/// target-owned Frozen marker for five seconds. Stationary solo enemies
/// have no real movement/cast AI to interrupt. No low-rank proc farming.
/// Source: https://wowforevertalents.com/mage/talents/frostbite/
/// </summary>
public sealed class MageFrostbiteProcProcessor : ICombatEventProcessor
{
    private readonly SimulationActorState _mage;
    private readonly AuraManager _auras;
    private readonly int _ranks;
    private readonly int _blizzardRanks;
    private readonly HashSet<(Guid,decimal,string)> _rolled=[];
    public int Procs {get;private set;}
    public MageFrostbiteProcProcessor(SimulationActorState mage,AuraManager auras,
        int ranks,int blizzardRanks)
    {
        if(ranks is <0 or >3)throw new ArgumentOutOfRangeException(nameof(ranks));
        _mage=mage;_auras=auras;_ranks=ranks;_blizzardRanks=blizzardRanks;
    }
    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(_ranks<=0||ev.Type!=CombatEventType.Damage||
            ev.SourceActorKey!=_mage.Key || ev.TargetActorKey is not { } key ||
            ev.ResultKey is not ("hit" or "critical") || ev.Amount is not >0m)
            return;
        var eligible=(!ev.IsPeriodic&&!ev.IsChannelTick &&
            ev.AbilityKey is ("frostbolt" or "frost-blast" or "frostfire-bolt")) ||
            (ev.IsChannelTick && ev.AbilityKey=="blizzard"&&_blizzardRanks>0);
        if(!eligible)return;
        if(ev.AbilityExecutionId is { } id &&
            !_rolled.Add((id,ev.IsChannelTick?ev.TimeSeconds:-1m,key)))return;
        if(context.GetActor(key) is not { TeamKey:"enemy", IsAlive:true } victim)
            return;
        if((decimal)context.Random.NextDouble()*100m >= 5m*_ranks)return;
        _auras.ApplyAura(context,victim,new AuraDefinition {
            Key="mage-frostbite-freeze",Name="Frostbite (Frozen)",
            DurationSeconds=5m
        },_mage.Key,"frostbite","frostbite-freeze");
        Procs++;
    }
}
