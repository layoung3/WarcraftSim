using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Current Forever Improved Scorch (build 70532): Rank 1/2/3 has 33/67/100%
/// chance on a landed direct Scorch to apply +3% Fire vulnerability to damage
/// from this Mage only. Stacks cap at five, last thirty seconds and belong to
/// the actual target rather than an actor-wide boss fixture. A Scorch cannot
/// benefit from the stack it creates; damage rolls occur before this listener.
/// </summary>
public sealed class MageImprovedScorchProcessor : ICombatEventProcessor
{
    private readonly SimulationActorState _mage;
    private readonly AuraManager _auras;
    private readonly int _rank;
    private readonly Dictionary<string, (int Stacks,decimal ExpiresAt)> _targets =
        new(StringComparer.OrdinalIgnoreCase);

    public int Applications { get; private set; }
    public int Expirations { get; private set; }
    public int GetStacks(string targetKey, decimal now) =>
        _targets.TryGetValue(targetKey, out var s) && s.ExpiresAt > now ? s.Stacks : 0;

    public MageImprovedScorchProcessor(SimulationActorState mage, AuraManager auras,int rank)
    {
        _mage=mage ?? throw new ArgumentNullException(nameof(mage));
        _auras=auras ?? throw new ArgumentNullException(nameof(auras));
        if(rank is <0 or >3)throw new ArgumentOutOfRangeException(nameof(rank));
        _rank=rank;
    }

    public decimal FireDamageMultiplier(string targetKey, decimal now) =>
        1m + GetStacks(targetKey,now)*.03m;

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if(_rank==0)return;
        if(ev.Type==CombatEventType.ActorDied && ev.TargetActorKey is { } deceased)
        {
            _targets.Remove(deceased);
            return;
        }
        if(ev.Type==CombatEventType.AuraRemoved && ev.EffectKey=="improved-scorch-stack" &&
           ev.TargetActorKey is { } removedKey)
        {
            // Replaced/expired aura events may coexist with a refresh on this
            // timestamp; only retire state if it truly has no active lifetime.
            if(GetStacks(removedKey,context.CurrentTimeSeconds)==0 &&
               _targets.Remove(removedKey)) Expirations++;
            return;
        }
        if(ev.Type!=CombatEventType.Damage || ev.AbilityKey!="scorch" ||
            ev.SourceActorKey!=_mage.Key || ev.TargetActorKey is not { } key ||
            ev.IsPeriodic || ev.IsChannelTick || ev.Amount is not >0m ||
            ev.ResultKey is not ("hit" or "critical"))return;
        var enemy=context.GetActor(key);
        if(enemy is null || enemy.TeamKey==_mage.TeamKey)return;
        var chance=_rank switch {1=>33m,2=>67m,3=>100m,_=>0m};
        if((decimal)context.Random.NextDouble()*100m>=chance)return;

        var next=Math.Min(5,GetStacks(key,context.CurrentTimeSeconds)+1);
        _targets[key]=(next,context.CurrentTimeSeconds+30m);
        _auras.ApplyAura(context,enemy,new AuraDefinition {
            Key="mage-improved-scorch-vulnerability",Name="Improved Scorch",
            DurationSeconds=30m
        },_mage.Key,"improved-scorch","improved-scorch-stack");
        Applications++;
    }
}
