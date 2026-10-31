using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Applies explicitly preconfigured external debuffs to enemy actors at the
/// start of a solo encounter. The normal aura manager owns exact expiration,
/// and stat deltas are removed only for the original aura instance.
/// </summary>
public sealed class ForeverExternalTargetDebuffProcessor : ICombatEventProcessor
{
    private readonly AuraManager _auras;
    private readonly string _targetKey;
    private readonly IReadOnlyList<ForeverExternalModifierDefinition> _effects;
    private readonly Dictionary<Guid,IReadOnlyDictionary<string,decimal>> _active=[];
    public int Applications { get; private set; }

    public ForeverExternalTargetDebuffProcessor(
        ForeverExternalModifierSelection selection,int level,
        AuraManager auras,string targetKey="target")
    {
        _auras=auras??throw new ArgumentNullException(nameof(auras));
        _targetKey=targetKey;
        _effects=ForeverExternalModifierCatalog.Resolve(selection,level)
            .Where(x=>x.Category=="target-debuff").ToArray();
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type==CombatEventType.SimulationStarted)
        {
            var target=context.GetActor(_targetKey) ??
                throw new InvalidOperationException($"External debuff target '{_targetKey}' missing.");
            foreach(var effect in _effects)
            {
                var aura=_auras.ApplyAura(context,target,new AuraDefinition {
                    Key="cp173-"+effect.Key,Name=effect.Name,
                    DurationSeconds=effect.DurationSeconds,
                    StackingMode=AuraStackingMode.Replace
                },"configured-external",effect.Key,"cp173-external");
                foreach(var (stat,value) in effect.Stats)
                    target.Stats.Add(stat,value);
                _active.Add(aura.InstanceId,effect.Stats);
                Applications++;
            }
        }
        if(ev.Type==CombatEventType.AuraRemoved &&
           string.Equals(ev.TargetActorKey,_targetKey,StringComparison.OrdinalIgnoreCase) &&
           ev.AuraInstanceId is Guid id && _active.Remove(id,out var delta))
        {
            var target=context.GetActor(_targetKey);
            if(target is null)return;
            foreach(var (stat,value) in delta)
                target.Stats.Add(stat,-value);
        }
    }
}
