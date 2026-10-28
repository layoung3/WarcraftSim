using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Companions;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Warlock;
using WarcraftSim.Data.Forever.Combat;

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
    private readonly decimal _activeDelay, _ownerInitialSpellPower, _ownerSpirit;
    private readonly bool _isReplacement;
    private decimal _engagedAt=-1m,_totalUptime;
    private decimal _lastManaSample,_spentSinceSample,_lastManaSampleAt;
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
        AutoAttackProcessor autos,AbilityExecutor executor,
        decimal ownerInitialSpellPower,decimal ownerSpirit,
        string actorKey="warlock-pet",string? demonType=null,
        decimal startAtSeconds=0m,bool isReplacement=false)
    {
        _request=request;_context=context;_owner=owner;_target=target;
        _auras=auras;_autos=autos;_executor=executor;
        _family=ForeverWarlockDemons.Get(demonType??request.PetType);
        _isReplacement=isReplacement;
        _tuning=request.DemonTuning;
        _activeDelay=startAtSeconds+_tuning.DemonEngagementDelaySeconds;
        _ownerInitialSpellPower=ownerInitialSpellPower;
        _ownerSpirit=ownerSpirit;
        var ap=200m+Math.Max(0m,owner.Stats.Get("intellect"))*request.Tuning.PetAttackPowerFromIntellect;
        // Existing baseline scaling is retained; the shared factory owns
        // actor registration and optional owner inheritance.
        var (actor,_) = CompanionActorFactory.Create(context,owner,new CompanionDefinition {
            Key=actorKey,OwnerKey=owner.Key,Name="Warlock "+_family.Key,
            FamilyKey=_family.Key,BaseHealth=_tuning.BaseHealth,
            FamilyHealthMultiplier=_family.HealthMultiplier*
                (1m+(_family.IsTemporary?0m:.05m*request.Talents.Rank("fel-vitality"))),
            BaseArmor=_tuning.BaseArmor,FamilyArmorMultiplier=_family.ArmorMultiplier,
            BaseAttackPower=ap*_family.AttackPowerMultiplier,
            OwnerHealthCoefficient=_tuning.OwnerHealthInheritance,
            OwnerArmorCoefficient=_tuning.OwnerArmorInheritance,
            BaseResource=_tuning.BaseMana*_family.ManaMultiplier*
                (1m+(_family.IsTemporary?0m:.05m*request.Talents.Rank("fel-vitality"))),
            ResourceKey="mana",ResourceRegenPerSecond=_tuning.ManaRegenPerSecond
        });
        Actor=actor;
        // Official Forever notes reserve brand bonus threat for tanking demons.
        Actor.Stats.Set("demonic-brand-tank-threat",_family.Key=="voidwalker"?1m:0m);
        _lastManaSample=Actor.Resources["mana"].Current;
        Actor.Stats.Set("spell-power",owner.Stats.Get("spell-power")*_tuning.OwnerSpellPowerInheritance+
            Warlock70425DamageModifiers.DemonicKnowledgeSpellPower(
                request.Talents.Rank("demonic-knowledge"),owner.Level,true));
        Actor.Stats.Set("intellect",owner.Stats.Get("intellect"));
        foreach(var ability in ForeverWarlockDemonSpellbook.Create(_family,_tuning,
            request.Talents.Rank("improved-felhunter"),
            request.Talents.Rank("improved-imp"),
            request.Talents.Rank("improved-sayaad")))Actor.AddAbility(ability);
        if(_family.HasMeleeAuto)
        {
            var apScale=Actor.Stats.Get("attack-power")*request.Tuning.PetSwingSeconds/14m;
            // Unholy Power is applied to all eligible demon damage at impact
            // by the Warlock roll resolver, avoiding double application.
            _melee=new AutoAttackDefinition {
                Key="pet-melee",Name=_family.Key+" melee",
                SwingIntervalSeconds=request.Tuning.PetSwingSeconds*_family.SwingMultiplier,
                DamageEffect=new AbilityEffectDefinition {
                    Key="damage",EffectType=AbilityEffectTypes.DirectDamage,
                    TargetType=AbilityTargetTypes.Enemy,SchoolKey="physical",
                    ResolutionType=ForeverCombatResolutionTypes.CreatureMeleeAuto,
                    MitigationType=DamageMitigationTypes.Armor,
                    CanMiss=true,CanCrit=true,CanBeDodged=true,CanBeParried=true,
                    MinimumValue=(request.Tuning.PetBaseDamage+apScale),
                    MaximumValue=(request.Tuning.PetBaseDamage+apScale*1.1m)
                }
            };
            // A demon is a creature companion, not a player weapon user.
            // Use the shared creature-auto hit table without claiming an
            // unverified pet-crushing or glancing rule against raid bosses.
            ForeverMeleeAttackProfiles.Apply(_melee.DamageEffect,
                ForeverMeleeAttackProfiles.CreatureAuto);
            _melee.DamageEffect.CanCrush=false;
        }
    }
    public ICombatEventProcessor Rotation()
    {
        var p=new RotationProfile {Name="Warlock "+_family.Key+" autocast",
            ClassKey="warlock-demon",RulesetKey="wow-forever",
            SimulationType=SimulationType.Dps};
        var priorities=_family.Key switch {
            "imp" => new[]{"demon-firebolt"},
            "voidwalker" => new[]{"demon-suffering","demon-torment"},
            "succubus" or "incubus" => _request.DemonTargetSusceptibleToSeduction ?
                new[]{"demon-seduction","demon-lash-of-pain"} : new[]{"demon-lash-of-pain"},
            "felhunter" => _request.DemonTargetIsCasting ?
                new[]{"demon-spell-lock","demon-shadow-bite"} :
                _request.DemonTargetHasMagicAura ?
                    new[]{"demon-devour-magic","demon-shadow-bite"} :
                    new[]{"demon-shadow-bite"},
            "infernal" => new[]{"demon-immolation"},
            "doomguard" => new[]{"demon-doom-bolt"},
            _ => Array.Empty<string>()
        };
        for(var i=0;i<priorities.Length;i++)
        {
            var entry=new RotationEntry {Priority=i,AbilityKey=priorities[i]};
            if(priorities[i]=="demon-devour-magic")
                entry.Conditions.Add(new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.TargetAuraActive,
                    Key="demon-test-magic-buff"
                });
            if(priorities[i]=="demon-seduction")
                entry.Conditions.Add(new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.TargetAuraMissing,Key="demon-seduction"
                });
            p.Entries.Add(entry);
        }
        return new PriorityRotationExecutor(p,Actor.Key,_target.Key,_executor,
            reactToTargetStateChanges:true,autoAttackProcessor:_autos);
    }
    public void Start(SimulationContext ctx)
    {
        Schedule(ctx,_activeDelay,"demon-engage");
        if(_family.IsTemporary)Schedule(ctx,_activeDelay+_tuning.TemporaryDemonDurationSeconds,"demon-despawn");
        if(!_isReplacement){
            if(_request.DemonDeathAtSeconds.HasValue)
                Schedule(ctx,_request.DemonDeathAtSeconds.Value,"demon-death");
            if(_request.DemonSacrificeAtSeconds.HasValue)
                Schedule(ctx,_request.DemonSacrificeAtSeconds.Value,"demon-sacrifice");
            if(_request.DemonReplacementAtSeconds.HasValue)
                Schedule(ctx,_request.DemonReplacementAtSeconds.Value,"demon-replaced");
        }
    }
    private void Schedule(SimulationContext ctx,decimal time,string key)
    {
        if(time<0m||time>=_request.DurationSeconds)return;
        ctx.ScheduleEvent(new CombatEvent {
            TimeSeconds=time,Type=CombatEventType.RotationDecision,
            SourceActorKey=Actor.Key,TargetActorKey=_target.Key,
            EffectKey=key,IsInternal=true
        });
    }
    private void Engage(decimal now)
    {
        if(_inactive||!Actor.IsAlive||_engagedAt>=0m)return;
        _engagedAt=now;
        _lastManaSampleAt=now;
        RestorePetDependentSpellPower();
        if(_isReplacement)
        {
            // Without Pact, summoning cancels every Sacrifice. With Pact,
            // only resummoning the sacrificed family cancels that buff.
            WarlockSacrificeRules.CancelOnSummon(_context,_auras,_owner,
                _family.Key,_request.Talents.Rank("demonic-pact")>0);
        }
        _auras.ApplyAura(_context,Actor,new AuraDefinition {
            Key="warlock-demon-engaged",Name="Demon engaged",DurationSeconds=_request.DurationSeconds-now
        },_owner.Key,"demon-engage","demon-state");
        if(_melee is not null)_autos.Start(_context,Actor.Key,_target.Key,_melee,0m);
        Schedule(_context,now+1m,"demon-mana-regeneration-report");
    }
    private void RestorePetDependentSpellPower() =>
        SetOwnerDemonicKnowledge(_ownerInitialSpellPower);
    private void RemovePetDependentSpellPower() =>
        SetOwnerDemonicKnowledge(Math.Max(0m,_ownerInitialSpellPower -
            Warlock70425DamageModifiers.DemonicKnowledgeSpellPower(
                _request.Talents.Rank("demonic-knowledge"),_owner.Level,true)));
    private void SetOwnerDemonicKnowledge(decimal updatedSpellPower)
    {
        // Preserve the existing spellbook's coefficient math, but drop the
        // Demonic Knowledge contribution once the demon is lost. Update live
        // ability definitions, not actor-wide coefficients shared with pets.
        var spells=ForeverWarlockDpsSpellbook.Create(_request.Specialization,
            _request.Talents,_request.Tuning,updatedSpellPower,_ownerSpirit);
        foreach(var spell in spells)
        {
            if(!_owner.Abilities.TryGetValue(spell.Key,out var running))continue;
            foreach(var effect in spell.Effects)
            {
                var live=running.Definition.Effects.FirstOrDefault(e=>e.Key==effect.Key);
                if(live is null || effect.Key=="life-tap-health")continue;
                live.MinimumValue=effect.MinimumValue;
                live.MaximumValue=effect.MaximumValue;
            }
        }
    }
    private void Leave(decimal now,string reason)
    {
        if(_inactive)return;
        _inactive=true;
        _executor.TryCancelCurrentCast(_context,Actor.Key);
        RemovePetDependentSpellPower();
        if(Sacrificed && _family.Key is "voidwalker" or "felhunter")
            Schedule(_context,now+4m,"demon-sacrifice-regeneration");
        if(_family.Key=="infernal")
            _auras.RemoveAuras(_context,_target,"infernal-immolation",Actor.Key,"demon despawned");
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
            ev.SourceActorKey==Actor.Key)
        {
            switch(ev.EffectKey){
                case "demon-engage":Engage(now);break;
                case "demon-despawn":Leave(now,"demon-despawn");break;
                case "demon-replaced":Leave(now,"demon-replaced");break;
                case "demon-sacrifice":
                    if(_request.Talents.Rank("demonic-sacrifice")>0 &&
                        _family.Key is "imp" or "voidwalker" or "succubus" or "incubus" or "felhunter")
                    {
                        Sacrificed=true;Leave(now,"demon-sacrifice");
                        WarlockSacrificeRules.Apply(context,_auras,_owner,
                            _family.Key,"demonic-sacrifice");
                    }
                    break;
                case "demon-mana-regeneration-report":
                    if(!IsActive)break;
                    var manaResource=Actor.Resources["mana"];
                    manaResource.AdvanceTo(now);
                    var currentMana=manaResource.Current;
                    // The source resource is advanced by SimulationContext
                    // before events are dispatched. Reconstruct the credited
                    // amount, but never report more than the regeneration rate
                    // could produce in this sampling interval. This prevents
                    // queued spend events from inflating Imp gains by a whole
                    // Firebolt cost (180 Mana) at boundary timestamps.
                    var elapsed=Math.Max(0m,now-_lastManaSampleAt);
                    var available=Math.Max(0m,currentMana-_lastManaSample+_spentSinceSample);
                    var regen=Math.Min(available,manaResource.RegenerationPerSecond*elapsed);
                    _spentSinceSample=0m;
                    _lastManaSample=currentMana;
                    _lastManaSampleAt=now;
                    if(regen>0m)context.EmitEvent(new CombatEvent {
                        TimeSeconds=now,Type=CombatEventType.ResourceChanged,
                        SourceActorKey=Actor.Key,TargetActorKey=Actor.Key,
                        AbilityKey="demon-mana-regeneration",ResourceKey="mana",
                        Amount=regen,ResourceTotalAfter=currentMana
                    });
                    Schedule(context,now+1m,"demon-mana-regeneration-report");
                    break;
                case "demon-sacrifice-regeneration":
                    if(!Sacrificed || !_owner.ActiveAuras.Any(a=>
                        a.Definition.Key=="demonic-sacrifice-"+_family.Key &&
                        a.IsActiveAt(now)))break;
                    if(_family.Key=="voidwalker"){
                        var mana=_owner.Resources["mana"];
                        var gained=mana.Gain(mana.Maximum*.02m);
                        if(gained>0m)context.EmitEvent(new CombatEvent{
                            TimeSeconds=now,Type=CombatEventType.ResourceChanged,
                            SourceActorKey=_owner.Key,TargetActorKey=_owner.Key,
                            AbilityKey="demonic-sacrifice",ResourceKey="mana",Amount=gained,
                            ResourceTotalAfter=mana.Current
                        });
                    }
                    if(_family.Key=="felhunter"){
                        var healed=_owner.Heal(_owner.MaximumHealth*.03m);
                        if(healed>0m)context.EmitEvent(new CombatEvent{
                            TimeSeconds=now,Type=CombatEventType.Healing,
                            SourceActorKey=_owner.Key,TargetActorKey=_owner.Key,
                            AbilityKey="demonic-sacrifice",Amount=healed
                        });
                    }
                    Schedule(context,now+4m,"demon-sacrifice-regeneration");
                    break;
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
        // Count ability Mana spending when the completed cast reaches the
        // companion processor: AbilityExecutor has already deducted the cost.
        // Its ResourceChanged notification is queued separately and can arrive
        // AFTER the same-timestamp regeneration sampler. Tracking that later
        // event used to shift a 180-Mana Firebolt into the next interval.
        if(ev.Type==CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey is not null &&
            Actor.Abilities.TryGetValue(ev.AbilityKey,out var finishedAbility))
            _spentSinceSample+=finishedAbility.Definition.ResourceCosts
                .Where(c=>c.ResourceKey=="mana").Sum(c=>c.Amount);
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
            if(ev.AbilityKey=="demon-devour-magic" && _request.DemonTargetHasMagicAura)
                _auras.RemoveAuras(context,_target,"demon-test-magic-buff",_target.Key,"devoured");
            if(ev.AbilityKey=="demon-seduction")
                _auras.ApplyAura(context,_target,new AuraDefinition {
                    Key="demon-seduction",Name="Seduction",
                    DurationSeconds=15m*(1m+.10m*_request.Talents.Rank("improved-sayaad"))
                },Actor.Key,"demon-seduction","demon-control");
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
            ThreatOwnerActorKey=e.TargetActorKey,ThreatSourceActorKey=e.SourceActorKey,
            // Current tooltip confirms high Searing Pain threat. The 2x legacy
            // multiplier remains provisional until measured in the Forever client.
            Amount=e.Amount.Value*(e.SourceActorKey=="warlock" && e.AbilityKey=="searing-pain" ?
                2m * (1m - (ctx.GetActor("warlock")?.Stats.Get("demonic-brand-threat-reduction") ?? 0m)) :
                e.AbilityKey=="demonic-brand-shadow" &&
                    ctx.GetActor(e.SourceActorKey)?.Stats.Get("demonic-brand-tank-threat")>0m ? 2m : 1m)
        }];
    }
}
