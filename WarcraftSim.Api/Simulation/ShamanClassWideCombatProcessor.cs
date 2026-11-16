using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

/// <summary>One run-local owner of Elemental Focus and Maelstrom pricing. Cast
/// snapshots keep a proc gained while casting from changing that cast's price.</summary>
public sealed class ShamanClassWideCombatProcessor : ICombatEventProcessor, IAbilityResourceCostPolicy
{
    private readonly SimulationActorState _actor;
    private readonly AuraManager _auras;
    private readonly AbilityExecutor _executor;
    private readonly ForeverEnhancementShamanTalents _talents;
    private readonly ForeverEnhancementShamanReactiveProcessor _reactive;
    private readonly Dictionary<string,decimal> _baseCosts;
    private readonly Dictionary<Guid,(string Key,decimal Cost,bool Clearcast)> _casts=[];
    private readonly HashSet<Guid> _overloadExecutions=[];
    private readonly decimal _chainCooldown;
    public int ClearcastingProcs {get;private set;}
    public int ClearcastingConsumed {get;private set;}
    public int LightningOverloadProcs {get;private set;}
    public int PushbackResisted {get;private set;}
    public decimal PushbackSeconds {get;private set;}
    public int StormstrikeChargesConsumed {get;private set;}

    public ShamanClassWideCombatProcessor(SimulationActorState actor,AuraManager auras,
        AbilityExecutor executor,ForeverEnhancementShamanTalents talents,
        ForeverEnhancementShamanReactiveProcessor reactive)
    {
        _actor=actor;_auras=auras;_executor=executor;_talents=talents;_reactive=reactive;
        _reactive.ExternalLightningCostManagement=true;
        _baseCosts=actor.Abilities.Values.Where(x=>x.Definition.ResourceCosts.Any(c=>c.ResourceKey=="mana"))
            .ToDictionary(x=>x.Definition.Key,x=>x.Definition.ResourceCosts.First(c=>c.ResourceKey=="mana").Amount,
                StringComparer.OrdinalIgnoreCase);
        _chainCooldown=actor.Abilities["chain-lightning"].Definition.CooldownSeconds;
    }

    public static bool IsLightning(string? key) => key is "lightning-bolt" or "chain-lightning" or "maelstrom-lightning-bolt";
    private static bool IsDamageCast(string? key) => IsLightning(key) || key is "earth-shock" or "flame-shock" or "frost-shock" or "fire-nova";
    private bool Clearcast(SimulationContext context) => _actor.ActiveAuras.Any(x=>
        x.Definition.Key=="shaman-clearcasting" && x.IsActiveAt(context.CurrentTimeSeconds));

    public void Synchronize(SimulationContext context,SimulationActorState actor,AbilityDefinition ability)
    {
        if(actor.Key!=_actor.Key || !_baseCosts.TryGetValue(ability.Key,out var original))return;
        var pending=_casts.Values.FirstOrDefault(x=>x.Key==ability.Key);
        var cost=pending.Key is not null ? pending.Cost :
            Clearcast(context) && IsDamageCast(ability.Key) ? 0m :
            ability.Key=="lightning-bolt" ? original*(1m-.2m*Math.Clamp(_reactive.CurrentMaelstromStacks,0,5)) : original;
        ability.ResourceCosts.First(x=>x.ResourceKey=="mana").Amount=Math.Max(0m,cost);
    }
    public decimal? GetNextAffordableTime(SimulationContext context,SimulationActorState actor,AbilityDefinition ability)
    {
        Synchronize(context,actor,ability);
        return ResourceAvailabilityCalculator.GetNextAffordableTime(actor,ability,context.CurrentTimeSeconds);
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(!_actor.IsAlive)return;
        // Latch before a same-time weapon hit can gain another Maelstrom stack.
        var executionId=_actor.CurrentCastExecutionId ?? context.GetLatestAbilityExecutionId(_actor.Key);
        if(executionId is Guid id && !_casts.ContainsKey(id) &&
            context.GetAbilityExecution(id) is {IsCancelled:false,CastCompletionProcessed:false} execution &&
            execution.SourceActorKey==_actor.Key && _actor.Abilities.TryGetValue(execution.AbilityKey,out var state))
        {
            var cost=state.Definition.ResourceCosts.FirstOrDefault(x=>x.ResourceKey=="mana")?.Amount ?? 0m;
            _casts[id]=(execution.AbilityKey,cost,Clearcast(context)&&IsDamageCast(execution.AbilityKey));
        }
        if(ev.Type==CombatEventType.Damage && ev.TargetActorKey==_actor.Key && ev.Amount>0m)
            ApplyPushback(context);
        if(ev.SourceActorKey!=_actor.Key)return;
        if(ev.Type is CombatEventType.AuraApplied or CombatEventType.AuraRemoved &&
            ev.AbilityKey=="lightning-mastery")
        {
            var active=_actor.ActiveAuras.Any(x=>x.Definition.Key=="lightning-mastery" && x.IsActiveAt(context.CurrentTimeSeconds));
            _actor.Abilities["chain-lightning"].Definition.CooldownSeconds=active?0m:_chainCooldown;
            if(active)_actor.Abilities["chain-lightning"].ResetCooldown(context.CurrentTimeSeconds);
        }
        if(ev.Type is CombatEventType.AbilityCastCancelled or CombatEventType.AbilityCastCompleted &&
            ev.AbilityExecutionId is Guid completed && _casts.Remove(completed,out var captured))
        {
            if(ev.Type==CombatEventType.AbilityCastCompleted && captured.Clearcast)
            {
                ClearcastingConsumed++;
                _auras.RemoveAuras(context,_actor,"shaman-clearcasting",_actor.Key,"consumed by a damage spell");
            }
            if(_actor.Abilities.TryGetValue(captured.Key,out var spell))Synchronize(context,_actor,spell.Definition);
        }
        // Pinned primary OnCastComplete trigger: one roll per player damage
        // cast, including misses. Multiple Chain Lightning targets do not
        // create additional rolls. Consume the old charge before this roll.
        if(ev.Type==CombatEventType.AbilityCastCompleted&&IsDamageCast(ev.AbilityKey)&&
            _talents.Rank("elemental-focus")>0&&ev.AbilityExecutionId is Guid focusId&&
            context.GetAbilityExecution(focusId) is {CastCompletionProcessed:true,IsCancelled:false}&&
            (decimal)context.Random.NextDouble()*100m<10m)
        {
            ClearcastingProcs++;
            _auras.ApplyAura(context,_actor,new AuraDefinition
            {Key="shaman-clearcasting",Name="Elemental Focus",DurationSeconds=15m},
                _actor.Key,"elemental-focus","clearcasting");
        }
        if(ev.Type!=CombatEventType.Damage || ev.Amount is not >0m || ev.IsPeriodic || ev.IsChannelTick)return;
        if(ev.AbilityKey is "lightning-bolt" or "chain-lightning" or "maelstrom-lightning-bolt" or "earth-shock")
        {
            var target=context.GetActor(ev.TargetActorKey ?? "");
            if(target is not null && target.ActiveAuras.Any(x=>x.Definition.Key=="shaman-stormstrike-nature" &&
                x.SourceActorKey==_actor.Key && x.IsActiveAt(context.CurrentTimeSeconds)))
            {
                _auras.RemoveAuras(context,target,"shaman-stormstrike-nature",_actor.Key,"next eligible Nature spell consumed Stormstrike");
                StormstrikeChargesConsumed++;
            }
        }
        if(_talents.Rank("lightning-overload")==0 || !IsLightning(ev.AbilityKey) ||
            ev.AbilityExecutionId is not Guid overloadId || !_overloadExecutions.Add(overloadId) ||
            (decimal)context.Random.NextDouble()*100m>=10m*_talents.Rank("lightning-overload"))return;
        var procKey=ev.AbilityKey=="chain-lightning"?"lightning-overload-chain":"lightning-overload-bolt";
        var proc=_actor.Abilities[procKey].Definition;
        var primary=context.GetActor(ev.TargetActorKey ?? "");
        if(primary is null)return;
        var targets=procKey=="lightning-overload-chain"
            ? context.Actors.Values.Where(x=>x.IsAlive&&x.TeamKey!=_actor.TeamKey)
                .OrderBy(x=>x.Key==primary.Key?0:1).ThenBy(x=>x.Key,StringComparer.Ordinal).Take(3)
            : new[]{primary}.AsEnumerable();
        foreach(var target in targets)
            _executor.ExecuteBackgroundDirectDamage(context,_actor,target,procKey,proc.Name,proc.Effects.Single());
        LightningOverloadProcs++;
    }

