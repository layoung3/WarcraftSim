using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Hand-specific Enhancement imbue state. Existing melee damage events trigger
/// additional Fire/Frost effects through the same executor; they never create
/// extra weapon swings or recursively proc Maelstrom/Windfury. Bonus Rockbiter
/// AP applies only to that hand's ordinary swing and Stormstrike scaling stat.
/// </summary>
public sealed class ShamanWeaponImbueProcessor : ICombatEventProcessor
{
    private readonly SimulationActorState _actor;
    private readonly AuraManager _auras;
    private readonly AbilityExecutor _executor;
    private readonly decimal _baseAp, _mainSpeed, _offSpeed, _frostChance;
    private readonly int _elementalWeapons;
    private readonly Dictionary<string,string> _active=new(StringComparer.OrdinalIgnoreCase)
        { ["main-hand"]="none", ["off-hand"]="none" };
    public string MainHand => _active["main-hand"];
    public string OffHand => _active["off-hand"];
    public int FlametongueProcs { get; private set; }
    public int FrostbrandProcs { get; private set; }

    public ShamanWeaponImbueProcessor(SimulationActorState actor,AuraManager auras,
        AbilityExecutor executor,decimal baseAp,decimal mainSpeed,decimal offSpeed,
        int elementalWeapons,decimal frostbrandChancePercent)
    {
        if(baseAp<0m || mainSpeed<=0m || offSpeed<=0m ||
            elementalWeapons is <0 or >3 ||
            frostbrandChancePercent is <0m or >100m)
            throw new ArgumentException("Invalid Shaman weapon imbue setup.");
        _actor=actor;
        _auras=auras;
        _executor=executor;
        _baseAp=baseAp;
        _mainSpeed=mainSpeed;
        _offSpeed=offSpeed;
        _elementalWeapons=elementalWeapons;
        _frostChance=frostbrandChancePercent;
        RefreshHandAttackPower();
    }

    private void RefreshHandAttackPower()
    {
        decimal rockBonus(string hand) => _active[hand]=="rockbiter"
            ? 554m * (1m+(_elementalWeapons switch {
                1 => .07m, 2 => .13m, 3 => .20m, _ => 0m })) : 0m;
        _actor.Stats.Set("shaman-main-hand-attack-power",_baseAp+rockBonus("main-hand"));
        _actor.Stats.Set("shaman-off-hand-attack-power",_baseAp+rockBonus("off-hand"));
    }

    private static bool TryParse(string? key,out string kind,out string hand)
    {
        foreach(var k in new[] {"rockbiter","flametongue","frostbrand"})
            foreach(var h in new[] {"main-hand","off-hand"})
                if(key==$"{k}-weapon-{h}") { kind=k;hand=h;return true; }
        kind="none";hand="none";return false;
    }

    public void Process(SimulationContext ctx,CombatEvent ev)
    {
        if(ev.SourceActorKey!=_actor.Key) return;
        if(ev.Type==CombatEventType.AuraApplied && ev.TargetActorKey==_actor.Key &&
            TryParse(ev.AbilityKey,out var applied,out var hand))
        {
            // Mutually exclusive within one hand, but never wipe another hand.
            _active[hand]=applied;
            foreach(var aura in _actor.ActiveAuras.Where(x=>
                x.Definition.Key.StartsWith($"shaman-imbue-{hand}-",
                    StringComparison.OrdinalIgnoreCase) &&
                x.Definition.Key!=$"shaman-imbue-{hand}-{applied}").ToArray())
                _auras.RemoveAura(ctx,_actor,aura,"replaced by new weapon imbue");
            RefreshHandAttackPower();
            return;
        }
        if(ev.Type==CombatEventType.AuraRemoved && ev.TargetActorKey==_actor.Key &&
            TryParse(ev.AbilityKey,out var removed,out var removedHand) &&
            _active[removedHand]==removed)
        {
            _active[removedHand]="none";
            RefreshHandAttackPower();
            return;
        }
        if(ev.Type!=CombatEventType.Damage || ev.Amount is not >0m ||
            ev.SchoolKey!="physical" || ev.TargetActorKey is null ||
            ev.AbilityKey is not ("main-hand-auto" or "off-hand-auto" or "stormstrike") ||
            ev.WeaponHandKey is not ("main-hand" or "off-hand")) return;
        var target=ctx.GetActor(ev.TargetActorKey);
        if(target is null || target.TeamKey==_actor.TeamKey || !target.IsAlive) return;
        var weaponHand=ev.WeaponHandKey;
        var imbue=_active[weaponHand];
        if(imbue is not ("flametongue" or "frostbrand")) return;
        if(imbue=="frostbrand" &&
            (decimal)ctx.Random.NextDouble()*100m>=_frostChance) return;
        var speed=weaponHand=="main-hand"?_mainSpeed:_offSpeed;
        var elementalBonus=1m+_elementalWeapons*.05m;
        // 70532 supplies a speed-dependent range, but not its server curve.
        // Explicit linear speed approximation until actual-client measurements.
        var damage=imbue=="flametongue"
            ? Math.Clamp(31.4m*speed,31.4m,99.9m)*elementalBonus
            : 158m*elementalBonus;
        var school=imbue=="flametongue"?"fire":"frost";
        var key=$"{imbue}-weapon-{weaponHand}-proc";
        _executor.ExecuteBackgroundDirectDamage(ctx,_actor,target,key,
            $"{imbue} Weapon",new AbilityEffectDefinition {
                Key="imbue-hit",EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey=school,
                MitigationType=DamageMitigationTypes.None,
                ResolutionType=CombatResolutionTypes.AlwaysHits,
                CanMiss=false,CanCrit=false,
                MinimumValue=damage,MaximumValue=damage
            });
        if(imbue=="flametongue") FlametongueProcs++;
        else
        {
            FrostbrandProcs++;
            _auras.ApplyAura(ctx,target,new AuraDefinition {
                Key="shaman-frostbrand-slow",Name="Frostbrand Slow",
                DurationSeconds=8m
            },_actor.Key,key,"frostbrand-slow");
        }
    }
}
