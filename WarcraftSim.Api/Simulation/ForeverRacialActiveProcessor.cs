using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// CP173 supported racial active: Orc Blood Fury, Forever spell 20572.
/// Published 70532: +10% Attack Power and Spell Power for 15s, 180s CD.
/// Auto-use is an explicit solo-simulation option, never implicit.
/// </summary>
public sealed class ForeverRacialActiveProcessor : ICombatEventProcessor
{
    public const string BloodFuryEffect="cp173-orc-blood-fury";
    public const string SourceUrl="https://wow-forever.gg/db/spells/20572-blood-fury/";
    private readonly string _actorKey;
    private readonly AuraManager _auras;
    private readonly Action<SimulationContext,SimulationActorState,IReadOnlyDictionary<string,decimal>>?
        _onStatDelta;
    private readonly bool _enabled;
    private readonly Dictionary<string,decimal> _active=new(StringComparer.OrdinalIgnoreCase);
    private Guid? _instance;
    private decimal _started;
    private decimal _uptime;
    public int Activations { get; private set; }

    public ForeverRacialActiveProcessor(string actorKey,string? race,bool enabled,
        AuraManager auras,
        Action<SimulationContext,SimulationActorState,IReadOnlyDictionary<string,decimal>>?
            onStatDelta=null)
    {
        if(enabled && !string.Equals(race,"orc",StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only the sourced Orc Blood Fury racial is executable in CP173.");
        _actorKey=actorKey;_enabled=enabled;_auras=auras;_onStatDelta=onStatDelta;
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(!_enabled)return;
        if(ev.Type==CombatEventType.SimulationStarted)
        {
            Activate(context);
            return;
        }
        if(ev.Type==CombatEventType.EquipmentEffectActivation &&
           ev.SourceActorKey==_actorKey && ev.EffectKey==BloodFuryEffect)
        {
            Activate(context);
            return;
        }
        if(ev.Type==CombatEventType.AuraRemoved && ev.AuraInstanceId==_instance &&
           ev.TargetActorKey==_actorKey)
        {
            var actor=context.GetActor(_actorKey);
            if(actor is not null)
            {
                var reversed=_active.ToDictionary(x=>x.Key,x=>-x.Value,
                    StringComparer.OrdinalIgnoreCase);
                foreach(var (key,value) in reversed)actor.Stats.Add(key,value);
                _onStatDelta?.Invoke(context,actor,reversed);
            }
            _uptime+=Math.Max(0m,context.CurrentTimeSeconds-_started);
            _active.Clear();_instance=null;
        }
        if(ev.Type==CombatEventType.SimulationEnded && _instance.HasValue)
            _uptime+=Math.Max(0m,context.CurrentTimeSeconds-_started);
    }

    private void Activate(SimulationContext context)
    {
        var actor=context.GetActor(_actorKey);
        if(actor is null || !actor.IsAlive || _instance.HasValue)return;
        _active["attack-power"]=Math.Max(0m,actor.Stats.Get("attack-power"))*.10m;
        _active["spell-power"]=Math.Max(0m,actor.Stats.Get("spell-power"))*.10m;
        var aura=_auras.ApplyAura(context,actor,new AuraDefinition {
            Key=BloodFuryEffect,Name="Blood Fury",DurationSeconds=15m,
            StackingMode=AuraStackingMode.Refresh
        },_actorKey,BloodFuryEffect,BloodFuryEffect);
        _instance=aura.InstanceId;_started=context.CurrentTimeSeconds;
        foreach(var (key,value) in _active)actor.Stats.Add(key,value);
        _onStatDelta?.Invoke(context,actor,_active);
        Activations++;
        var next=context.CurrentTimeSeconds+180m;
        if(next<context.Options.DurationSeconds)
            context.ScheduleEvent(new CombatEvent {
                Type=CombatEventType.EquipmentEffectActivation,TimeSeconds=next,
                SourceActorKey=_actorKey,EffectKey=BloodFuryEffect,IsInternal=true,
                Description="Orc Blood Fury personal cooldown ready"
            });
    }
    public void WriteMetrics(SimulationRunResult run)
    {
        run.CustomMetrics["cp173-racial-blood-fury-activations"]=Activations;
        run.CustomMetrics["cp173-racial-blood-fury-uptime-seconds"]=_uptime;
    }
}
