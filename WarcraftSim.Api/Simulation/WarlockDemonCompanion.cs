using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Companions;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Warlock;

namespace WarcraftSim.Api.Simulation;

/// <summary>Warlock-specific companion lifecycle. All attacks, casts, resources and
/// auras are processed by the normal SimulationEngine and shared companion actor.</summary>
public sealed class WarlockDemonCompanion : ICombatEventProcessor
{
    private readonly WarlockSimRequest _request;
    private readonly SimulationContext _context;
    private readonly SimulationActorState _owner, _target;
    private readonly AuraManager _auras;
    private readonly AutoAttackProcessor _autos;
    private readonly AbilityExecutor _executor;
    private readonly ForeverWarlockDemonProfile _family;
    private readonly ForeverWarlockDemonTuning _tuning;
    private readonly AutoAttackDefinition? _melee;
    private readonly decimal _activeDelay;
    private decimal _engagedAt=-1m,_totalUptime;
    private bool _inactive;
    public SimulationActorState Actor { get; }
    public bool IsActive => !_inactive && _engagedAt>=0m && Actor.IsAlive;
    public decimal Uptime => _totalUptime+(_engagedAt>=0m?Math.Max(0m,_context.CurrentTimeSeconds-_engagedAt):0m);
    public bool Died {get;private set;}
    public bool Sacrificed {get;private set;}
    public int SpecialCasts {get;private set;}
    public decimal BonusThreat {get;private set;}
    public decimal SpellDamage {get;private set;}
    public decimal AutoDamage {get;private set;}
    public decimal CriticalHits {get;private set;}
    public decimal Misses {get;private set;}
    public ForeverWarlockDemonProfile Family => _family;
    public WarlockDemonCompanion(WarlockSimRequest request,SimulationContext context,
        SimulationActorState owner,SimulationActorState target,AuraManager auras,
        AutoAttackProcessor autos,AbilityExecutor executor)
    {
        _request=request;_context=context;_owner=owner;_target=target;
        _auras=auras;_autos=autos;_executor=executor;
        _family=ForeverWarlockDemons.Get(request.PetType);
        _tuning=request.DemonTuning;
        _activeDelay=_tuning.DemonEngagementDelaySeconds;
        var ap=200m+Math.Max(0m,owner.Stats.Get("intellect"))*request.Tuning.PetAttackPowerFromIntellect;
        // Existing baseline scaling is retained; the shared factory owns
        // actor registration and optional owner inheritance.
        var (actor,_) = CompanionActorFactory.Create(context,owner,new CompanionDefinition {
            Key="warlock-pet",OwnerKey=owner.Key,Name="Warlock "+_family.Key,
            FamilyKey=_family.Key,BaseHealth=_tuning.BaseHealth,
            FamilyHealthMultiplier=_family.HealthMultiplier*
                (1m+.05m*request.Talents.Rank("fel-vitality")),
            BaseArmor=_tuning.BaseArmor,FamilyArmorMultiplier=_family.ArmorMultiplier,
            BaseAttackPower=ap*_family.AttackPowerMultiplier,
            OwnerHealthCoefficient=_tuning.OwnerHealthInheritance,
            OwnerArmorCoefficient=_tuning.OwnerArmorInheritance,
            BaseResource=_tuning.BaseMana*_family.ManaMultiplier,
            ResourceKey="mana",ResourceRegenPerSecond=_tuning.ManaRegenPerSecond
        });
        Actor=actor;
        Actor.Stats.Set("spell-power",owner.Stats.Get("spell-power")*_tuning.OwnerSpellPowerInheritance);
        Actor.Stats.Set("intellect",owner.Stats.Get("intellect"));
        foreach(var ability in ForeverWarlockDemonSpellbook.Create(_family,_tuning,
            request.Talents.Rank("improved-felhunter")))Actor.AddAbility(ability);
        if(_family.HasMeleeAuto)
        {
            var apScale=Actor.Stats.Get("attack-power")*request.Tuning.PetSwingSeconds/14m;
            var unholy=1m+.02m*request.Talents.Rank("unholy-power");
            _melee=new AutoAttackDefinition {
                Key="pet-melee",Name=_family.Key+" melee",
                SwingIntervalSeconds=request.Tuning.PetSwingSeconds*_family.SwingMultiplier,
                DamageEffect=new AbilityEffectDefinition {
                    Key="damage",EffectType=AbilityEffectTypes.DirectDamage,
                    TargetType=AbilityTargetTypes.Enemy,SchoolKey="physical",
                    ResolutionType=CombatResolutionTypes.Melee,
                    CanMiss=true,CanCrit=true,CanBeDodged=true,CanBeParried=true,
                    MinimumValue=(request.Tuning.PetBaseDamage+apScale)*unholy,
                    MaximumValue=(request.Tuning.PetBaseDamage+apScale*1.1m)*unholy
                }
            };
        }
    }
    public ICombatEventProcessor Rotation()
    {
        var p=new RotationProfile {Name="Warlock "+_family.Key+" autocast",
            ClassKey="warlock-demon",RulesetKey="wow-forever",
            SimulationType=SimulationType.Dps};
        var priorities=_family.Key switch {
            "imp" => new[]{"demon-firebolt"},
            "voidwalker" => new[]{"demon-torment"},
            "succubus" or "incubus" => new[]{"demon-lash-of-pain"},
            "felhunter" => new[]{"demon-shadow-bite"},
            "infernal" => new[]{"demon-immolation"},
            "doomguard" => new[]{"demon-doom-bolt"},
            _ => Array.Empty<string>()
        };
        for(var i=0;i<priorities.Length;i++)
            p.Entries.Add(new RotationEntry {Priority=i,AbilityKey=priorities[i]});
        return new PriorityRotationExecutor(p,Actor.Key,_target.Key,_executor,
            reactToTargetStateChanges:true,autoAttackProcessor:_autos);
    }
    public void Start(SimulationContext ctx)
    {
        Schedule(ctx,_activeDelay,"demon-engage");
        if(_family.IsTemporary)Schedule(ctx,_activeDelay+_tuning.TemporaryDemonDurationSeconds,"demon-despawn");
        if(_request.DemonDeathAtSeconds.HasValue)Schedule(ctx,_request.DemonDeathAtSeconds.Value,"demon-death");
        if(_request.DemonSacrificeAtSeconds.HasValue)Schedule(ctx,_request.DemonSacrificeAtSeconds.Value,"demon-sacrifice");
    }
    private void Schedule(SimulationContext ctx,decimal time,string key)
    {
        if(time<0m||time>=_request.DurationSeconds)return;
        ctx.ScheduleEvent(new CombatEvent {
            TimeSeconds=time,Type=CombatEventType.RotationDecision,
            SourceActorKey=_owner.Key,TargetActorKey=_target.Key,
            EffectKey=key,IsInternal=true
        });
    }
    private void Engage(decimal now)
    {
        if(_inactive||!Actor.IsAlive||_engagedAt>=0m)return;
        _engagedAt=now;
        _auras.ApplyAura(_context,Actor,new AuraDefinition {
            Key="warlock-demon-engaged",Name="Demon engaged",DurationSeconds=_request.DurationSeconds-now
        },_owner.Key,"demon-engage","demon-state");
        if(_melee is not null)_autos.Start(_context,Actor.Key,_target.Key,_melee,0m);
    }
    private void Leave(decimal now,string reason)
    {
        if(_inactive)return;
        _inactive=true;
        if(_engagedAt>=0m){_totalUptime+=now-_engagedAt;_engagedAt=-1m;}
        _autos.Stop(_context,Actor.Key,"pet-melee");
        _auras.RemoveAuras(_context,Actor,"warlock-demon-engaged",_owner.Key,reason);
        _context.EmitEvent(new CombatEvent {
            TimeSeconds=now,Type=CombatEventType.AuraRemoved,SourceActorKey=Actor.Key,
            TargetActorKey=Actor.Key,AbilityKey=reason,
            Description="Warlock demon "+reason
        });
    }
    public void Process(SimulationContext context,CombatEvent ev)
    {
        var now=ev.TimeSeconds;
        if(ev.Type==CombatEventType.RotationDecision && ev.IsInternal &&
            ev.SourceActorKey==_owner.Key)
        {
            switch(ev.EffectKey){
                case "demon-engage":Engage(now);break;
                case "demon-despawn":Leave(now,"demon-despawn");break;
                case "demon-sacrifice":Sacrificed=true;Leave(now,"demon-sacrifice");break;
                case "demon-death":
                    if(Actor.IsAlive){Actor.TakeDamage(Actor.CurrentHealth);Died=true;}
                    Leave(now,"demon-death");
                    context.EmitEvent(new CombatEvent {
                        TimeSeconds=now,Type=CombatEventType.ActorDied,
                        SourceActorKey=_target.Key,TargetActorKey=Actor.Key,
                        Description="Scripted Warlock demon death"
                    });
                    break;
            }
        }
        if(ev.SourceActorKey!=Actor.Key)return;
        if(ev.Type==CombatEventType.Damage)
        {
            if(ev.AbilityKey=="pet-melee")AutoDamage+=ev.Amount??0m;
            else SpellDamage+=ev.Amount??0m;
            if(ev.IsCritical)CriticalHits++;
            if(ev.ResultKey is "miss" or "dodge" or "parry")Misses++;
        }
        if(ev.Type==CombatEventType.AbilityCastCompleted)
        {
            SpecialCasts++;
            if(ev.AbilityKey is "demon-torment" or "demon-suffering")
            {
                var amount=ev.AbilityKey=="demon-torment"?
                    _tuning.VoidwalkerTormentThreat:_tuning.VoidwalkerSufferingThreat;
                ThreatManipulator.Apply(context,_target,Actor,
                    ThreatManipulationOperationTypes.Add,amount);
                BonusThreat+=amount;
                context.EmitEvent(new CombatEvent {
                    TimeSeconds=now,Type=CombatEventType.ThreatChanged,
                    SourceActorKey=Actor.Key,TargetActorKey=_target.Key,
                    AbilityKey=ev.AbilityKey,Amount=amount,
                    Description="Voidwalker bonus threat"
                });
            }
            if(ev.AbilityKey=="demon-spell-lock")
                _auras.ApplyAura(context,_target,new AuraDefinition {
                    Key="demon-spell-lock",Name="Spell Lock",DurationSeconds=3m
                },Actor.Key,"demon-spell-lock","demon-silence");
        }
    }
}

public sealed class WarlockDemonThreat : IThreatGenerationResolver
{
    public IReadOnlyList<ThreatGenerationContribution> Resolve(SimulationContext ctx,CombatEvent e)
    {
        if(e.Type!=CombatEventType.Damage||e.Amount is not >0m ||
            e.SourceActorKey is null||e.TargetActorKey is null ||
            ctx.GetActor(e.SourceActorKey)?.TeamKey!="raid" ||
            ctx.GetActor(e.TargetActorKey)?.TeamKey!="enemy")return [];
        return [new ThreatGenerationContribution {
            ThreatOwnerActorKey=e.TargetActorKey,ThreatSourceActorKey=e.SourceActorKey,Amount=e.Amount.Value
        }];
    }
}
