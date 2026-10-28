using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// One Shaman's four live slots. Observes actual applied auras from individual
/// casts or a programmed Call, removes replaced auras in the same slot and
/// refunds only currently active paid slots on Totemic Recall. Existing
/// periodic effect instances and Windfury proc engine remain authoritative.
/// </summary>
public sealed class ShamanTotemLifecycleProcessor : ICombatEventProcessor
{
    private readonly SimulationActorState _owner;
    private readonly AuraManager _auras;
    private readonly Action? _refreshWeaponStats;
    private readonly Dictionary<string,TotemSlot> _slots=new(StringComparer.OrdinalIgnoreCase);
    public int Placements {get;private set;}
    public int Replacements {get;private set;}
    public int Recalls {get;private set;}
    public decimal RecallManaGained {get;private set;}
    public IReadOnlyDictionary<string,string> ActiveSlots =>
        _slots.ToDictionary(x=>x.Key,x=>x.Value.TotemKey,StringComparer.OrdinalIgnoreCase);

    private sealed record TotemSlot(string TotemKey,string AuraKey,string TargetKey,
        decimal Cost,decimal Expires);

    public ShamanTotemLifecycleProcessor(SimulationActorState owner,AuraManager auras,
        Action? refreshWeaponStats=null)
    {
        _owner=owner;
        _auras=auras;
        _refreshWeaponStats=refreshWeaponStats;
    }

    private static string? Identify(CombatEvent ev) => ev.EffectKey switch
    {
        "strength-of-earth-aura" => "strength-of-earth-totem",
        "stoneskin-aura" => "stoneskin-totem",
        "searing-totem-tick" => "searing-totem",
        "mana-spring-totem-tick" => "mana-spring-totem",
        "windfury-totem-air" => "windfury-totem",
        _ => null
    };
    private static string Aura(string key)=>key switch
    {
        "strength-of-earth-totem"=>"shaman-strength-of-earth-totem",
        "stoneskin-totem"=>"shaman-stoneskin-totem",
        "searing-totem"=>"searing-totem",
        "mana-spring-totem"=>"mana-spring-totem",
        "windfury-totem"=>"shaman-windfury-totem",
        _=>throw new ArgumentException("Unknown totem key.",nameof(key))
    };

    private void ChangeStrength(decimal amount)
    {
        _owner.Stats.Set(ForeverCombatStatKeys.AttackPower,
            Math.Max(0m,_owner.Stats.Get(ForeverCombatStatKeys.AttackPower)+amount));
        _refreshWeaponStats?.Invoke();
    }

    private void RemoveSlot(SimulationContext context,string slot)
    {
        if(!_slots.Remove(slot,out var existing))return;
        var recipient=context.GetActor(existing.TargetKey);
        if(recipient is not null)
            _auras.RemoveAuras(context,recipient,existing.AuraKey,
                _owner.Key,"Shaman totem slot replaced or recalled");
        if(slot=="fire")
            _auras.RemoveAuras(context,_owner,"shaman-fire-totem-active",
                _owner.Key,"Fire totem removed");
        if(slot=="earth" && existing.TotemKey=="strength-of-earth-totem")
            ChangeStrength(-106m);
    }

    private void Register(SimulationContext context,CombatEvent ev,string key,string slot)
    {
        var target=context.GetActor(ev.TargetActorKey ?? "");
        if(target is null)return;
        var aura=target.ActiveAuras.LastOrDefault(a=>a.Definition.Key==Aura(key) &&
            a.SourceActorKey==_owner.Key && a.IsActiveAt(context.CurrentTimeSeconds));
        if(aura is null)return;
        var paid= _owner.Abilities.GetValueOrDefault(key)?.Definition.ResourceCosts
            .FirstOrDefault(x=>x.ResourceKey=="mana")?.Amount ?? 0m;
        if(_slots.TryGetValue(slot,out var old))
        {
            // The same buff has already refreshed its aura; don't remove it.
            if(old.TotemKey!=key)RemoveSlot(context,slot);
            else if(old.TargetKey!=target.Key)RemoveSlot(context,slot);
            Replacements++;
        }
        _slots[slot]=new TotemSlot(key,Aura(key),target.Key,paid,
            aura.ExpiresAtSeconds);
        if(slot=="earth" && key=="strength-of-earth-totem" &&
            (old is null || old.TotemKey!=key))
            ChangeStrength(106m); // 53 Strength * 2 AP/Strength at level 60.
        Placements++;
    }

    public void Process(SimulationContext context,CombatEvent ev)
    {
        if(ev.SourceActorKey!=_owner.Key)return;
        if(ev.Type==CombatEventType.AuraApplied &&
            Identify(ev) is { } key &&
            ForeverShamanTotemCalls.Slots.TryGetValue(key,out var slot))
        {
            Register(context,ev,key,slot);
            return;
        }
        if(ev.Type==CombatEventType.AuraRemoved && Identify(ev) is { } removed &&
            ForeverShamanTotemCalls.Slots.TryGetValue(removed,out var removedSlot) &&
            _slots.TryGetValue(removedSlot,out var current) &&
            current.TotemKey==removed &&
            current.TargetKey==ev.TargetActorKey)
        {
            var target=context.GetActor(current.TargetKey);
            if(target?.ActiveAuras.Any(a=>a.Definition.Key==current.AuraKey &&
                a.SourceActorKey==_owner.Key && a.IsActiveAt(context.CurrentTimeSeconds))!=true)
                RemoveSlot(context,removedSlot);
            return;
        }
        if(ev.Type!=CombatEventType.AbilityCastCompleted ||
            ev.AbilityKey!="totemic-recall")return;
        decimal refundable=0m;
        foreach(var slotKey in _slots.Keys.ToList())
        {
            var active=_slots[slotKey];
            if(active.Expires>context.CurrentTimeSeconds)
                refundable+=active.Cost*.25m;
            RemoveSlot(context,slotKey);
        }
        Recalls++;
        if(refundable<=0m || !_owner.Resources.TryGetValue("mana",out var mana))return;
        mana.AdvanceTo(context.CurrentTimeSeconds);
        var gained=mana.Gain(refundable);
        if(gained<=0m)return;
        RecallManaGained+=gained;
        context.EmitEvent(new CombatEvent {
            TimeSeconds=context.CurrentTimeSeconds,Type=CombatEventType.ResourceChanged,
            SourceActorKey=_owner.Key,TargetActorKey=_owner.Key,
            AbilityKey="totemic-recall",EffectKey="totemic-recall-refund",
            ResourceKey="mana",ResourceTotalAfter=mana.Current,Amount=gained
        });
    }
}
