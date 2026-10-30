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
        decimal WeaponSpeedSeconds,Random Rng);
    private sealed record Active(Guid AuraInstanceId,Dictionary<string,decimal> StatBonuses,
        int Stacks,decimal StartedAt);
    private readonly List<Instance> _effects=[];
    private readonly Dictionary<string,Active> _active=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _nextAvailable=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,int> _applications=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _damageByEffect=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _healingByEffect=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _resourceByEffect=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _uptimeByEffect=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,int> _charges=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _chargeReadyAt=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,decimal> _lastEligibleAt=new(StringComparer.OrdinalIgnoreCase);
    private readonly int _seed;
    private readonly string _actorKey;
    private readonly AuraManager _auras;
    private readonly AbilityExecutor _executor;
    private readonly string _targetKey;
    private readonly AutoAttackProcessor? _autoAttacks;

    public IReadOnlyDictionary<string,int> Applications => _applications;

    public ForeverEquipmentEffectProcessor(CharacterEquipmentLoadout loadout,
        string actorKey,string targetKey,AuraManager auras,AbilityExecutor executor,
        int seed,AutoAttackProcessor? autoAttacks=null,
        IReadOnlyList<ForeverEquipmentEffectBinding>? explicitBindings=null)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(loadout.Items);
        _actorKey=actorKey;_targetKey=targetKey;_auras=auras;_executor=executor;
        _seed=seed;_autoAttacks=autoAttacks;
        // Explicit bindings are for deterministic engine-level tests and reviewed
        // rulesets; API/client requests always use the versioned embedded catalog.
        if(explicitBindings is not null)
        {
            foreach(var binding in explicitBindings)
                Add(binding.Effect,binding.Slot,binding.WeaponSpeedSeconds);
            return;
        }
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
        // The same shared execution path handles active source-backed set
        // effects once their threshold and mechanics have been reviewed.
        foreach(var effect in ForeverEquipmentEffectsCatalog.ActiveSetEffects(
            loadout,ForeverEquipmentEffectsCatalog.GetCatalog().Effects)
            .Where(x=>x.Modeled))
            Add(effect,$"set-{effect.SetKey}-{effect.RequiredPieces}",0m);
    }

    private void Add(ForeverExecutableEquipmentEffect effect,string slot,decimal speed)
    {
        var id=effect.Key+"@"+slot;
        if(_effects.Any(x=>x.Id.Equals(id,StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"Duplicate effect {id} in equipped loadout.");
        if(effect.Kind.StartsWith("proc",StringComparison.Ordinal) && speed<=0m &&
           effect.ProcsPerMinute>0m)
            throw new ArgumentException($"Proc {id} requires known unmodified weapon speed.");
        // FNV-1a gives each effect a stable independent RNG sequence. Adding
        // another equipped effect never perturbs an existing effect's rolls.
        uint hash=2166136261u;
        foreach(var c in id.ToLowerInvariant())
            hash=unchecked((hash^(uint)c)*16777619u);
        _effects.Add(new Instance(id,slot,effect,speed,
            new Random(_seed^unchecked((int)hash))));
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.Type==CombatEventType.SimulationStarted)
        {
            // Deterministic solo-DPS auto-use policy: first usable moment and
            // thereafter on personal/shared cooldown, without ignoring resources.
            foreach(var effect in _effects.Where(x=>x.Effect.Kind=="on-use" && x.Effect.AutoUse))
                TryActivate(context,effect);
            return;
        }
        if(ev.Type==CombatEventType.SimulationEnded)
        {
            foreach(var (id,active) in _active)
                _uptimeByEffect[id]=_uptimeByEffect.GetValueOrDefault(id)+
                    Math.Max(0m,context.CurrentTimeSeconds-active.StartedAt);
            return;
        }
        if(ev.Type==CombatEventType.RotationDecision &&
           string.Equals(ev.SourceActorKey,_actorKey,StringComparison.OrdinalIgnoreCase))
        {
            foreach(var pending in _effects.Where(x=>x.Effect.Kind=="on-use" &&
                x.Effect.AutoUse && !string.IsNullOrEmpty(x.Effect.ResourceKey)))
                TryActivate(context,pending);
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
                        actor.Stats.Set(key,actor.Stats.Get(key)-value*pair.Value.Stacks);
                    _autoAttacks?.RescheduleForHasteChange(context,_actorKey,
                        oldHaste,actor.Stats.Get("haste-percent"));
                }
                _uptimeByEffect[pair.Key]=_uptimeByEffect.GetValueOrDefault(pair.Key)+
                    Math.Max(0m,context.CurrentTimeSeconds-pair.Value.StartedAt);
                _active.Remove(pair.Key);
            }
            return;
        }
        // Equipment-originated events never trigger any equipment procs.
        // Their resource/healing/damage contributions remain reported.
        if(ev.AbilityKey?.StartsWith("equipment-",StringComparison.OrdinalIgnoreCase)==true)
        {
            if(string.Equals(ev.SourceActorKey,_actorKey,StringComparison.OrdinalIgnoreCase))
            {
                var effectId=ev.AbilityKey["equipment-".Length..];
                if(ev.Type==CombatEventType.Damage && ev.Amount is >0m)
                    _damageByEffect[effectId]=_damageByEffect.GetValueOrDefault(effectId)+ev.Amount.Value;
                else if(ev.Type==CombatEventType.Healing && ev.Amount is >0m)
                    _healingByEffect[effectId]=_healingByEffect.GetValueOrDefault(effectId)+ev.Amount.Value;
                else if(ev.Type==CombatEventType.ResourceChanged && ev.Amount is >0m)
                    _resourceByEffect[effectId]=_resourceByEffect.GetValueOrDefault(effectId)+ev.Amount.Value;
            }
            return;
        }
        if(ev.Type is not (CombatEventType.Damage or CombatEventType.Healing or
            CombatEventType.ResourceChanged) || ev.Amount is not >0m || ev.IsInternal)
            return;

        foreach(var effect in _effects.Where(x=>x.Effect.Kind.StartsWith("proc",StringComparison.Ordinal)))
        {
            if(!Eligible(effect,ev))continue;
            var chance=ResolveProcChance(effect,context.CurrentTimeSeconds);
            if(_nextAvailable.GetValueOrDefault(effect.Id)>context.CurrentTimeSeconds ||
               AvailableCharges(effect,context.CurrentTimeSeconds)==0)
                continue;
            if((decimal)effect.Rng.NextDouble()*100m>=chance)continue;
            ConsumeCharge(effect,context.CurrentTimeSeconds);
            _nextAvailable[effect.Id]=context.CurrentTimeSeconds+
                (effect.Effect.InternalCooldownSeconds>0m ?
                    effect.Effect.InternalCooldownSeconds : effect.Effect.CooldownSeconds);
            var target=effect.Effect.Trigger switch {
                "damage-taken" => ev.SourceActorKey,
                "healing" or "periodic-healing" or "resource-gain" => _targetKey,
                _ => ev.TargetActorKey
            };
            Trigger(context,effect,target);
        }
    }

    private decimal ResolveProcChance(Instance instance,decimal now)
    {
        var e=instance.Effect;
        var mode=string.IsNullOrWhiteSpace(e.ProcRateMode) ?
            (e.ProcsPerMinute>0m?"ppm":"chance") : e.ProcRateMode;
        if(mode=="rppm")
        {
            var previous=_lastEligibleAt.GetValueOrDefault(instance.Id);
            _lastEligibleAt[instance.Id]=now;
            return Math.Min(100m,Math.Max(0m,now-previous)*e.RealProcsPerMinute*100m/60m);
        }
        return mode=="ppm" ?
            Math.Min(100m,e.ProcsPerMinute*instance.WeaponSpeedSeconds*100m/60m) :
            e.ProcChancePercent;
    }

    private bool Eligible(Instance source,CombatEvent ev)
    {
        var e=source.Effect;
        var ownerSource=string.Equals(ev.SourceActorKey,_actorKey,StringComparison.OrdinalIgnoreCase);
        var ownerTarget=string.Equals(ev.TargetActorKey,_actorKey,StringComparison.OrdinalIgnoreCase);
        if(e.Trigger=="damage-taken")
            return ev.Type==CombatEventType.Damage && ownerTarget && !ownerSource;
        if(e.Trigger=="resource-gain")
            return ev.Type==CombatEventType.ResourceChanged && ownerSource && ownerTarget;
        if(!ownerSource)return false; // Pets and other raid actors never trigger owner gear.
        if(ev.Type==CombatEventType.Healing)
            return e.Trigger=="healing" || e.Trigger=="periodic-healing" &&
                (ev.IsPeriodic || ev.IsChannelTick);
        if(ev.Type!=CombatEventType.Damage || ev.SchoolKey=="healing")return false;
        var periodic=ev.IsPeriodic || ev.IsChannelTick;
        var melee= !periodic && ev.SchoolKey=="physical" &&
            ev.WeaponHandKey!="ranged" && ev.ResolutionType!="spell" &&
            // An enchant belongs to its exact weapon hand; item/set procs
            // must not accidentally be restricted to "trinket1"/"set-...".
            (!e.EnchantSpellId.HasValue ||
             string.IsNullOrWhiteSpace(ev.WeaponHandKey) ||
             string.Equals(ev.WeaponHandKey,source.Slot,StringComparison.OrdinalIgnoreCase));
        var ranged=!periodic && ev.WeaponHandKey=="ranged";
        var spell=!periodic && ev.ResolutionType=="spell";
        return e.Trigger switch
        {
            "melee-hit" => melee,
            "melee-crit" => melee && ev.IsCritical,
            "ranged-hit" => ranged,
            "ranged-crit" => ranged && ev.IsCritical,
            "spell-hit" => spell,
            "spell-crit" => spell && ev.IsCritical,
            "periodic-damage" => periodic,
            "periodic-crit" => periodic && ev.IsCritical,
            "critical-hit" => !periodic && ev.IsCritical,
            "any-hit" => !periodic,
            _ => false
        };
    }

    private int AvailableCharges(Instance instance,decimal time)
    {
        var e=instance.Effect;
        if(e.MaxCharges<=0)return int.MaxValue;
        var count=_charges.GetValueOrDefault(instance.Id,e.MaxCharges);
        if(count<e.MaxCharges && e.ChargeRecoverySeconds>0m &&
           _chargeReadyAt.TryGetValue(instance.Id,out var ready) && time>=ready)
        {
            var recovered=(int)Math.Min(e.MaxCharges-count,
                1m+Math.Floor((time-ready)/e.ChargeRecoverySeconds));
            count+=recovered;
            _charges[instance.Id]=count;
            if(count==e.MaxCharges)_chargeReadyAt.Remove(instance.Id);
            else _chargeReadyAt[instance.Id]=ready+recovered*e.ChargeRecoverySeconds;
        }
        return count;
    }

    private void ConsumeCharge(Instance instance,decimal time)
    {
        var e=instance.Effect;
        if(e.MaxCharges<=0)return;
        var count=AvailableCharges(instance,time);
        if(count<=0)throw new InvalidOperationException($"No charges for {instance.Id}.");
        _charges[instance.Id]=count-1;
        if(e.ChargeRecoverySeconds>0m && !_chargeReadyAt.ContainsKey(instance.Id))
            _chargeReadyAt[instance.Id]=time+e.ChargeRecoverySeconds;
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
        if(e.ResourceMaximum>0m)
        {
            var resource=actor.Resources.GetValueOrDefault(e.ResourceKey);
            if(resource is null)
                throw new InvalidOperationException($"Effect {e.Key} requires resource '{e.ResourceKey}'.");
            var gain=e.ResourceMinimum+
                (e.ResourceMaximum-e.ResourceMinimum)*(decimal)instance.Rng.NextDouble();
            var actual=resource.Gain(gain);
            if(actual>0m)
                context.EmitEvent(new CombatEvent {
                    TimeSeconds=context.CurrentTimeSeconds,Type=CombatEventType.ResourceChanged,
                    SourceActorKey=_actorKey,TargetActorKey=_actorKey,
                    AbilityKey="equipment-"+instance.Id,EffectKey=instance.Id,
                    ResourceKey=e.ResourceKey,Amount=actual,
                    ResourceTotalAfter=resource.Current,
                    Description=$"{e.Key} restored {actual:0.##} {e.ResourceKey}."
                });
        }
        if(e.HealingMaximum>0m)
        {
            var delta=e.HealingMinimum+
                (e.HealingMaximum-e.HealingMinimum)*(decimal)instance.Rng.NextDouble();
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
        var previous=_active.GetValueOrDefault(instance.Id);
        var max=Math.Max(1,e.MaxStacks);
        var stacks=Math.Min(max,(previous?.Stacks??0)+1);
        var aura=_auras.ApplyAura(context,actor,new AuraDefinition {
            Key="equipment-"+instance.Id,Name=e.Key,
            DurationSeconds=e.DurationSeconds,
            StackingMode=max>1?AuraStackingMode.Stack:AuraStackingMode.Refresh,
            MaxStacks=max
        },_actorKey,"equipment-"+instance.Id,instance.Id);
        var oldHaste=actor.Stats.Get("haste-percent");
        if(previous is null || previous.Stacks<stacks)
            foreach(var (key,value) in e.StatBonuses)
                actor.Stats.Set(key,actor.Stats.Get(key)+value);
        _autoAttacks?.RescheduleForHasteChange(context,_actorKey,
            oldHaste,actor.Stats.Get("haste-percent"));
        _active[instance.Id]=new Active(aura.InstanceId,
            new Dictionary<string,decimal>(e.StatBonuses,StringComparer.OrdinalIgnoreCase),
            stacks,previous?.StartedAt??context.CurrentTimeSeconds);
    }

    private void TryActivate(SimulationContext context,Instance instance)
    {
        var e=instance.Effect;var actor=context.GetActor(_actorKey);
        if(actor is null || !actor.IsAlive)return;
        if(e.ResourceMaximum>0m)
        {
            if(!actor.Resources.TryGetValue(e.ResourceKey,out var resource) ||
               resource.Maximum-resource.Current<e.ActivationMissingResourceMinimum)
                return;
        }
        if(e.CooldownSeconds<=0m)throw new InvalidOperationException($"On-use {e.Key} has no cooldown.");
        var next=Math.Max(_nextAvailable.GetValueOrDefault(instance.Id),
            actor.GetSharedCooldownReadyAt(e.SharedCooldownGroup,context.CurrentTimeSeconds));
        if(next>context.CurrentTimeSeconds)
        {
            Schedule(context,instance,next);
            return;
        }
        if(AvailableCharges(instance,context.CurrentTimeSeconds)==0)
        {
            if(_chargeReadyAt.TryGetValue(instance.Id,out var chargeReady))
                Schedule(context,instance,chargeReady);
            return;
        }
        ConsumeCharge(instance,context.CurrentTimeSeconds);
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
        foreach(var (key,value) in _resourceByEffect)
            run.CustomMetrics["equipment-"+key+"-resource"]=value;
        foreach(var (key,value) in _uptimeByEffect)
            run.CustomMetrics["equipment-"+key+"-uptime-seconds"]=value;
        foreach(var effect in _effects.Where(x=>x.Effect.MaxCharges>0))
            run.CustomMetrics["equipment-"+effect.Id+"-charges-left"]=
                AvailableCharges(effect,run.Summary.DurationSeconds);
    }
}

// Explicitly constructed engine-level/source-reviewed effect binding.
// Runtime API selections never deserialize arbitrary effect definitions.
public sealed record ForeverEquipmentEffectBinding(
    string Slot,ForeverExecutableEquipmentEffect Effect,decimal WeaponSpeedSeconds=0m);
