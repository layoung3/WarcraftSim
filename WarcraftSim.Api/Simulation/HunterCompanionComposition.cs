using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Companions;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// A single pet composition contract for all Hunter specializations. Uses the
/// same ordinary actor, pet spellbook, auto-attack and priority engine as BM;
/// no parallel damage simulator or companion actor implementation.
/// </summary>
public static class HunterCompanionComposition
{
    public static SimulationActorState Create(SimulationContext context,
        SimulationActorState hunter,ForeverHunterPetFamily family,
        ForeverBeastMasteryHunterTuning tuning,int enduranceRank=0,int disciplineRank=0)
    {
        var (pet,_)=CompanionActorFactory.Create(context,hunter,new CompanionDefinition {
            Key="hunter-pet",Name=$"Hunter {family.Key}",OwnerKey=hunter.Key,
            FamilyKey=family.Key,
            BaseHealth=tuning.PetBaseHealth+tuning.HunterStamina*tuning.PetHealthPerOwnerStamina,
            BaseArmor=tuning.PetBaseArmor,BaseAttackPower=tuning.PetBaseAttackPower,
            OwnerAttackPowerCoefficient=tuning.PetOwnerAttackPowerInheritance,
            OwnerArmorCoefficient=tuning.PetOwnerArmorInheritance,
            FamilyArmorMultiplier=(1m+family.ArmorModifier)*(1m+.03m*enduranceRank),
            FamilyHealthMultiplier=(1m+family.HealthModifier)*(1m+.03m*enduranceRank),
            BaseResource=100m,ResourceKey="focus",
            ResourceRegenPerSecond=tuning.PetFocusPerSecond*(1m+.10m*disciplineRank)
        });
        pet.Stats.Set("hunter-owner-inherited-ap-snapshot",
            hunter.Stats.Get("companion-attack-power"));
        pet.Stats.Set("hunter-owner-inherited-armor-snapshot",
            hunter.Stats.Get("armor"));
        foreach(var ability in ForeverBeastMasteryHunterSpellbook.Pet(family,tuning))
        {
            if(ability.Key!="pet-prowl")ability.RequiredSourceAuraKeys.Add("pet-engaged");
            pet.AddAbility(ability);
        }
        return pet;
    }

    public static AutoAttackDefinition Melee(ForeverHunterPetFamily family,
        ForeverBeastMasteryHunterTuning tuning)=>new() {
        Key="pet-melee",Name=$"{family.Key} melee",
        SwingIntervalSeconds=tuning.PetSwingSeconds,
        DamageEffect=new AbilityEffectDefinition {
            Key="pet-melee-impact",EffectType=AbilityEffectTypes.DirectDamage,
            TargetType=AbilityTargetTypes.Enemy,SchoolKey="physical",
            MitigationType=DamageMitigationTypes.Armor,
            ResolutionType=CombatResolutionTypes.Melee,
            CanMiss=true,CanCrit=true,CanBeDodged=true,CanBeParried=true,
            MinimumValue=tuning.PetDamageMin,MaximumValue=tuning.PetDamageMax,
            ScalingStatKey="attack-power",ScalingCoefficient=tuning.PetSwingSeconds/14m
        }
    };

