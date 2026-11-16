using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Api.Simulation;

/// <summary>Per-run November Rogue state, layered on the existing Rogue engine.
/// Source-backed client numbers are provisional; no preset identity enters these rules.</summary>
public sealed class RogueClassWideMechanics
{
    private readonly RogueSimRequest _request;
    private readonly SimulationActorState _actor;
    private readonly AuraManager _auras;
    private readonly AbilityExecutor _executor;
    private readonly Func<string,int> _points;
    private readonly Action<string,int> _addPoints;
    private readonly Action<string,int> _setPoints;
    private readonly Dictionary<string,decimal> _baseCosts;
    private readonly HashSet<Guid> _shanks=[];
    private readonly Dictionary<string,Mark> _marks=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,(decimal Expires,int Initial)> _premeditation=new(StringComparer.OrdinalIgnoreCase);
    private bool _inCombat;
    private bool _poised;
    private sealed class Mark(Guid aura,decimal expires,decimal rate,decimal cap,decimal multiplier)
    {
        public Guid Aura { get; }=aura;
        public decimal Expires { get; }=expires;
        public decimal Rate { get; }=rate;
        public decimal Cap { get; }=cap;
        public decimal Multiplier { get; }=multiplier;
        public decimal Stored { get; set; }
    }
    public decimal ShankEnergy { get; private set; }
    public int BootKnifeProcs { get; private set; }
    public int SetupProcs { get; private set; }
    public decimal RelentlessEnergy { get; private set; }
    public decimal DeathMarkDamage { get; private set; }
    public decimal DeathMarkStored { get; private set; }
    public int PoisedUses { get; private set; }
    public int RemorselessActivations { get; private set; }
    public int SprintCleanses { get; private set; }
    public int InterruptedCasts { get; private set; }

    public RogueClassWideMechanics(RogueSimRequest request,SimulationActorState actor,
        AuraManager auras,AbilityExecutor executor,Func<string,int> points,
        Action<string,int> addPoints,Action<string,int> setPoints)
    {
        _request=request;_actor=actor;_auras=auras;_executor=executor;
        _points=points;_addPoints=addPoints;_setPoints=setPoints;
        _baseCosts=actor.Abilities.Values.ToDictionary(x=>x.Definition.Key,
            x=>x.Definition.ResourceCosts.Where(c=>c.ResourceKey=="energy").Sum(c=>c.Amount),
            StringComparer.OrdinalIgnoreCase);
    }
    private int Rank(string key)=>_request.Talents.Rank(key);
    private bool Has(SimulationContext context,string key)=>_actor.ActiveAuras.Any(a=>
        a.Definition.Key==key && a.SourceActorKey==_actor.Key && a.IsActiveAt(context.CurrentTimeSeconds));
    private void Apply(SimulationContext context,string key,decimal duration,string ability)=>
        _auras.ApplyAura(context,_actor,new AuraDefinition { Key=key,Name=key,DurationSeconds=duration },
            _actor.Key,ability,key);

