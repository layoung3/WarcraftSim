using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

/// <summary>Explicit focused encounter controls. Speeds are scenario inputs,
/// not inferred class statistics. Default bosses remain control immune.</summary>
public sealed class ShamanClassWideUtilityScenario
{
    public bool IsIndoors {get;set;}
    public bool TargetCanBeControlled {get;set;}
    public bool PlayerMovesTowardTarget {get;set;}
    public bool TargetMovesTowardPlayer {get;set;}
    public decimal PlayerSpeedYardsPerSecond {get;set;}=7m;
    public decimal TargetSpeedYardsPerSecond {get;set;}=7m;
    public decimal StopDistanceYards {get;set;}=3m;
    public string IncomingAttackKind {get;set;}="melee";
    public bool IncomingSpellsAreArea {get;set;}
    public bool UseReincarnation {get;set;}
    public void Validate()
    {
        if(PlayerSpeedYardsPerSecond is <0m or >100m || TargetSpeedYardsPerSecond is <0m or >100m ||
            StopDistanceYards is <0m or >100m || IncomingAttackKind is not ("melee" or "ranged" or "spell"))
            throw new ArgumentException("Invalid bounded Shaman utility scenario.");
    }
}

/// <summary>Real one-dimensional movement, control, totem-health, redirection
/// and resurrection scenarios. No raid AI or full tank/healer UI is implied.</summary>
public sealed class ShamanClassWideUtilityProcessor : ICombatEventProcessor
{
    private readonly EnhancementShamanSimRequest _request;
    private readonly ShamanClassWideUtilityScenario _scenario;
    private readonly SimulationActorState _actor;
    private readonly AuraManager _auras;
    private readonly AbilityExecutor _executor;
    private readonly Action<SimulationContext,bool> _setMeleeActive;
    private decimal _playerPosition;
    private decimal _enemyPosition;
    private decimal _lastMovement;
    private readonly Dictionary<string,decimal> _totemPositions=new(StringComparer.OrdinalIgnoreCase);
    private SimulationActorState? _stoneclaw;
    public int EarthboundRoots {get;private set;}
    public int GroundedSpells {get;private set;}
    public int StoneclawIntercepts {get;private set;}
    public int RelocatedTotems {get;private set;}
    public int Reincarnations {get;private set;}
    public int FlametongueTotemProcs {get;private set;}
    public decimal ReincarnationReadyAtSeconds {get;private set;}
    public decimal PlayerDistanceTravelled {get;private set;}
    public decimal TargetDistanceTravelled {get;private set;}
    public decimal Distance=>Math.Abs(_enemyPosition-_playerPosition);

    public ShamanClassWideUtilityProcessor(EnhancementShamanSimRequest request,SimulationActorState actor,
        AuraManager auras,AbilityExecutor executor,Action<SimulationContext,bool> setMeleeActive)
    {
        _request=request;_actor=actor;_auras=auras;_executor=executor;_setMeleeActive=setMeleeActive;
        _scenario=request.UtilityScenario??new(){IncomingAttackKind=request.IncomingDamageSchool is not (null or "physical")?"spell":"melee"};_scenario.Validate();
        _enemyPosition=request.TargetDistanceYards??3m;
    }

    public void Initialize(SimulationContext context)
    {
        _stoneclaw=new SimulationActorState{Key="shaman-stoneclaw",Name="Stoneclaw Totem",TeamKey=_actor.TeamKey,
            Level=_actor.Level,MaximumHealth=480m*(1m+.25m*_request.Talents.Rank("earths-grasp"))};
        _stoneclaw.InitializeHealth(_stoneclaw.MaximumHealth,0m);
        _stoneclaw.Stats.Set("shaman-totem-actor",1m);context.AddActor(_stoneclaw);
        UpdatePositionStats(context);
    }

    private bool Active(SimulationContext context,string aura)=>_actor.ActiveAuras.Any(x=>
        x.Definition.Key==aura&&x.IsActiveAt(context.CurrentTimeSeconds));
    public bool InTotemRange(SimulationContext context,string key,decimal radius=30m)=>
        _totemPositions.TryGetValue(key,out var position)&&Math.Abs(_playerPosition-position)<=radius;