    public static RotationProfile Rotation(SimulationActorState pet,
        ForeverHunterPetFamily family,bool useSpecial=true,bool useBite=true,
        bool useClaw=true,IReadOnlyList<string>? customPriority=null)
    {
        var rotation=new RotationProfile {
            Name="Hunter companion autocast",ClassKey="pet",
            RulesetKey="wow-forever",SimulationType=SimulationType.Dps
        };
        var special="pet-"+family.SpecialAbility;
        var reserve=useSpecial && family.SpecialAbility!="prowl" &&
            pet.Abilities.TryGetValue(special,out var state) &&
            state.Definition.Effects.Any(e=>e.EffectType is
                (AbilityEffectTypes.DirectDamage or AbilityEffectTypes.PeriodicDamage));
        var order=0;
        void Add(string key,bool filler=false)
        {
            if(!pet.Abilities.ContainsKey(key))return;
            var entry=new RotationEntry{Priority=++order,AbilityKey=key};
            if(filler && reserve)
                entry.Conditions.Add(new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.SourceAbilityOnCooldown,Key=special
                });
            rotation.Entries.Add(entry);
        }
        if(customPriority is {Count:>0})
        {
            if(customPriority.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=customPriority.Count)
                throw new ArgumentException("Pet autocast priority contains a repeated ability.");
            foreach(var key in customPriority)
            {
                var enabled=key switch {
                    "pet-bite" => useBite,
                    "pet-claw" => useClaw,
                    _ => useSpecial && key==special && family.SpecialAbility!="prowl"
                };
                if(!enabled || !pet.Abilities.ContainsKey(key))
                    throw new ArgumentException($"Invalid pet autocast for {family.Key}: {key}");
                Add(key); // deliberate override may choose cheap fillers first.
            }
        }
        else
        {
            if(useSpecial && family.SpecialAbility!="prowl")Add(special);
            if(useBite)Add("pet-bite",true);
            if(useClaw)Add("pet-claw",true);
        }
        return rotation;
    }

    /// <summary>Update inherited AP/armor against current owner effective stats.
    /// Apply deltas, preserving independent pet buffs (Wolf Howl, Frenzy etc).
    /// This is also invoked before pet rolls so equal-timestamp owner stat
    /// changes are visible to the very next companion attack.</summary>
    public static void RefreshOwnerStats(SimulationContext context,
        SimulationActorState pet,ForeverBeastMasteryHunterTuning tuning)
    {
        var owner=context.GetActor("hunter");
        if(owner is null)return;
        var current=owner.Stats.Get("companion-attack-power");
        var previous=pet.Stats.Get("hunter-owner-inherited-ap-snapshot");
        if(current!=previous)
        {
            pet.Stats.Set("attack-power",Math.Max(0m,
                pet.Stats.Get("attack-power")+
                    (current-previous)*tuning.PetOwnerAttackPowerInheritance));
            pet.Stats.Set("hunter-owner-inherited-ap-snapshot",current);
        }
        var armor=owner.Stats.Get("armor");
        var armorBefore=pet.Stats.Get("hunter-owner-inherited-armor-snapshot");
        if(armor!=armorBefore)
        {
            pet.Stats.Set("armor",Math.Max(0m,
                pet.Stats.Get("armor")+
                    (armor-armorBefore)*tuning.PetOwnerArmorInheritance));
            pet.Stats.Set("hunter-owner-inherited-armor-snapshot",armor);
        }
    }

    /// <summary>Uses identical Hunter pet bonus-hit, Expertise and crit rules for
    /// MM, Survival and BM; never treats a gear hit bonus as final hit chance.</summary>
    public static CombatRollResult Resolve(SimulationContext context,
        SimulationActorState pet,SimulationActorState target,
        AbilityEffectDefinition effect,ForeverBeastMasteryHunterTuning tuning,
        int ferocityRank=0)
    {
        RefreshOwnerStats(context,pet,tuning);
        var owner=context.GetActor("hunter");
        var hitBonus=owner?.Stats.Get("ranged-hit-bonus-percent") ??
            tuning.HunterRangedHitBonusPercent;
        var magic=effect.ResolutionType==CombatResolutionTypes.Spell;
        var hit=tuning.PetHitPercent ??
            ForeverCompanionHitInheritance.FinalHit(
                magic?ForeverCompanionHitInheritance.SpellBaseHit(pet.Level,target.Level):
                    ForeverCompanionHitInheritance.CreatureMeleeBaseHit(pet.Level,target.Level),
                magic?ForeverCompanionHitInheritance.HunterSpellBonus(hitBonus):
                    ForeverCompanionHitInheritance.HunterMeleeBonus(hitBonus));
        if(effect.CanMiss && (decimal)context.Random.NextDouble()*100m>=hit)
            return CombatRollResult.Miss();
        var expertiseDodge=owner?.Stats.Get(ForeverCombatStatKeys.DodgeReductionPercent) ?? 0m;
        var expertiseParry=owner?.Stats.Get(ForeverCombatStatKeys.ParryReductionPercent) ?? 0m;
        if(effect.CanBeDodged && (decimal)context.Random.NextDouble()*100m<
            Math.Max(0m,tuning.PetAvoidancePercent-expertiseDodge))
            return CombatRollResult.Dodge();
        if(effect.CanBeParried && (decimal)context.Random.NextDouble()*100m<
            Math.Max(0m,tuning.PetAvoidancePercent-expertiseParry))
            return CombatRollResult.Parry();
        var crit=Math.Max(owner?.Stats.Get("ranged-critical-chance-percent") ??
                tuning.HunterCriticalPercent,
            owner?.Stats.Get("melee-critical-chance-percent") ??
                tuning.HunterMeleeCriticalPercent)+tuning.PetCriticalBonusPercent+
            2m*ferocityRank+
            (owner?.Stats.Get("hunter-pet-intimidation-armed") is >0m ? 100m : 0m);
        if(effect.CanCrit && (decimal)context.Random.NextDouble()*100m<Math.Clamp(crit,0m,100m))
            return CombatRollResult.Critical(2m);
        return CombatRollResult.Hit();
    }
}

