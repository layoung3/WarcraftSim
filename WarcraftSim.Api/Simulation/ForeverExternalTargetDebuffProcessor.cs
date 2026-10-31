using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Explicit external support auras. "initial-only" expires naturally.
/// "maintained" replaces the aura at expiry without granting damage, a cast,
/// or a stacking second stat source. All stat adjustments are balanced by
/// the ORIGINAL aura InstanceId, including a boundary at encounter end.
/// </summary>
public sealed class ForeverExternalTargetDebuffProcessor : ICombatEventProcessor
{
    private readonly AuraManager _auras;
    private readonly string _targetKey;
    private readonly IReadOnlyList<ForeverExternalModifierDefinition> _effects;
    private readonly bool _maintained;
    private readonly Dictionary<Guid,ForeverExternalModifierDefinition> _active=[];
    public int Applications { get; private set; }

    public ForeverExternalTargetDebuffProcessor(
        ForeverExternalModifierSelection selection,int level,
        AuraManager auras,string targetKey="target")
    {
        _auras=auras??throw new ArgumentNullException(nameof(auras));
        _targetKey=targetKey;
        // Enforce valid policy and all the catalog's source/stacking/level gates.
        _effects=ForeverExternalModifierCatalog.Resolve(selection,level)
            .Where(x=>x.Category=="target-debuff").ToArray();
        _maintained=selection.TargetDebuffMode=="maintained";
    }

    private void Apply(SimulationContext context,SimulationActorState target,
        ForeverExternalModifierDefinition effect)
    {
        var aura=_auras.ApplyAura(context,target,new AuraDefinition {
            Key="cp173-"+effect.Key,Name=effect.Name,
            DurationSeconds=effect.DurationSeconds,
            StackingMode=AuraStackingMode.Replace
        },"configured-external",effect.Key,"cp173-external");
        foreach(var (stat,value) in effect.Stats)
            target.Stats.Add(stat,value);
        _active.Add(aura.InstanceId,effect);
        Applications++;
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type==CombatEventType.SimulationStarted)
        {
            var target=context.GetActor(_targetKey) ??
                throw new InvalidOperationException($"External debuff target '{_targetKey}' missing.");
            foreach(var effect in _effects)
                Apply(context,target,effect);
        }
        if(ev.Type==CombatEventType.AuraRemoved &&
           string.Equals(ev.TargetActorKey,_targetKey,StringComparison.OrdinalIgnoreCase) &&
           ev.AuraInstanceId is Guid id && _active.Remove(id,out var effect))
        {
            var target=context.GetActor(_targetKey);
            if(target is null)return;
            foreach(var (stat,value) in effect.Stats)
                target.Stats.Add(stat,-value);

            // Reapply only once a real aura instance has expired; no extra
            // effects, additional stacks, spell damage or resource benefits.
            // Do not reapply at/after the simulation's final timestamp.
            if(_maintained && target.IsAlive &&
               context.CurrentTimeSeconds+0.000001m<context.Options.DurationSeconds)
                Apply(context,target,effect);
        }
    }
}