    public bool TryRedirectIncoming(SimulationContext context,CombatEvent ev,decimal raw)
    {
        if(_scenario.IncomingAttackKind=="melee"&&_scenario.TargetCanBeControlled&&_stoneclaw?.IsAlive==true&&
            Active(context,"shaman-stoneclaw-totem")&&_totemPositions.TryGetValue("stoneclaw-totem",out var at)&&
            Math.Abs(_enemyPosition-at)<=8m)
        {
            var dealt=_stoneclaw.TakeDamage(raw);StoneclawIntercepts++;
            context.EmitEvent(new CombatEvent{Type=CombatEventType.Damage,TimeSeconds=context.CurrentTimeSeconds,
                SourceActorKey=ev.SourceActorKey,TargetActorKey=_stoneclaw.Key,AbilityKey="encounter:stoneclaw-intercept",
                SchoolKey=ev.SchoolKey,RawAmount=raw,Amount=dealt,ResultKey=CombatResultTypes.Hit});
            if(!_stoneclaw.IsAlive)_auras.RemoveAuras(context,_actor,"shaman-stoneclaw-totem",_actor.Key,"Stoneclaw destroyed");
            return true;
        }
        if(_scenario.IncomingAttackKind=="spell"&&!_scenario.IncomingSpellsAreArea&&
            Active(context,"shaman-grounding-totem")&&InTotemRange(context,"grounding-totem"))
        {
            GroundedSpells++;
            _auras.RemoveAuras(context,_actor,"shaman-grounding-totem",_actor.Key,"Grounding destroyed by redirected damaging spell");
            context.EmitEvent(new CombatEvent{Type=CombatEventType.Damage,TimeSeconds=context.CurrentTimeSeconds,
                SourceActorKey=ev.SourceActorKey,TargetActorKey=_actor.Key,AbilityKey="encounter:grounding-redirect",
                SchoolKey=ev.SchoolKey,RawAmount=raw,Amount=0m,ResultKey="redirected"});
            return true;
        }
        return false;
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type==CombatEventType.SimulationStarted)
        {
            if(_scenario.PlayerMovesTowardTarget||_scenario.TargetMovesTowardPlayer)ScheduleMovement(context,.1m);
            return;
        }
        if(ev.Type==CombatEventType.RotationDecision&&ev.EffectKey=="shaman-movement-step")
        {Move(context);return;}
        if(ev.Type==CombatEventType.ActorDied&&ev.TargetActorKey==_actor.Key)
        {TryReincarnate(context);return;}
        if(ev.SourceActorKey!=_actor.Key)return;
        if(ev.Type==CombatEventType.AuraApplied)
        {
            var key=TotemKey(ev.EffectKey);
            if(key is not null)
            {
                var slot=ForeverShamanTotemCalls.Slots[key];
                foreach(var replaced in _totemPositions.Keys.Where(x=>x!=key&&
                    ForeverShamanTotemCalls.Slots.GetValueOrDefault(x)==slot).ToArray())_totemPositions.Remove(replaced);
                _totemPositions[key]=_playerPosition;
                if(key=="stoneclaw-totem"&&_stoneclaw is not null)_stoneclaw.InitializeHealth(_stoneclaw.MaximumHealth);
                if(key=="earthbind-totem"&&_request.Talents.Rank("earthbound")>0&&_scenario.TargetCanBeControlled)
                    foreach(var target in context.Actors.Values.Where(x=>x.TeamKey!=_actor.TeamKey&&x.IsAlive))
                        if(Distance<=10m*(1m+.1m*_request.Talents.Rank("earths-grasp")))
                        {
                            _auras.ApplyAura(context,target,new AuraDefinition{Key="shaman-earthbound-root",Name="Earthbound",DurationSeconds=5m},
                                _actor.Key,"earthbound","earthbound-root");EarthboundRoots++;
                        }
            }
            if(ev.AbilityKey=="ghost-wolf")_setMeleeActive(context,false);
            UpdatePositionStats(context);
        }
        if(ev.Type==CombatEventType.AuraRemoved&&ev.EffectKey=="stoneclaw-aura"&&_stoneclaw is not null)
            _stoneclaw.InitializeHealth(_stoneclaw.MaximumHealth,0m);
        if(ev.Type==CombatEventType.AuraRemoved&&ev.EffectKey=="ghost-wolf-form")
            _setMeleeActive(context,Distance<=5m&&_actor.IsAlive);
        if(ev.Type==CombatEventType.AbilityCastCompleted&&ev.AbilityKey=="totemic-call")
        {
            foreach(var key in _totemPositions.Keys.ToArray())
                if(Active(context,AuraFor(key)))
                {_totemPositions[key]=_playerPosition;RelocatedTotems++;}
            UpdatePositionStats(context);
        }
        if(ev.Type==CombatEventType.Damage&&ev.AbilityKey=="frost-shock"&&ev.Amount>0m&&_scenario.TargetCanBeControlled&&
            context.GetActor(ev.TargetActorKey??"") is {} shocked)
            _auras.ApplyAura(context,shocked,new AuraDefinition{Key="shaman-frost-shock-slow",Name="Frost Shock",DurationSeconds=8m},
                _actor.Key,"frost-shock","frost-shock-slow");
        if(ev.Type==CombatEventType.Damage&&ev.WeaponHandKey=="main-hand"&&ev.Amount>0m&&
            Active(context,"shaman-flametongue-totem")&&InTotemRange(context,"flametongue-totem")&&
            !Active(context,"shaman-windfury-totem")&&context.GetActor(ev.TargetActorKey??"") is {} victim)
        {
            // Current client rank4 dummy1363 and speed normalization are
            // source-backed provisional. No invented SP coefficient is added.
            var damage=13.63m*_request.WeaponSpeedSeconds*(1m+.05m*_request.Talents.Rank("call-of-flame"));
            _executor.ExecuteBackgroundDirectDamage(context,_actor,victim,"flametongue-totem-proc","Flametongue Totem",new()
            {
                Key="flametongue-totem-hit",EffectType=AbilityEffectTypes.DirectDamage,TargetType=AbilityTargetTypes.Enemy,
                SchoolKey="fire",ResolutionType=CombatResolutionTypes.Spell,CanMiss=false,CanCrit=true,
                MinimumValue=damage,MaximumValue=damage
            });
            FlametongueTotemProcs++;
        }
    }

    private void Move(SimulationContext context)
    {
        var dt=context.CurrentTimeSeconds-_lastMovement;_lastMovement=context.CurrentTimeSeconds;
        var remaining=Math.Max(0m,Distance-_scenario.StopDistanceYards);
        if(_actor.IsAlive&&_scenario.PlayerMovesTowardTarget&&!_actor.IsCasting(context.CurrentTimeSeconds))
        {
            var move=Math.Min(remaining,_scenario.PlayerSpeedYardsPerSecond*(Active(context,"shaman-ghost-wolf")?1.4m:1m)*dt);
            _playerPosition+=Math.Sign(_enemyPosition-_playerPosition)*move;PlayerDistanceTravelled+=move;
        }
        var target=context.GetActor("target");
        if(_actor.IsAlive&&target?.IsAlive==true&&_scenario.TargetMovesTowardPlayer)
        {
            var rooted=target.ActiveAuras.Any(x=>x.Definition.Key=="shaman-earthbound-root"&&x.IsActiveAt(context.CurrentTimeSeconds));
            var earthbind=_scenario.TargetCanBeControlled&&Active(context,"shaman-earthbind-totem")&&
                _totemPositions.TryGetValue("earthbind-totem",out var at)&&
                Math.Abs(_enemyPosition-at)<=10m*(1m+.1m*_request.Talents.Rank("earths-grasp"));
            var frost=target.ActiveAuras.Any(x=>x.Definition.Key=="shaman-frost-shock-slow"&&x.IsActiveAt(context.CurrentTimeSeconds));
            // Forever Earthbind aura3600:50% speed; current Frost Shock8s50%.
            var multiplier=rooted?0m:(earthbind||frost) ? .5m : 1m;
            var move=Math.Min(Math.Max(0m,Distance-_scenario.StopDistanceYards),_scenario.TargetSpeedYardsPerSecond*multiplier*dt);
            _enemyPosition-=Math.Sign(_enemyPosition-_playerPosition)*move;TargetDistanceTravelled+=move;
        }
        UpdatePositionStats(context);
        _setMeleeActive(context,_actor.IsAlive&&Distance<=5m&&!Active(context,"shaman-ghost-wolf"));
        ScheduleMovement(context,context.CurrentTimeSeconds+.1m);
    }
    private void ScheduleMovement(SimulationContext context,decimal time)
    {
        if(time>=context.Options.DurationSeconds)return;
        context.ScheduleEvent(new CombatEvent{TimeSeconds=time,Type=CombatEventType.RotationDecision,
            SourceActorKey=_actor.Key,TargetActorKey="target",EffectKey="shaman-movement-step",IsInternal=true});
    }
    private void UpdatePositionStats(SimulationContext context)
    {
        _actor.Stats.Set("shaman-target-distance",Distance);
        _actor.Stats.Set("shaman-player-position",_playerPosition);
        foreach(var target in context.Actors.Values.Where(x=>x.TeamKey!=_actor.TeamKey))target.Stats.Set("shaman-target-position",_enemyPosition);
        foreach(var (key,at) in _totemPositions)
            _actor.Stats.Set("shaman-position:"+key,at);
        var fire=_totemPositions.Where(x=>ForeverShamanTotemCalls.Slots.GetValueOrDefault(x.Key)=="fire"&&Active(context,AuraFor(x.Key)))
            .Select(x=>Math.Abs(_enemyPosition-x.Value)).DefaultIfEmpty(Distance).Min();
        _actor.Stats.Set("shaman-fire-target-distance",fire);
    }
    private void TryReincarnate(SimulationContext context)
    {
        if(!_scenario.UseReincarnation||context.CurrentTimeSeconds<ReincarnationReadyAtSeconds)return;
        _executor.TryCancelCurrentCast(context,_actor.Key);
        _setMeleeActive(context,false);
        foreach(var target in context.Actors.Values)
            foreach(var aura in target.ActiveAuras.Where(x=>x.SourceActorKey==_actor.Key).ToArray())
                _auras.RemoveAuras(context,target,aura.Definition.Key,_actor.Key,"Death removed owned effects");
        var fraction=.2m+.1m*_request.Talents.Rank("improved-reincarnation");
        _actor.InitializeHealth(_actor.MaximumHealth,_actor.MaximumHealth*fraction);
        var mana=_actor.Resources["mana"];mana.SetCurrent(mana.Maximum*fraction);
        ReincarnationReadyAtSeconds=context.CurrentTimeSeconds+3600m-600m*_request.Talents.Rank("improved-reincarnation");
        Reincarnations++;
        context.EmitEvent(new CombatEvent{Type=CombatEventType.HealthChanged,TimeSeconds=context.CurrentTimeSeconds,
            SourceActorKey=_actor.Key,TargetActorKey=_actor.Key,AbilityKey="reincarnation",Amount=_actor.CurrentHealth,
            Description="Reincarnation restored the sourced rank fraction of maximum Health and Mana."});
        _setMeleeActive(context,Distance<=5m);
        context.ScheduleEvent(new CombatEvent{Type=CombatEventType.RotationDecision,TimeSeconds=context.CurrentTimeSeconds+.000001m,
            SourceActorKey=_actor.Key,TargetActorKey="target"});
    }
    private static string? TotemKey(string? effect)=>effect switch
    {
        "earthbind-aura"=>"earthbind-totem","stoneclaw-aura"=>"stoneclaw-totem","windwall-aura"=>"windwall-totem",
        "grounding-aura"=>"grounding-totem","flametongue-totem-aura"=>"flametongue-totem",
        "searing-totem-tick"=>"searing-totem","magma-totem-tick"=>"magma-totem",
        "strength-of-earth-aura"=>"strength-of-earth-totem","stoneskin-aura"=>"stoneskin-totem",
        "windfury-totem-air"=>"windfury-totem","mana-spring-totem-tick"=>"mana-spring-totem",
        "healing-stream-aura"=>"healing-stream-totem","mana-tide-aura"=>"mana-tide-totem",_=>null
    };
    private static string AuraFor(string key)=>key switch
    {
        "magma-totem" or "searing-totem"=>"shaman-fire-totem-active",
        "mana-spring-totem" or "healing-stream-totem"=>key,
        "mana-tide-totem"=>"mana-tide-active",_=>"shaman-"+key
    };
}