/// <summary>
/// Pet lifecycle and family utility for pet-enabled Marksmanship/Survival.
/// BM retains its additional spec-specific Bestial Wrath/Frenzy/Hawk controller
/// but shares the above actor/ability/rotation composition. All attacks and
/// periodic effects still execute through the existing engine processors.
/// </summary>
public sealed class HunterSharedCompanionRuntime(
    SimulationActorState hunter,SimulationActorState pet,SimulationActorState boss,
    ForeverHunterPetFamily family,ForeverBeastMasteryHunterTuning tuning,
    AuraManager auras,AutoAttackProcessor autos,AutoAttackDefinition melee,
    decimal duration,bool useSpecial=true,decimal engageDelay=.75m,
    decimal incomingDamage=0m,decimal incomingInterval=3m,bool revive=false,
    int improvedReviveRank=0,int frenzyRank=0,int spiritBondRank=0)
    : ICombatEventProcessor
{
    private decimal _engagedAt=-1m,_uptime;
    private decimal _focusLast=100m,_focusSpent;
    private decimal _wolfHowlEnd,_frenzyEnd;
    private bool _wolfHowl;
    public decimal UptimeSeconds=>_uptime+(_engagedAt>=0m?Math.Max(0m,duration-_engagedAt):0m);
    public bool Died{get;private set;}
    public int FamilySpecialCasts{get;private set;}
    public SimulationActorState Pet=>pet;

    private void Schedule(SimulationContext ctx,decimal at,string effect)
    {
        if(at>=duration||at<0m)return;
        ctx.ScheduleEvent(new CombatEvent{
            TimeSeconds=at,Type=CombatEventType.RotationDecision,
            SourceActorKey=hunter.Key,TargetActorKey=boss.Key,
            EffectKey=effect,IsInternal=true
        });
    }
    private void Aura(SimulationContext ctx,SimulationActorState actor,string key,decimal seconds)
    {
        auras.ApplyAura(ctx,actor,new AuraDefinition {
            Key=key,Name=key,DurationSeconds=seconds
        },hunter.Key,key,"hunter-companion");
    }
    private void End(SimulationContext ctx,decimal now)
    {
        if(_engagedAt>=0m)_uptime+=now-_engagedAt;
        _engagedAt=-1m;
        auras.RemoveAuras(ctx,pet,"pet-engaged",hunter.Key,"pet-inactive");
        auras.RemoveAuras(ctx,hunter,"hunter-pet-active",hunter.Key,"pet-inactive");
        autos.Stop(ctx,pet.Key,"pet-melee");
        if(_wolfHowl)
        {
            pet.Stats.Set("attack-power",
                Math.Max(0m,pet.Stats.Get("attack-power")-tuning.WolfHowlMeleeAttackPowerBonus));
            _wolfHowl=false;
        }
    }
    public void Start(SimulationContext ctx,AbilityExecutor executor)
    {
        if(spiritBondRank>0)
            Schedule(ctx,spiritBondRank==2?5m:10m,"shared-spirit-bond");
        if(useSpecial && family.SpecialAbility=="prowl")
            executor.TryStartAbility(ctx,pet.Key,boss.Key,"pet-prowl");
        Schedule(ctx,engageDelay,"shared-pet-engage");
        Schedule(ctx,Math.Max(1m,engageDelay),"shared-pet-focus");
        if(incomingDamage>0m)Schedule(ctx,engageDelay+incomingInterval,"shared-pet-hit");
    }
    public void Process(SimulationContext ctx,CombatEvent ev)
    {
        HunterCompanionComposition.RefreshOwnerStats(ctx,pet,tuning);
        var now=ev.TimeSeconds;
        if(ev.SourceActorKey==pet.Key &&
           ev.Type==CombatEventType.ResourceChanged &&
           ev.ResourceKey=="focus" && ev.Amount is <0m)_focusSpent-=ev.Amount.Value;
        if(ev.Type==CombatEventType.AbilityCastCompleted &&
           ev.SourceActorKey==hunter.Key && pet.IsAlive && _engagedAt>=0m)
        {
            if(ev.AbilityKey=="bestial-wrath")
                Aura(ctx,pet,"bestial-wrath",18m);
            if(ev.AbilityKey=="intimidation")
                hunter.Stats.Set("hunter-pet-intimidation-armed",1m);
        }
        if(ev.SourceActorKey==pet.Key && ev.Type==CombatEventType.Damage &&
           ev.Amount is >0m && ev.AbilityKey?.StartsWith("pet-",StringComparison.Ordinal)==true)
        {
            if(ev.IsCritical && frenzyRank>0 &&
               (decimal)ctx.Random.NextDouble()<.20m*frenzyRank)
            {
                if(now>=_frenzyEnd)
                {
                    var old=pet.Stats.Get("haste-percent");
                    pet.Stats.Set("haste-percent",old+30m);
                    autos.RescheduleForHasteChange(ctx,pet.Key,old,old+30m);
                }
                _frenzyEnd=now+8m;
                Aura(ctx,pet,"pet-frenzy",8m);
                Schedule(ctx,_frenzyEnd,"shared-frenzy-expire");
            }
            if(hunter.Stats.Get("hunter-pet-intimidation-armed")>0m)
            {
                hunter.Stats.Set("hunter-pet-intimidation-armed",0m);
                Aura(ctx,boss,"hunter-intimidation-stun",3m);
                ctx.EmitEvent(new CombatEvent {
                    TimeSeconds=now,Type=CombatEventType.ThreatChanged,
                    SourceActorKey=pet.Key,TargetActorKey=boss.Key,
                    AbilityKey="intimidation",Amount=ev.Amount.Value,
                    Description="Bonus pet Intimidation threat."
                });
            }
        }
        if(ev.SourceActorKey==pet.Key &&
           ev.Type==CombatEventType.AbilityCastCompleted &&
           ev.AbilityKey=="pet-"+family.SpecialAbility)
        {
            FamilySpecialCasts++;
            var special=family.SpecialAbility;
            if(special=="furious-howl")
            {
                if(!_wolfHowl)
                {
                    pet.Stats.Set("attack-power",
                        pet.Stats.Get("attack-power")+tuning.WolfHowlMeleeAttackPowerBonus);
                    _wolfHowl=true;
                }
                _wolfHowlEnd=now+60m;
                Aura(ctx,pet,"wolf-furious-howl",60m);
                Schedule(ctx,_wolfHowlEnd,"shared-wolf-expire");
            }
            if(special=="tricksters-dance")
            {
                var old=pet.Stats.Get("haste-percent");
                pet.Stats.Set("haste-percent",old+30m);
                autos.RescheduleForHasteChange(ctx,pet.Key,old,old+30m);
                Aura(ctx,pet,"pet-tricksters-dance-buff",12m);
                Schedule(ctx,now+12m,"shared-fox-expire");
            }
            if(special=="prowl")Aura(ctx,pet,"pet-prowl-buff",duration);
            if(special=="shell-shield")Aura(ctx,pet,"pet-shell-shield-buff",8m);
            if(special is "dismember" or "pinch" or "savage-rend" or "scorpid-poison" or
                "tendon-rip" or "web" or "demoralizing-screech" or "sonic-blast" or "dust-cloud")
                Aura(ctx,boss,"pet-"+special+"-debuff",special=="dismember"?6m:5m);
        }
        if(ev.SourceActorKey==pet.Key && ev.Type==CombatEventType.Damage &&
           ev.AbilityKey?.StartsWith("pet-",StringComparison.Ordinal)==true &&
           pet.ActiveAuras.Any(a=>a.Definition.Key=="pet-prowl-buff" &&
               a.IsActiveAt(now)))
            auras.RemoveAuras(ctx,pet,"pet-prowl-buff",hunter.Key,"attack broke stealth");
        if(ev.Type==CombatEventType.ActorDied && ev.TargetActorKey==pet.Key)
        {
            if(_engagedAt>=0m)End(ctx,now);
            Died=true;
            if(revive)Aura(ctx,hunter,"pet-needs-revive",30m);
        }
        if(ev.Type==CombatEventType.AbilityCastCompleted &&
           ev.SourceActorKey==hunter.Key && ev.AbilityKey=="revive-pet" &&
           !pet.IsAlive)
        {
            pet.InitializeHealth(pet.MaximumHealth,pet.MaximumHealth*
                (.40m+.15m*improvedReviveRank));
            pet.Resources["focus"].SetCurrent(100m);
            Died=false;
            auras.RemoveAuras(ctx,hunter,"pet-needs-revive",hunter.Key,"revived");
            Schedule(ctx,now+.5m,"shared-pet-engage");
            if(incomingDamage>0m)Schedule(ctx,now+.5m+incomingInterval,"shared-pet-hit");
        }
        if(ev.Type!=CombatEventType.RotationDecision || !ev.IsInternal ||
           ev.SourceActorKey!=hunter.Key)return;
        switch(ev.EffectKey)
        {
            case "shared-pet-engage":
                if(!pet.IsAlive||_engagedAt>=0m)break;
                _engagedAt=now;
                Aura(ctx,pet,"pet-engaged",duration-now);
                Aura(ctx,hunter,"hunter-pet-active",duration-now);
                autos.Start(ctx,pet.Key,boss.Key,melee,0m);
                break;
            case "shared-pet-focus":
                if(pet.IsAlive)
                {
                    pet.Resources["focus"].AdvanceTo(now);
                    var value=pet.Resources["focus"].Current;
                    var gained=Math.Max(0m,value-_focusLast+_focusSpent);
                    _focusLast=value;_focusSpent=0m;
                    if(gained>0m)ctx.EmitEvent(new CombatEvent {
                        TimeSeconds=now,Type=CombatEventType.ResourceChanged,
                        SourceActorKey=pet.Key,TargetActorKey=pet.Key,
                        AbilityKey="pet-focus-regeneration",ResourceKey="focus",
                        Amount=gained,ResourceTotalAfter=value
                    });
                }
                Schedule(ctx,now+1m,"shared-pet-focus");
                break;
            case "shared-pet-hit":
                if(!pet.IsAlive||_engagedAt<0m)break;
                var armor=pet.Stats.Get("armor");
                var amount=incomingDamage*(1m-armor/(armor+400m+85m*63m));
                if(pet.ActiveAuras.Any(a=>a.Definition.Key=="pet-shell-shield-buff" &&
                    a.IsActiveAt(now)))amount*=.5m;
                if(pet.ActiveAuras.Any(a=>a.Definition.Key=="pet-tricksters-dance-buff" &&
                    a.IsActiveAt(now)) && (decimal)ctx.Random.NextDouble()<.5m)
                {
                    Schedule(ctx,now+incomingInterval,"shared-pet-hit");
                    break;
                }
                var applied=pet.TakeDamage(amount);
                if(applied>0m)ctx.EmitEvent(new CombatEvent {
                    TimeSeconds=now,Type=CombatEventType.Damage,
                    SourceActorKey=boss.Key,TargetActorKey=pet.Key,
                    AbilityKey="scripted-pet-incoming-hit",SchoolKey="physical",
                    ResultKey="hit",Amount=applied
                });
                if(!pet.IsAlive)
                    ctx.EmitEvent(new CombatEvent {
                        TimeSeconds=now,Type=CombatEventType.ActorDied,
                        SourceActorKey=boss.Key,TargetActorKey=pet.Key,
                        AbilityKey="scripted-pet-incoming-hit"
                    });
                else Schedule(ctx,now+incomingInterval,"shared-pet-hit");
                break;
            case "shared-spirit-bond":
                if(pet.IsAlive && _engagedAt>=0m)
                {
                    foreach(var actor in new[]{hunter,pet})
                    {
                        var healed=actor.Heal(actor.MaximumHealth*.01m);
                        if(healed>0m)ctx.EmitEvent(new CombatEvent {
                            TimeSeconds=now,Type=CombatEventType.Healing,
                            SourceActorKey=hunter.Key,TargetActorKey=actor.Key,
                            AbilityKey="spirit-bond",Amount=healed
                        });
                    }
                }
                Schedule(ctx,now+(spiritBondRank==2?5m:10m),"shared-spirit-bond");
                break;
            case "shared-frenzy-expire":
                if(now>=_frenzyEnd && pet.IsAlive)
                {
                    var old=pet.Stats.Get("haste-percent");
                    var next=Math.Max(0m,old-30m);
                    pet.Stats.Set("haste-percent",next);
                    autos.RescheduleForHasteChange(ctx,pet.Key,old,next);
                }
                break;
            case "shared-wolf-expire":
                if(now>=_wolfHowlEnd && _wolfHowl)
                {
                    pet.Stats.Set("attack-power",Math.Max(0m,
                        pet.Stats.Get("attack-power")-tuning.WolfHowlMeleeAttackPowerBonus));
                    _wolfHowl=false;
                }
                break;
            case "shared-fox-expire":
                if(pet.IsAlive)
                {
                    var old=pet.Stats.Get("haste-percent");
                    var next=Math.Max(0m,old-30m);
                    pet.Stats.Set("haste-percent",next);
                    autos.RescheduleForHasteChange(ctx,pet.Key,old,next);
                }
                break;
        }
    }
}

/// <summary>One damage threat resolver for both owner and independent pet.
/// Does not double count self hits, pet healing, or target-side damage.</summary>
public sealed class HunterOwnerAndPetThreatResolver : IThreatGenerationResolver
{
    public IReadOnlyList<ThreatGenerationContribution> Resolve(
        SimulationContext ctx,CombatEvent ev)
    {
        if(ev.Type!=CombatEventType.Damage||ev.Amount is not >0m ||
           ev.SourceActorKey is not ("hunter" or "hunter-pet") ||
           ev.TargetActorKey is not { } target ||
           ctx.GetActor(target)?.TeamKey!="enemy")return [];
        return [new ThreatGenerationContribution {
            ThreatOwnerActorKey=target,ThreatSourceActorKey=ev.SourceActorKey,
            Amount=ev.Amount.Value
        }];
    }
}