    private void ApplyPushback(SimulationContext context)
    {
        if(_actor.CurrentCastExecutionId is not Guid id || !_actor.IsCasting(context.CurrentTimeSeconds) ||
            context.GetAbilityExecution(id) is not {IsCancelled:false,CastCompletionProcessed:false} cast ||
            cast.CastCompletionDueAtSeconds<=context.CurrentTimeSeconds)return;
        var rank=cast.AbilityKey is "lightning-bolt" or "chain-lightning" ? _talents.Rank("eye-of-the-storm") :
            cast.AbilityKey is "healing-wave" or "lesser-healing-wave" or "chain-heal" ? _talents.Rank("healing-focus") : 0;
        if((decimal)context.Random.NextDouble()*100m<ForeverShamanClassTalents.PushbackResistance(rank))
            PushbackResisted++;
        else PushbackSeconds+=_executor.TryPushbackCurrentCast(context,_actor.Key,.5m,1m);
    }
}

public sealed class ShamanClassWideTimingProvider(Func<int> stacks) : IAbilityTimingProvider
{
    public AbilityTimingSnapshot Resolve(SimulationContext context,SimulationActorState source,AbilityDefinition ability)
    {
        var timing=BaseAbilityTimingProvider.Instance.Resolve(context,source,ability);
        bool active(string key)=>source.ActiveAuras.Any(x=>x.Definition.Key==key&&x.IsActiveAt(context.CurrentTimeSeconds));
        var cast=timing.CastTimeSeconds;
        if(cast>0m && cast<10m && ability.Effects.Any(x=>x.SchoolKey=="nature") && active("nature-swiftness-ready"))cast=0m;
        else
        {
            if(ability.Key=="lightning-bolt")
            {
                if(active("lightning-mastery"))cast=Math.Max(0m,cast-.5m);
                cast*=1m-.2m*Math.Clamp(stacks(),0,5);
            }
            // Farseer and Flurry are weapon-only haste; equipment spell haste
            // remains a separate live component of the existing haste stat.
            var spellHasteMultiplier=(1m+source.Stats.Get(ForeverCombatStatKeys.HastePercent)/100m)/
                (1m+source.Stats.Get("shaman-farseer-haste-percent")/100m)/
                (1m+source.Stats.Get("shaman-flurry-haste-percent")/100m);
            cast/=Math.Max(1m,spellHasteMultiplier);
        }
        return new AbilityTimingSnapshot
        {
            CastTimeSeconds=cast,GlobalCooldownSeconds=timing.GlobalCooldownSeconds,
            ChannelDurationSeconds=timing.ChannelDurationSeconds,ChannelTickIntervalSeconds=timing.ChannelTickIntervalSeconds
        };
    }
}
