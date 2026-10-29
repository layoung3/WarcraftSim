using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Target-owned Demonic Brand from a landed Searing Pain. A pet's next 2/4/6
/// landed attacks against that branded target trigger elemental Shadow/Fire
/// damage through the existing executor. Pet primary hits and their physical
/// hit tables remain untouched; the brand proc does not recursively trigger.
/// </summary>
public sealed class WarlockDemonicBrandProcessor : ICombatEventProcessor
{
    private readonly int _rank;
    private readonly AuraManager _auras;
    private readonly AbilityExecutor _executor;
    private readonly SimulationActorState _owner;
    private readonly decimal _spellPower;
    private readonly IReadOnlyDictionary<string,string> _petFamilies;
    private readonly Func<string,bool> _isCompanionActive;
    private readonly Dictionary<string,(int Charges,decimal Expires)> _brands =
        new(StringComparer.OrdinalIgnoreCase);
    public int Triggers {get;private set;}
    public int Applications {get;private set;}

    public WarlockDemonicBrandProcessor(int rank, AuraManager auras, AbilityExecutor executor,
        SimulationActorState owner, decimal spellPower,
        IReadOnlyDictionary<string,string> petFamilies,
        Func<string,bool>? isCompanionActive=null)
    {
        if(rank is <0 or >3 || spellPower<0m)
            throw new ArgumentOutOfRangeException(nameof(rank));
        _rank=rank;
        _auras=auras;
        _executor=executor;
        _owner=owner;
        _spellPower=spellPower;
        _petFamilies=petFamilies;
        _isCompanionActive=isCompanionActive ?? (_=>true);
    }

    private static bool IsShadow(string family) =>
        family is "voidwalker" or "felhunter" or "succubus" or "incubus" or "doomguard";

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (_rank == 0 || ev.Type != CombatEventType.Damage ||
            ev.TargetActorKey is not { } targetKey || ev.Amount is not >0m ||
            ev.ResultKey is not ("hit" or "critical")) return;
        var target=context.GetActor(targetKey);
        if(target is null || target.TeamKey==_owner.TeamKey) return;
        if(ev.SourceActorKey == _owner.Key && ev.AbilityKey=="searing-pain")
        {
            _brands[targetKey]=(2*_rank,context.CurrentTimeSeconds+10m);
            _auras.ApplyAura(context,target,new AuraDefinition {
                Key="demonic-brand",Name="Demonic Brand",DurationSeconds=10m
            },_owner.Key,"searing-pain","demonic-brand");
            Applications++;
            return;
        }
        if(ev.SourceActorKey is null || !_petFamilies.TryGetValue(ev.SourceActorKey,out var family) ||
            string.IsNullOrWhiteSpace(family) || ev.AbilityKey is
                ("demonic-brand-fire" or "demonic-brand-shadow") ||
            !_brands.TryGetValue(targetKey,out var brand) ||
            context.CurrentTimeSeconds >= brand.Expires) return;
        // Only a branded target and an actual successful companion attack
        // can spend a charge. A miss, different enemy or owner spell cannot.
        var source=context.GetActor(ev.SourceActorKey);
        if(source is null || !source.IsAlive || !_isCompanionActive(source.Key)) return;
        // A removed/expired aura cannot spend cached charges. This also
        // handles a Bane/Brand dispel or same-timestamp expiration safely.
        if(!target.ActiveAuras.Any(a=>a.Definition.Key=="demonic-brand" &&
            a.SourceActorKey==_owner.Key && a.IsActiveAt(context.CurrentTimeSeconds)))
        {
            _brands.Remove(targetKey);
            return;
        }
        if(brand.Charges<=1)
        {
            _brands.Remove(targetKey);
            _auras.RemoveAuras(context,target,"demonic-brand",_owner.Key,
                "Demonic Brand charges exhausted");
        }
        else _brands[targetKey]=(brand.Charges-1,brand.Expires);

        var shadow=IsShadow(family);
        var school=shadow?"shadow":"fire";
        // Rank 1/2/3 share the current client base damage, while the rank
        // controls charges. Coefficient is provisional from the beta guide.
        var value=66.5m+_spellPower*.078m;
        _executor.ExecuteBackgroundDirectDamage(context,source,target,
            shadow?"demonic-brand-shadow":"demonic-brand-fire",
            "Demonic Brand",new AbilityEffectDefinition {
                Key="demonic-brand-proc",EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey=school,
                ResolutionType=CombatResolutionTypes.AlwaysHits,
                MitigationType=DamageMitigationTypes.None,
                CanMiss=false,CanCrit=false,
                MinimumValue=value,MaximumValue=value
            });
        Triggers++;
    }
}
