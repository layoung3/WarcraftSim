using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// One event-driven CP172 effect executor for every class. Per-run state only.
/// Source stat additions use existing actor Stats, AuraManager and AbilityExecutor.
/// All spell/proc values come from the versioned catalog (including explicit
/// provisional rates) -- never from client-supplied arbitrary stat dictionaries.
/// </summary>
public sealed class ForeverEquipmentEffectProcessor : ICombatEventProcessor
{
    private sealed record Instance(string Id,string Slot,ForeverExecutableEquipmentEffect Effect,
        decimal WeaponSpeedSeconds);
    private sealed record Active(Guid AuraInstanceId, Dictionary<string,decimal> StatBonuses);
    private readonly List<Instance> _effects=[];
    private readonly Dictionary<string,Active> _active=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _nextAvailable=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,int> _applications=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _damageByEffect=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _healingByEffect=new(StringComparer.OrdinalIgnoreCase);
    private readonly string _actorKey;
    private readonly AuraManager _auras;
    private readonly AbilityExecutor _executor;
    private readonly Random _rng;
    private readonly string _targetKey;
    private readonly AutoAttackProcessor? _autoAttacks;

    public IReadOnlyDictionary<string,int> Applications => _applications;

    public ForeverEquipmentEffectProcessor(CharacterEquipmentLoadout loadout,
        string actorKey,string targetKey,AuraManager auras,AbilityExecutor executor,
        int seed,AutoAttackProcessor? autoAttacks=null)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(loadout.Items);
        _actorKey=actorKey;_targetKey=targetKey;_auras=auras;_executor=executor;
        _rng=new Random(seed ^ 0x17EFC02);
        _autoAttacks=autoAttacks;
        foreach(var selection in loadout.Items)
        {
            var slot=selection.SlotKey?.Trim().ToLowerInvariant() ?? "";
            if(selection.ItemId is int itemId)
            {
                foreach(var e in ForeverEquipmentEffectsCatalog.GetCatalog().Effects
                    .Where(x=>x.ItemId==itemId && x.Modeled))
                    Add(e,slot,0m);
            }
            foreach(var spellId in new[]{selection.EnchantSpellId,selection.TemporaryEnchantSpellId})
            {
                if(spellId is not int enchantId)continue;
                var item=selection.ItemId.HasValue ?
                    ForeverEquipmentCatalog.FindItem(selection.ItemId.Value):null;
                var speed=item?.Weapon?.SpeedSeconds??0m;
                foreach(var e in ForeverEquipmentEffectsCatalog.GetCatalog().Effects
                    .Where(x=>x.EnchantSpellId==enchantId && x.Modeled))
                    Add(e,slot,speed);
            }
        }
    }

    private void Add(ForeverExecutableEquipmentEffect effect,string slot,decimal speed)
    {
        var id=effect.Key+"@"+slot;
        if(_effects.Any(x=>x.Id.Equals(id,StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Duplicate effect {id} in equipped loadout.");
        if(effect.Kind.StartsWith("proc",StringComparison.Ordinal) && speed<=0m &&
           effect.ProcsPerMinute>0m)
            throw new ArgumentException($"Proc {id} requires known unmodified weapon speed.");
        _effects.Add(new Instance(id,slot,effect,speed));
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type==CombatEventType.SimulationStarted)
        {
            foreach(var effect in _effects.Where(x=>x.Effect.Kind=="on-use" && x.Effect.AutoUse))
                TryActivate(context,effect);
            return;
        }
        if(ev.Type==CombatEventType.EquipmentEffectActivation &&
           string.Equals(ev.SourceActorKey,_actorKey,StringComparison.OrdinalIgnoreCase))
        {
            var found=_effects.FirstOrDefault(x=>x.Id==ev.EffectKey && x.Effect.Kind=="on-use");
            if(found is not null)TryActivate(context,found);
            return;
        }
        if(ev.Type==CombatEventType.AuraRemoved && ev.TargetActorKey==_actorKey &&
           ev.AuraInstanceId is Guid auraId)
        {
            foreach(var pair in _active.Where(x=>x.Value.AuraInstanceId==auraId).ToArray())
            {
                var actor=context.GetActor(_actorKey);
                if(actor is not null)
                {
                    var oldHaste=actor.Stats.Get("haste-percent");
                    foreach(var (key,value) in pair.Value.StatBonuses)
                        actor.Stats.Set(key,actor.Stats.Get(key)-value);
                    _autoAttacks?.RescheduleForHasteChange(context,_actorKey,
                        oldHaste,actor.Stats.Get("haste-percent"));
                }
                _active.Remove(pair.Key);
            }
            return;
        }
        if(ev.AbilityKey is { } abilityKey &&
           abilityKey.StartsWith("equipment-",StringComparison.OrdinalIgnoreCase) &&
           string.Equals(ev.SourceActorKey,_actorKey,StringComparison.OrdinalIgnoreCase))
        {
            var effectId=abilityKey["equipment-".Length..];
            if(ev.Type==CombatEventType.Damage && ev.Amount is >0m)
                _damageByEffect[effectId]=_damageByEffect.GetValueOrDefault(effectId)+ev.Amount.Value;
            else if(ev.Type==CombatEventType.Healing && ev.Amount is >0m)
                _healingByEffect[effectId]=_healingByEffect.GetValueOrDefault(effectId)+ev.Amount.Value;
            // Never let a proc's own damage or healing generate recursive procs.
            return;
        }
        if(ev.Type!=CombatEventType.Damage && ev.Type!=CombatEventType.Healing)
            return;
        if(!string.Equals(ev.SourceActorKey,_actorKey,StringComparison.OrdinalIgnoreCase) ||
           ev.Amount is not >0m || (ev.AbilityKey?.StartsWith("equipment-",StringComparison.OrdinalIgnoreCase) ?? false))
            return;
        foreach(var effect in _effects.Where(x=>x.Effect.Kind.StartsWith("proc",StringComparison.Ordinal)))
        {
            if(!Eligible(effect,ev))continue;
            if(_nextAvailable.GetValueOrDefault(effect.Id)>context.CurrentTimeSeconds)
                continue;
            var chance=effect.Effect.ProcsPerMinute>0m ?
                Math.Min(100m,effect.Effect.ProcsPerMinute*effect.WeaponSpeedSeconds*100m/60m) :
                effect.Effect.ProcChancePercent;
            if((decimal)_rng.NextDouble()*100m >= chance)continue;
            _nextAvailable[effect.Id]=context.CurrentTimeSeconds+
                Math.Max(0m,effect.Effect.CooldownSeconds);
            Trigger(context,effect,ev.TargetActorKey);
        }
    }

    private static bool Eligible(Instance source,CombatEvent ev)
    {
        var e=source.Effect;
        if(ev.IsInternal || ev.IsPeriodic || ev.IsChannelTick)return false;
        if(ev.Type==CombatEventType.Healing)
            return e.Trigger=="healing";
        if(ev.Type!=CombatEventType.Damage)return false;
        if(ev.SchoolKey=="healing")return false;
        return e.Trigger switch
        {
            "melee-hit" => ev.SchoolKey=="physical" &&
                ev.WeaponHandKey!="ranged" && ev.ResolutionType!="spell" &&
                (string.IsNullOrWhiteSpace(ev.WeaponHandKey) ||
                 string.Equals(ev.WeaponHandKey,source.Slot,StringComparison.OrdinalIgnoreCase)),
            "ranged-hit" => ev.WeaponHandKey=="ranged",
            "spell-hit" => ev.ResolutionType=="spell",
            "critical-hit" => ev.IsCritical,
            "any-hit" => true,
            "damage-taken" => false,
            _ => false
        };
    }

    private void Trigger(SimulationContext context,Instance instance,string? targetKey)
    {
        var e=instance.Effect;
        var actor=context.GetActor(_actorKey);
        if(actor is null || !actor.IsAlive)return;
        if(e.Kind=="proc-damage" && e.Damage>0m)
        {
            var target=targetKey is not null ? context.GetActor(targetKey):null;
            if(target is null || !target.IsAlive)return;
            _executor.ExecuteBackgroundDirectDamage(context,actor,target,
                "equipment-"+instance.Id,e.Key,new AbilityEffectDefinition {
                    Key="equipment-"+instance.Id+"-damage",
                    EffectType=AbilityEffectTypes.DirectDamage,
                    TargetType=AbilityTargetTypes.Enemy,
                    ResolutionType="spell",SchoolKey=e.School,
                    CanMiss=false,CanCrit=false,
                    MinimumValue=e.Damage,MaximumValue=e.Damage
                });
        }
        if(e.StatBonuses.Count>0 && e.DurationSeconds>0m)
            ApplyBuff(context,instance,actor);
        if(e.HealingMaximum>0m)
        {
            var delta=e.HealingMinimum+
                (e.HealingMaximum-e.HealingMinimum)*(decimal)_rng.NextDouble();
            var actual=actor.Heal(delta);
            if(actual>0m)
                context.EmitEvent(new CombatEvent {
                    TimeSeconds=context.CurrentTimeSeconds,Type=CombatEventType.Healing,
                    SourceActorKey=_actorKey,TargetActorKey=_actorKey,
                    AbilityKey="equipment-"+instance.Id,EffectKey=instance.Id,
                    Amount=actual,Description=$"{e.Key} healed {actual:0.##}."
                });
        }
        _applications[instance.Id]=_applications.GetValueOrDefault(instance.Id)+1;
    }

    private void ApplyBuff(SimulationContext context,Instance instance,SimulationActorState actor)
    {
        var e=instance.Effect;
        var aura=_auras.ApplyAura(context,actor,new AuraDefinition {
            Key="equipment-"+instance.Id,Name=e.Key,
            DurationSeconds=e.DurationSeconds,
            StackingMode=AuraStackingMode.Refresh
        },_actorKey,"equipment-"+instance.Id,instance.Id);
        if(!_active.ContainsKey(instance.Id))
        {
            var oldHaste=actor.Stats.Get("haste-percent");
            foreach(var (key,value) in e.StatBonuses)
                actor.Stats.Set(key,actor.Stats.Get(key)+value);
            _autoAttacks?.RescheduleForHasteChange(context,_actorKey,
                oldHaste,actor.Stats.Get("haste-percent"));
        }
        _active[instance.Id]=new Active(aura.InstanceId,
            new Dictionary<string,decimal>(e.StatBonuses,StringComparer.OrdinalIgnoreCase));
    }

    private void TryActivate(SimulationContext context,Instance instance)
    {
        var e=instance.Effect;var actor=context.GetActor(_actorKey);
        if(actor is null || !actor.IsAlive)return;
        if(e.CooldownSeconds<=0m)throw new InvalidOperationException($"On-use {e.Key} has no cooldown.");
        var next=Math.Max(_nextAvailable.GetValueOrDefault(instance.Id),
            actor.GetSharedCooldownReadyAt(e.SharedCooldownGroup,context.CurrentTimeSeconds));
        if(next>context.CurrentTimeSeconds)
        {
            Schedule(context,instance,next);
            return;
        }
        Trigger(context,instance,_targetKey);
        if(!string.IsNullOrWhiteSpace(e.SharedCooldownGroup))
            actor.StartSharedCooldown(e.SharedCooldownGroup,context.CurrentTimeSeconds,e.SharedCooldownSeconds);
        _nextAvailable[instance.Id]=context.CurrentTimeSeconds+e.CooldownSeconds;
        Schedule(context,instance,_nextAvailable[instance.Id]);
    }

    private static void Schedule(SimulationContext context,Instance instance,decimal time)
    {
        if(time>=context.Options.DurationSeconds)return;
        context.ScheduleEvent(new CombatEvent {
            Type=CombatEventType.EquipmentEffectActivation,TimeSeconds=time,
            SourceActorKey=context.Options.PrimaryActorKey,
            EffectKey=instance.Id,IsInternal=true,
            Description="Equipment on-use cooldown ready"
        });
    }

    public void WriteMetrics(SimulationRunResult run)
    {
        foreach(var (key,count) in _applications)
            run.CustomMetrics["equipment-"+key+"-activations"]=count;
        foreach(var (key,value) in _damageByEffect)
            run.CustomMetrics["equipment-"+key+"-damage"]=value;
        foreach(var (key,value) in _healingByEffect)
            run.CustomMetrics["equipment-"+key+"-healing"]=value;
    }
}