    public void Synchronize(SimulationContext context)
    {
        _actor.RefreshResources(context.CurrentTimeSeconds);
        if(!_inCombat && !_poised && _actor.Resources["energy"].Current>=_actor.Resources["energy"].Maximum)
        {
            _poised=true;
            Apply(context,"rogue-poised",3600m,"poised");
        }
        foreach(var (key,cost) in _baseCosts)
        {
            if(!_actor.Abilities.TryGetValue(key,out var state))continue;
            var energy=state.Definition.ResourceCosts.FirstOrDefault(c=>c.ResourceKey=="energy");
            if(energy is not null)energy.Amount=Math.Max(0m,cost-(_poised?10m+5m*Rank("composure"):0m));
        }
        foreach(var (target,p) in _premeditation.ToArray())
            if(context.CurrentTimeSeconds>=p.Expires)
            {
                // The pending points survive only until another point is added or a finisher uses them.
                _setPoints(target,Math.Max(0,_points(target)-p.Initial));
                _premeditation.Remove(target);
            }
    }
    public void OnPointsAdded(string target)=>_premeditation.Remove(target);
    public void RegisterPremeditation(SimulationContext context,string target,int added)
    {
        _premeditation[target]=(context.CurrentTimeSeconds+20m,added);
        context.ScheduleEvent(new CombatEvent { Type=CombatEventType.RotationDecision,
            TimeSeconds=context.CurrentTimeSeconds+20m,SourceActorKey=_actor.Key,TargetActorKey=target,
            EffectKey="rogue-premeditation-expiry",IsInternal=true });
    }
    public void OnFinisher(SimulationContext context,string target,int spent)
    {
        _premeditation.Remove(target);
        if(Rank("relentless-strikes")>0 && (decimal)context.Random.NextDouble()*100m<4m*Rank("relentless-strikes")*spent)
            RelentlessEnergy+=GainEnergy(context,25m,"relentless-strikes");
    }
    private decimal GainEnergy(SimulationContext context,decimal amount,string source)
    {
        _actor.RefreshResources(context.CurrentTimeSeconds);
        var actual=_actor.Resources["energy"].Gain(amount);
        context.EmitEvent(new CombatEvent { Type=CombatEventType.ResourceChanged,TimeSeconds=context.CurrentTimeSeconds,
            SourceActorKey=_actor.Key,TargetActorKey=_actor.Key,AbilityKey=source,ResourceKey="energy",
            Amount=actual,ResourceTotalAfter=_actor.Resources["energy"].Current });
        return actual;
    }
    public void TrySetup(SimulationContext context,string target)
    {
        if(Rank("setup")<=0 || context.GetActor(target) is not { IsAlive:true } attacker || attacker.TeamKey==_actor.TeamKey)return;
        if((decimal)context.Random.NextDouble()*100m<15m*Rank("setup"))
        {
            _addPoints(target,1);SetupProcs++;
        }
    }
    public decimal ConsumeRemorseless(SimulationContext context,string ability)
    {
        if(ability is not ("sinister-strike" or "backstab" or "ambush" or "mutilate" or "ghostly-strike") ||
            !Has(context,"rogue-remorseless"))return 0m;
        _auras.RemoveAuras(context,_actor,"rogue-remorseless",_actor.Key,"next eligible attack");
        return 20m*Rank("remorseless-attacks");
    }
    public void Process(SimulationContext context,CombatEvent ev)
    {
        Synchronize(context);
        if(ev.Type==CombatEventType.Damage && ev.TargetActorKey==_actor.Key)
        {
            _inCombat=true;
            if(ev.ResolutionType==CombatResolutionTypes.Spell && ev.ResultKey==CombatResultTypes.Resist && ev.Amount==0m &&
                ev.RawAmount>0m && ev.SourceActorKey is {} source)TrySetup(context,source);
        }
        // Control/silence cancels a real in-progress enemy cast; no invented damage or successful interrupt.
        if(ev.Type==CombatEventType.AbilityCastStarted && ev.SourceActorKey is {} casterKey && casterKey!=_actor.Key &&
            context.GetActor(casterKey) is {} caster && caster.TeamKey!=_actor.TeamKey &&
            caster.ActiveAuras.Any(a=>a.SourceActorKey==_actor.Key && a.IsActiveAt(context.CurrentTimeSeconds) &&
                (a.Definition.Key is ("rogue-gouge-control" or "rogue-blind-control" or "rogue-sap-control" or "kidney-shot-stun" or "cheap-shot-stun") ||
                 a.Definition.Key=="rogue-kick-silence" && ev.AbilityKey is {} casting &&
                 caster.Abilities.TryGetValue(casting,out var cast) && cast.Definition.Effects.Any(e=>e.ResolutionType==CombatResolutionTypes.Spell))))
        {
            if(_executor.TryCancelCurrentCast(context,casterKey))InterruptedCasts++;
        }
        if(ev.Type==CombatEventType.Damage && ev.Amount>0m && ev.AbilityKey!="gouge" && ev.TargetActorKey is {} damaged && context.GetActor(damaged) is {} victim)
        {
            foreach(var key in new[]{"rogue-gouge-control","rogue-blind-control","rogue-sap-control"})
                _auras.RemoveAuras(context,victim,key,_actor.Key,"control broken by damage");
        }
        if(ev.SourceActorKey!=_actor.Key)return;
        if(ev.Type==CombatEventType.AuraApplied && ev.AbilityKey=="sap")
        {
            var maximum=1+(Rank("quick-getaway")>0?1:0);
            var active=context.Actors.Values.SelectMany(target=>target.ActiveAuras
                .Where(a=>a.SourceActorKey==_actor.Key && a.Definition.Key=="rogue-sap-control" && a.IsActiveAt(context.CurrentTimeSeconds))
                .Select(a=>(Target:target,Aura:a))).OrderBy(x=>x.Aura.AppliedAtSeconds).ToArray();
            foreach(var stale in active.Take(Math.Max(0,active.Length-maximum)))
                _auras.RemoveAuras(context,stale.Target,"rogue-sap-control",_actor.Key,"Sap target limit");
        }
        if(ev.Type==CombatEventType.AuraApplied && ev.TargetActorKey is {} controlled && controlled!=_actor.Key &&
            ev.AbilityKey is ("gouge" or "blind" or "sap" or "kidney-shot" or "cheap-shot"))
            if(_executor.TryCancelCurrentCast(context,controlled))InterruptedCasts++;
        if(ev.Type==CombatEventType.ActorDied && Rank("remorseless-attacks")>0 &&
            ev.TargetActorKey is {} killed && context.GetActor(killed) is {} enemy &&
            enemy.TeamKey!=_actor.TeamKey && enemy.Level>=_actor.Level-5)
        {
            // Explicit encounter convention: within five levels counts as non-trivial.
            Apply(context,"rogue-remorseless",20m,"remorseless-attacks");RemorselessActivations++;
        }
        if(ev.Type==CombatEventType.RotationDecision && ev.EffectKey=="rogue-death-mark-release" && ev.TargetActorKey is {} releaseTarget)
            ReleaseMark(context,releaseTarget,ev.AuraInstanceId);
        if(ev.Type==CombatEventType.AuraRemoved && ev.AbilityKey=="death-mark" && ev.TargetActorKey is {} removed &&
            _marks.TryGetValue(removed,out var current) && current.Aura==ev.AuraInstanceId && context.CurrentTimeSeconds<current.Expires)
            _marks.Remove(removed); // Dispel/removal cancels storage; natural expiration releases it.
        if(ev.Type==CombatEventType.AuraApplied && ev.AbilityKey=="death-mark" && ev.TargetActorKey is {} markTarget &&
            ev.AuraInstanceId is Guid markId)
        {
            var cp=_points(markTarget);
            var bonus=1m+ForeverRogueClassTalents.CoupDeGraceFraction(Rank("coup-de-grace"));
            var crit=Math.Clamp(_request.Tuning.PhysicalCritPercent+Rank("malice")+_actor.Stats.Get("attack-critical-chance-percent"),0m,100m);
            var cap=(156m+245m*cp+.04m*Math.Max(0m,_actor.Stats.Get("attack-power"))*cp)*(1m+crit/100m);
            _marks[markTarget]=new(markId,context.CurrentTimeSeconds+6m,.12m*cp*bonus,cap,bonus);
            context.ScheduleEvent(new CombatEvent { Type=CombatEventType.RotationDecision,
                TimeSeconds=context.CurrentTimeSeconds+6m,SourceActorKey=_actor.Key,TargetActorKey=markTarget,
                AuraInstanceId=markId,EffectKey="rogue-death-mark-release",IsInternal=true });
        }
        if(ev.Type==CombatEventType.AbilityCastCompleted && ev.AbilityKey is {} ability)
        {
            if(_poised && _baseCosts.GetValueOrDefault(ability)>0m)
            {
                _poised=false;PoisedUses++;
                _auras.RemoveAuras(context,_actor,"rogue-poised",_actor.Key,"energy ability spent");
            }
            if(ability is not ("premeditation" or "vanish" or "sprint" or "evasion" or "preparation" or "cold-blood" or "sap" or "distract"))
                _inCombat=true;
            if(ability is "ambush" or "garrote")
                _auras.RemoveAuras(context,_actor,"rogue-ghostly-opener",_actor.Key,"opener spent");
            if(ability=="sprint" && Rank("improved-sprint")>0 && (decimal)context.Random.NextDouble()*100m<50m*Rank("improved-sprint"))
            {
                foreach(var impaired in _actor.ActiveAuras.Where(a=>a.IsActiveAt(context.CurrentTimeSeconds) &&
                    a.Definition.Tags.Any(t=>t is "movement-impairing" or "root" or "snare")).ToArray())
                {
                    _auras.RemoveAuras(context,_actor,impaired.Definition.Key,impaired.SourceActorKey,"Improved Sprint");SprintCleanses++;
                }
            }
            if(ability=="kick" && ev.TargetActorKey is {} kickTarget && context.GetActor(kickTarget) is {} kicked)
            {
                if(_executor.TryCancelCurrentCast(context,kickTarget))InterruptedCasts++;
                context.EmitEvent(new CombatEvent { Type=CombatEventType.EncounterBossInterruptRequested,
                    TimeSeconds=context.CurrentTimeSeconds,SourceActorKey=_actor.Key,TargetActorKey=kickTarget,
                    AbilityKey="kick" });
                if(Rank("improved-kick")>0 && (decimal)context.Random.NextDouble()*100m<50m*Rank("improved-kick"))
                    _auras.ApplyAura(context,kicked,new AuraDefinition { Key="rogue-kick-silence",Name="Improved Kick",DurationSeconds=2m },
                        _actor.Key,"kick","improved-kick");
            }
        }
        if(ev.Type==CombatEventType.Damage && !ev.IsPeriodic && ev.ResultKey is not ("miss" or "dodge" or "parry") &&
            ev.AbilityKey=="ghostly-strike")Apply(context,"rogue-ghostly-opener",7m,"ghostly-strike");
        if(ev.Type==CombatEventType.Damage && !ev.IsPeriodic && ev.AbilityKey=="shank" &&
            ev.ResultKey is not ("miss" or "dodge" or "parry") && ev.AbilityExecutionId is Guid shankId && _shanks.Add(shankId))
        {
            ShankEnergy+=GainEnergy(context,25m+3m*Rank("flash-of-steel"),"shank");
            if(Rank("boot-knife")>0 && ev.TargetActorKey is {} shankTarget && (decimal)context.Random.NextDouble()*100m<20m*Rank("boot-knife"))
            {
                _addPoints(shankTarget,2);BootKnifeProcs++;
            }
            if(Rank("venom")>0)Apply(context,"venom",6m,"shank");
        }
        if(ev.Type==CombatEventType.Damage && ev.Amount>0m && ev.AbilityKey!="death-mark-damage" &&
            ev.TargetActorKey is {} storing && _marks.TryGetValue(storing,out var mark) && context.CurrentTimeSeconds<mark.Expires &&
            (ev.SchoolKey=="physical" || ev.AbilityKey is "instant-poison" or "deadly-poison"))
        {
            var stored=ev.Amount.Value*mark.Rate*(ev.AbilityKey is "garrote" or "rupture"?2m:1m);
            mark.Stored+=stored;DeathMarkStored+=stored;
        }
        Synchronize(context);
    }
    private void ReleaseMark(SimulationContext context,string targetKey,Guid? aura)
    {
        if(!_marks.TryGetValue(targetKey,out var mark) || mark.Aura!=aura || context.CurrentTimeSeconds<mark.Expires)return;
        _marks.Remove(targetKey);
        if(context.GetActor(targetKey) is not { IsAlive:true } target)return;
        var damage=Math.Min(mark.Stored,mark.Cap)*mark.Multiplier;
        DeathMarkDamage+=damage;
        _executor.ExecuteBackgroundDirectDamage(context,_actor,target,"death-mark-damage","Death Mark",
            new AbilityEffectDefinition { Key="death-mark-release",EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="physical",ResolutionType=CombatResolutionTypes.AlwaysHits,
                CanMiss=false,CanCrit=false,MinimumValue=damage,MaximumValue=damage });
    }
    public void WriteMetrics(SimulationRunResult run)
    {
        run.CustomMetrics["rogue-shank-energy"]=ShankEnergy;
        run.CustomMetrics["rogue-boot-knife-procs"]=BootKnifeProcs;
        run.CustomMetrics["rogue-setup-procs"]=SetupProcs;
        run.CustomMetrics["rogue-relentless-energy"]=RelentlessEnergy;
        run.CustomMetrics["rogue-death-mark-stored"]=DeathMarkStored;
        run.CustomMetrics["rogue-death-mark-damage"]=DeathMarkDamage;
        run.CustomMetrics["rogue-poised-uses"]=PoisedUses;
        run.CustomMetrics["rogue-remorseless-activations"]=RemorselessActivations;
        run.CustomMetrics["rogue-sprint-cleanses"]=SprintCleanses;
        run.CustomMetrics["rogue-interrupted-casts"]=InterruptedCasts;
    }
}
