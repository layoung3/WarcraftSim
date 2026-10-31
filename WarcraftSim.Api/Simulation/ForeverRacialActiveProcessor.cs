using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Shared CP173 racial active executor. Only published current-build numeric
/// effects with known duration and cooldown are implemented. In-game use is
/// opt-in for solo DPS. Future racial effects use this same event/auras path.
/// </summary>
public sealed class ForeverRacialActiveProcessor : ICombatEventProcessor
{
    public const string BloodFuryEffect="cp173-orc-blood-fury";
    public const string BerserkingEffect="cp173-troll-berserking";
    public const string ElunesLightEffect="cp173-night-elf-elunes-light";
    public const string SourceUrl="https://wow-forever.gg/db/spells/20572-blood-fury/";
    private readonly string _actorKey,_race,_effectKey,_displayName;
    private readonly decimal _duration;
    private readonly AuraManager _auras;
    private readonly AutoAttackProcessor? _autoAttacks;
    private readonly Action<SimulationContext,SimulationActorState,IReadOnlyDictionary<string,decimal>>?
        _onStatDelta;
    private readonly bool _enabled;
    private readonly Dictionary<string,decimal> _active=new(StringComparer.OrdinalIgnoreCase);
    private Guid? _instance;
    private decimal _started,_uptime;
    public int Activations { get; private set; }

    public ForeverRacialActiveProcessor(string actorKey,string? race,bool enabled,
        AuraManager auras,
        Action<SimulationContext,SimulationActorState,IReadOnlyDictionary<string,decimal>>?
            onStatDelta=null,AutoAttackProcessor? autoAttacks=null)
    {
        _race=race?.Trim().ToLowerInvariant()??"";
        if(enabled && _race is not ("orc" or "troll" or "night-elf"))
            throw new ArgumentException("Only sourced Orc Blood Fury, Troll Berserking and Night Elf Elune's Light actives are currently executable.");
        (_effectKey,_displayName,_duration)=_race switch {
            "orc"=>(BloodFuryEffect,"Blood Fury",15m),
            "troll"=>(BerserkingEffect,"Berserking",10m),
            "night-elf"=>(ElunesLightEffect,"Elune's Light",15m),
            _=>("", "",0m)
        };
        _actorKey=actorKey;_enabled=enabled;_auras=auras;_onStatDelta=onStatDelta;
        _autoAttacks=autoAttacks;
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
           ev.SourceActorKey==_actorKey && ev.EffectKey==_effectKey)
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
                var oldHaste=actor.Stats.Get("haste-percent");
                var reversed=_active.ToDictionary(x=>x.Key,x=>-x.Value,
                    StringComparer.OrdinalIgnoreCase);
                foreach(var (key,value) in reversed)actor.Stats.Add(key,value);
                _onStatDelta?.Invoke(context,actor,reversed);
                _autoAttacks?.RescheduleForHasteChange(context,_actorKey,
                    oldHaste,actor.Stats.Get("haste-percent"));
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
        switch(_race)
        {
            case "orc":
                _active["attack-power"]=Math.Max(0m,actor.Stats.Get("attack-power"))*.10m;
                _active["spell-power"]=Math.Max(0m,actor.Stats.Get("spell-power"))*.10m;
                break;
            case "troll":
                // 70707 client implements melee, ranged and casting +10% haste.
                // Its tooltip references a 10s buff; the announced possible
                // launch extension to 15s is not asserted as current DB2 fact.
                _active["haste-percent"]=10m;
                break;
            case "night-elf":
                _active["attack-critical-chance-percent"]=10m;
                _active["spell-critical-chance-percent"]=10m;
                break;
        }
        var aura=_auras.ApplyAura(context,actor,new AuraDefinition {
            Key=_effectKey,Name=_displayName,DurationSeconds=_duration,
            StackingMode=AuraStackingMode.Refresh
        },_actorKey,_effectKey,_effectKey);
        _instance=aura.InstanceId;_started=context.CurrentTimeSeconds;
        var oldHaste=actor.Stats.Get("haste-percent");
        foreach(var (key,value) in _active)actor.Stats.Add(key,value);
        _onStatDelta?.Invoke(context,actor,_active);
        _autoAttacks?.RescheduleForHasteChange(context,_actorKey,
            oldHaste,actor.Stats.Get("haste-percent"));
        Activations++;
        var next=context.CurrentTimeSeconds+180m;
        if(next<context.Options.DurationSeconds)
            context.ScheduleEvent(new CombatEvent {
                Type=CombatEventType.EquipmentEffectActivation,TimeSeconds=next,
                SourceActorKey=_actorKey,EffectKey=_effectKey,IsInternal=true,
                Description=_displayName+" cooldown ready"
            });
    }
    public void WriteMetrics(SimulationRunResult run)
    {
        if(!_enabled)return;
        run.CustomMetrics[_effectKey+"-activations"]=Activations;
        run.CustomMetrics[_effectKey+"-uptime-seconds"]=_uptime;
    }
}
