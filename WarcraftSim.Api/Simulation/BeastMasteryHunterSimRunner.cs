using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Companions;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP163: One engine context, independent Hunter / pet / hawk actors.
/// Family tuning is separate from generic companion registration.</summary>
public sealed class BeastMasteryHunterSimRunner
{
    public BeastMasteryHunterSimResponse Run(BeastMasteryHunterSimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if(request.Talents is null || request.Tuning is null ||
           request.DurationSeconds is <=0m or >1200m ||
           request.TargetMaximumHealth<=0m || request.AdditionalTargets is <0 or >8 ||
           request.PetUptimePercent is <0m or >100m || request.PetEngagementDelaySeconds<0m ||
           request.PetIncomingHitDamage<0m || request.PetIncomingHitEverySeconds<=0m)
            throw new ArgumentException("Invalid Beast Mastery encounter options.");
        request.Talents.Validate();request.Tuning.Validate();
        var talent=request.Talents; var t=request.Tuning;
        var family=ForeverHunterPetFamilies.Get(request.PetFamily);
        var hunterStats=new StatCollection();
        hunterStats.Set("attack-power",t.RangedAttackPower);
        hunterStats.Set("companion-attack-power",Math.Max(t.RangedAttackPower,t.MeleeAttackPower));
        hunterStats.Set("armor",t.HunterArmor);
        hunterStats.Set("haste-percent",0m);
        var ctx=new SimulationContext(new SimulationRunOptions {
            Seed=request.Seed,DurationSeconds=request.DurationSeconds,PrimaryActorKey="hunter",
            CaptureTimeline=request.CaptureTimeline,MaximumProcessedEvents=90000,
            CancellationToken=cancellationToken
        });
        var hunter=new SimulationActorState {
            Key="hunter",Name="Beast Mastery Hunter",TeamKey="raid",Level=60,
            MaximumHealth=t.HunterHealth,Stats=hunterStats
        };
        hunter.InitializeHealth(t.HunterHealth);
        hunter.AddResource(new ResourceState("mana",t.HunterMana,t.HunterMana));
        var boss=new SimulationActorState {
            Key="target",Name="Level 63 training boss",TeamKey="enemy",Level=63,
            MaximumHealth=request.TargetMaximumHealth
        };
        boss.InitializeHealth(boss.MaximumHealth);
        ctx.AddActor(hunter);ctx.AddActor(boss);
        for(int i=1;i<=request.AdditionalTargets;i++){
            var add=new SimulationActorState {
                Key=$"add-{i}",Name=$"Additional enemy {i}",TeamKey="enemy",Level=63,
                MaximumHealth=request.TargetMaximumHealth
            };
            add.InitializeHealth(add.MaximumHealth);ctx.AddActor(add);
        }

        SimulationActorState? pet=null;
        if(request.PetEnabled){
            var (actor,_) =CompanionActorFactory.Create(ctx,hunter,new CompanionDefinition {
                Key="hunter-pet",Name=$"Hunter {family.Key}",OwnerKey=hunter.Key,
                FamilyKey=family.Key,
                BaseHealth=t.PetBaseHealth+t.HunterStamina*t.PetHealthPerOwnerStamina,
                BaseArmor=t.PetBaseArmor,BaseAttackPower=t.PetBaseAttackPower,
                OwnerAttackPowerCoefficient=t.PetOwnerAttackPowerInheritance,
                OwnerArmorCoefficient=t.PetOwnerArmorInheritance,
                FamilyArmorMultiplier=(1m+family.ArmorModifier)*(1m+.03m*talent.Rank("endurance-training")),
                FamilyHealthMultiplier=(1m+family.HealthModifier)*(1m+.03m*talent.Rank("endurance-training")),
                BaseResource=100m,ResourceKey="focus",
                ResourceRegenPerSecond=t.PetFocusPerSecond * (1m+.10m*talent.Rank("bestial-discipline"))
            });
            pet=actor;
            foreach(var skill in ForeverBeastMasteryHunterSpellbook.Pet(family,t)){
                if (skill.Key != "pet-prowl")
                    skill.RequiredSourceAuraKeys.Add("pet-engaged");
                pet.AddAbility(skill);
            }
        }
        foreach(var spell in ForeverBeastMasteryHunterSpellbook.Hunter(talent,t))hunter.AddAbility(spell);
        var hawks=new List<SimulationActorState>();
        if(talent.Rank("summon-hawk")>0 && request.UseSummonHawk){
            for(int i=0;i<2;i++){
                var (bird,_) = CompanionActorFactory.Create(ctx,hunter,new CompanionDefinition {
                    Key=$"hawk-{i+1}",Name=$"Summoned Hawk {i+1}",OwnerKey=hunter.Key,
                    FamilyKey="summoned-hawk",BaseHealth=450m,BaseAttackPower=0m,
                    BaseArmor=0m,BaseResource=0m,ResourceRegenPerSecond=0m,
                    OwnerAttackPowerCoefficient=.05m
                });
                hawks.Add(bird);
            }
        }

        var aura=new AuraManager();
        var state=new BeastState(request,ctx,hunter,pet,hawks,boss,family,aura,talent,t);
        var executor=new AbilityExecutor(aura,new BeastRolls(request,state),new BeastMitigation(t,state));
        var auto=new AutoAttackProcessor(executor,new ForeverAutoAttackTimingProvider());
        state.AttachAuto(auto);
        state.AttachExecutor(executor);
        var rangedAttack=new AutoAttackDefinition{
            Key="auto-shot",Name="Auto Shot",SwingIntervalSeconds=t.HunterWeaponSpeed,
            DamageEffect=new AbilityEffectDefinition {
                Key="ranged-impact",EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="physical",
                ResolutionType=CombatResolutionTypes.Melee,MitigationType=DamageMitigationTypes.Armor,
                CanMiss=true,CanCrit=true,MinimumValue=t.HunterWeaponMin,MaximumValue=t.HunterWeaponMax,
                ScalingStatKey="attack-power",ScalingCoefficient=t.HunterWeaponSpeed/14m
            }
        };
        AutoAttackDefinition? petAttack=null;
        if(pet is not null){
            petAttack=new AutoAttackDefinition{
                Key="pet-melee",Name=$"{family.Key} melee",SwingIntervalSeconds=t.PetSwingSeconds,
                DamageMultiplier=1m,
                DamageEffect=new AbilityEffectDefinition{
                    Key="pet-melee-impact",EffectType=AbilityEffectTypes.DirectDamage,
                    TargetType=AbilityTargetTypes.Enemy,SchoolKey="physical",
                    MitigationType=DamageMitigationTypes.Armor,
                    ResolutionType=CombatResolutionTypes.Melee,CanMiss=true,CanCrit=true,
                    CanBeDodged=true,CanBeParried=true,
                    MinimumValue=t.PetDamageMin,MaximumValue=t.PetDamageMax,
                    ScalingStatKey="attack-power",ScalingCoefficient=t.PetSwingSeconds/14m
                }
            };
            state.AttachPetAuto(petAttack);
        }
        state.AttachHawkAuto(new AutoAttackDefinition{
            Key="hawk-auto",Name="Hawk Assault",SwingIntervalSeconds=t.HawkAttackInterval,
            DamageEffect=new AbilityEffectDefinition{
                Key="hawk-impact",EffectType=AbilityEffectTypes.DirectDamage,
                TargetType=AbilityTargetTypes.Enemy,SchoolKey="physical",
                MitigationType=DamageMitigationTypes.Armor,ResolutionType=CombatResolutionTypes.Melee,
                CanMiss=true,CanCrit=true,MinimumValue=t.HawkAttackDamage,
                MaximumValue=t.HawkAttackDamage,ScalingStatKey="attack-power",
                ScalingCoefficient=t.HawkAttackInterval/14m
            }
        });

        var rotations=new List<ICombatEventProcessor>();
        rotations.Add(new PriorityRotationExecutor(HunterRotation(request,hunter),hunter.Key,boss.Key,executor,
            reactToTargetStateChanges:true,autoAttackProcessor:auto));
        if(pet is not null){
            rotations.Add(new PriorityRotationExecutor(PetRotation(request,pet,family),pet.Key,boss.Key,executor,
                reactToTargetStateChanges:true,autoAttackProcessor:auto));
        }
        var regen=new FiveSecondManaRegenerationProcessor(hunter.Key,t.HunterSpiritRegenPerSecond,
            t.HunterMp5,talent.Rank("bestial-discipline")*.25m);
        var processors=new List<ICombatEventProcessor> { executor,aura,auto,state,regen,
            new ThreatManager(new CompanionThreat()) };
        processors.AddRange(rotations);
        var engine=new SimulationEngine(processors);
        regen.Initialize(ctx);
        var result=engine.Run(ctx,started=>{
            // Cats enter the encounter stealthed if Prowl is enabled. The
            // executor owns its Focus cost, cooldown and cast reporting.
            if (pet is not null && request.UsePetSpecial && family.SpecialAbility == "prowl")
                executor.TryStartAbility(started, pet.Key, boss.Key, "pet-prowl");
            auto.Start(started,hunter.Key,boss.Key,rangedAttack,.5m);
            if(pet is not null){
                var engage=request.PetEngagementDelaySeconds /
                    (talent.Rank("bestial-swiftness")>0 ? 1.3m:1m);
                state.Schedule(started,engage,"pet-engage");
                state.Schedule(started,Math.Max(1m,engage),"pet-focus-report");
                if(request.PetIncomingHitDamage>0m)
                    state.Schedule(started,engage+request.PetIncomingHitEverySeconds,"pet-incoming-hit");
                if(request.PetUptimePercent<100m)
                    state.Schedule(started,engage+(request.DurationSeconds-engage)*request.PetUptimePercent/100m,
                        "pet-disengage");
            }
        });
        var hunterDamage=result.Summary.ActorSummaries[hunter.Key].DamageDone;
        var petReport=pet is null?null:result.Summary.ActorSummaries[pet.Key];
        var petDamage=petReport?.DamageDone??0m;
        var hawkDamage=hawks.Sum(x=>result.Summary.ActorSummaries[x.Key].DamageDone);
        var petAuto=petReport?.DamageDoneByAbility.GetValueOrDefault("pet-melee")??0m;
        var combined=hunterDamage+petDamage+hawkDamage;
        var breakdown=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
        foreach(var actor in new[]{hunter,pet}.Where(x=>x is not null).Cast<SimulationActorState>().Concat(hawks))
            foreach(var pair in result.Summary.ActorSummaries[actor.Key].DamageDoneByAbility)
                breakdown[actor.Key+":"+pair.Key]=pair.Value;
        result.CustomMetrics["beast-pet-damage"]=petDamage;
        result.CustomMetrics["beast-hawk-damage"]=hawkDamage;
        result.CustomMetrics["beast-owner-damage"]=hunterDamage;
        result.CustomMetrics["beast-pet-uptime-seconds"]=state.PetActiveSeconds;
        result.CustomMetrics["beast-frenzy-procs"]=state.FrenzyProcs;
        result.CustomMetrics["beast-family-special-casts"]=state.FamilySpecialCasts;
        result.CustomMetrics["beast-hawk-summons"]=state.HawkSummons;
        return new BeastMasteryHunterSimResponse{
            PetFamily=family.Key,HunterDamage=hunterDamage,PetDamage=petDamage,HawkDamage=hawkDamage,
            DamageDone=combined,Dps=combined/request.DurationSeconds,PetDps=petDamage/request.DurationSeconds,
            PetAutoDamage=petAuto,PetSpecialDamage=petDamage-petAuto,
            PetThreat=boss.ThreatTable.GetThreat(pet?.Key??"missing"),
            HunterThreat=boss.ThreatTable.GetThreat(hunter.Key),
            PetFocus=pet?.Resources["focus"].Current??0m,EndingMana=hunter.Resources["mana"].Current,
            PetActiveSeconds=state.PetActiveSeconds,PetInactiveSeconds=request.DurationSeconds-state.PetActiveSeconds,
            PetDied=pet is not null&&!pet.IsAlive,PetCriticalHits=state.PetCrits,PetMisses=state.PetMisses,
            DamageByAbility=breakdown,Run=result,
            ModelingNotes=[
                "CP163 full-actor Hunter + companion + two temporary hawk actors; all share one combat engine.",
                "Family percent modifiers and Forever talent descriptions are sourced from the October 2026 beta.",
                "Pet base weapon damage, focus regeneration, family special damage, hawk swing and threat multipliers remain configurable.",
                "Core Hound is in client family data, but player tameability is unconfirmed.",
                "Pet movement is engagement delay/uptime only, and incoming pet damage is optional scripted testing.",
                "Utility signatures apply simulation auras; full boss CC/defensive AI requires a later encounter model."
            ]
        };
    }

    private static RotationProfile HunterRotation(BeastMasteryHunterSimRequest req,SimulationActorState actor)
    {
        var rotation=new RotationProfile { Name="Forever Beast Mastery",ClassKey="hunter",
            RulesetKey="wow-forever",SimulationType=SimulationType.Dps };
        int n=0;
        void add(bool active,string key,params RotationConditionDefinition[] conditions){
            if(active&&actor.Abilities.ContainsKey(key))rotation.Entries.Add(new RotationEntry{
                Priority=++n,AbilityKey=key,Conditions=conditions.ToList()});
        }
        add(req.UseRevivePet && req.PetEnabled,"revive-pet");
        add(req.UseMendPet && req.PetEnabled,"mend-pet");
        add(req.UseRapidFire,"rapid-fire");
        add(req.UseBestialWrath && req.PetEnabled,"bestial-wrath");
        add(req.UseIntimidation && req.PetEnabled,"intimidation");
        add(req.UseSummonHawk,"summon-hawk");
        add(req.UseSerpentSting,"serpent-sting",new RotationConditionDefinition{
            ConditionType=RotationConditionTypes.TargetAuraMissing,Key="serpent-sting"});
        add(req.UseMultiShot&&req.AdditionalTargets>0,"multi-shot");
        add(req.UseAimedShot,"aimed-shot");
        add(req.UseArcaneShot,"arcane-shot");
        return rotation;
    }
    private static RotationProfile PetRotation(BeastMasteryHunterSimRequest req,
        SimulationActorState pet,ForeverHunterPetFamily family)
    {
        var rotation=new RotationProfile{Name="Hunter companion autocast",ClassKey="pet",
            RulesetKey="wow-forever",SimulationType=SimulationType.Dps};
        int p=0;
        void add(bool yes,string key){if(yes && pet.Abilities.ContainsKey(key))
            rotation.Entries.Add(new RotationEntry{Priority=++p,AbilityKey=key});}
        // Prowl is a pre-combat stealth ability, not a repeating autocast.
        add(req.UsePetSpecial && family.SpecialAbility != "prowl",
            "pet-"+family.SpecialAbility);
        add(req.UsePetBite,"pet-bite");
        add(req.UsePetClaw,"pet-claw");
        return rotation;
    }

    private sealed class CompanionThreat : IThreatGenerationResolver
    {
        public IReadOnlyList<ThreatGenerationContribution> Resolve(SimulationContext context, CombatEvent ev)
        {
            if(ev.Type!=CombatEventType.Damage || ev.Amount is not >0m || ev.SourceActorKey is null ||
               ev.TargetActorKey is null ||
               context.GetActor(ev.SourceActorKey)?.TeamKey!="raid" ||
               context.GetActor(ev.TargetActorKey)?.TeamKey!="enemy") return [];
            return [new ThreatGenerationContribution {
                ThreatOwnerActorKey=ev.TargetActorKey,ThreatSourceActorKey=ev.SourceActorKey,
                Amount=ev.Amount.Value
            }];
        }
    }
    private sealed class BeastRolls(BeastMasteryHunterSimRequest req,BeastState state) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext ctx,SimulationActorState source,
            SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect) =>
            Resolve(ctx,source,target,ability,effect,CombatEffectDeliveryType.Direct);
        public CombatRollResult Resolve(SimulationContext ctx,SimulationActorState source,
            SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,
            CombatEffectDeliveryType deliveryType)
        {
            var pet=source.Key=="hunter-pet";
            var hawk=source.Key.StartsWith("hawk-",StringComparison.Ordinal);
            var hit=pet||hawk?req.Tuning.PetHitPercent:req.Tuning.HunterHitPercent;
            if(effect.CanMiss && (decimal)ctx.Random.NextDouble()*100m >= hit)return CombatRollResult.Miss();
            if(pet && effect.CanBeDodged && (decimal)ctx.Random.NextDouble()*100m <req.Tuning.PetAvoidancePercent)
                return CombatRollResult.Dodge();
            if(pet && effect.CanBeParried && (decimal)ctx.Random.NextDouble()*100m <req.Tuning.PetAvoidancePercent)
                return CombatRollResult.Parry();
            var crit=req.Tuning.HunterCriticalPercent+req.Talents.Rank("lethal-attacks");
            if(pet||hawk)crit=Math.Max(req.Tuning.HunterCriticalPercent,
                req.Tuning.HunterMeleeCriticalPercent)+req.Tuning.PetCriticalBonusPercent+
                2m*req.Talents.Rank("ferocity");
            if(pet&&state.IntimidationArmed)crit+=100m;
            if(effect.CanCrit&&(decimal)ctx.Random.NextDouble()*100m<crit)
                return CombatRollResult.Critical(2m);
            return CombatRollResult.Hit();
        }
    }
    private sealed class BeastMitigation(ForeverBeastMasteryHunterTuning t,BeastState state) : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext ctx,SimulationActorState source,
            SimulationActorState target,AbilityDefinition ability,AbilityEffectDefinition effect,decimal rawAmount)
        {
            rawAmount*=state.DamageMultiplier(source.Key,ctx.CurrentTimeSeconds);
            if(effect.MitigationType!=DamageMitigationTypes.Armor)
                return DamageMitigationResult.Unmitigated(rawAmount);
            var reduction=t.TargetArmor/(t.TargetArmor+400m+85m*Math.Max(1,source.Level));
            return new DamageMitigationResult{RawAmount=rawAmount,FinalAmount=rawAmount*(1m-reduction),
                MitigatedAmount=rawAmount*reduction,ReductionPercent=reduction*100m,
                DefenseValue=t.TargetArmor};
        }
    }

    private sealed class BeastState(
        BeastMasteryHunterSimRequest req,SimulationContext ctx,SimulationActorState hunter,
        SimulationActorState? pet,List<SimulationActorState> hawks,SimulationActorState boss,
        ForeverHunterPetFamily family,AuraManager aura,
        ForeverBeastMasteryHunterTalents talents,ForeverBeastMasteryHunterTuning t) : ICombatEventProcessor
    {
        private AutoAttackProcessor? _auto;
        private AbilityExecutor? _executor;
        private AutoAttackDefinition? _petAuto;
        private AutoAttackDefinition? _hawkAuto;
        private decimal _petEngagedAt=-1m,_petDisengagedAt,_totalPetUptime;
        private decimal _wrathUntil,_frenzyUntil,_rapidUntil,_deadlyUntil,_wolfHowlUntil;
        private bool _wolfHowlApplied;
        private readonly Dictionary<string,decimal> _hawkExpires=new(StringComparer.Ordinal);
        public int FrenzyProcs {get;private set;}
        public int FamilySpecialCasts {get;private set;}
        public int HawkSummons {get;private set;}
        public int PetCrits {get;private set;}
        public int PetMisses {get;private set;}
        public bool IntimidationArmed {get;private set;}
        public decimal PetActiveSeconds => _totalPetUptime+(_petEngagedAt<0m?0m:
            Math.Max(0m,req.DurationSeconds-_petEngagedAt));
        public void AttachAuto(AutoAttackProcessor auto)=>_auto=auto;
        public void AttachExecutor(AbilityExecutor executor)=>_executor=executor;
        public void AttachPetAuto(AutoAttackDefinition attack)=>_petAuto=attack;
        public void AttachHawkAuto(AutoAttackDefinition attack)=>_hawkAuto=attack;
        public void Schedule(SimulationContext context,decimal at,string effect,string? who=null)
        {
            if(at>=req.DurationSeconds || at<0m)return;
            context.ScheduleEvent(new CombatEvent{TimeSeconds=at,Type=CombatEventType.RotationDecision,
                SourceActorKey=who??hunter.Key,TargetActorKey=boss.Key,EffectKey=effect,IsInternal=true});
        }
        private void Buff(SimulationActorState who,string key,decimal duration)
        {
            aura.ApplyAura(ctx,who,new AuraDefinition {Key=key,Name=key,DurationSeconds=duration},
                hunter.Key,key,"pet-talent");
        }
        public decimal DamageMultiplier(string sourceKey,decimal now)
        {
            var activePet=pet is not null && pet.IsAlive && _petEngagedAt>=0m &&
                (_petDisengagedAt==0m || now<_petDisengagedAt);
            var focused=activePet?1m+.01m*talents.Rank("focused-fire"):1m;
            if(sourceKey==hunter.Key)return focused;
            var petOrHawk=sourceKey==pet?.Key || sourceKey.StartsWith("hawk-",StringComparison.Ordinal);
            if(!petOrHawk)return 1m;
            var value=focused*(1m+.03m*talents.Rank("unleashed-fury"));
            if(sourceKey==pet?.Key)value*=1m+family.DamageModifier;
            if(pet is not null && sourceKey==pet.Key &&
               pet.ActiveAuras.Any(x=>x.Definition.Key=="pet-prowl-buff"&&x.IsActiveAt(now)))
                value*=1.50m; // Max-rank Prowl: +50% to the first strike only.
            if(sourceKey==pet?.Key && now<_wrathUntil)value*=1.5m;
            return value;
        }
        private void ApplyHaste(SimulationActorState who,decimal added)
        {
            if(_auto is null)return;
            var old=who.Stats.Get("haste-percent");
            var value=Math.Max(0m,old+added);
            who.Stats.Set("haste-percent",value);
            _auto.RescheduleForHasteChange(ctx,who.Key,old,value);
        }
        private void SummonHawk(decimal now)
        {
            if(_auto is null || _hawkAuto is null || hawks.Count==0)return;
            // Two independent actors; a third call refreshes the oldest active hawk.
            var selected=hawks.OrderBy(x=>_hawkExpires.GetValueOrDefault(x.Key)).First();
            _auto.Stop(ctx,selected.Key,"hawk-auto");
            _hawkExpires[selected.Key]=now+18m;
            HawkSummons++;
            // The first swoop is a separate immediate strike. The summoned hawk
            // then attacks independently for the full 18-second duration.
            if(_executor is not null){
                var dive=108m+.05m*t.RangedAttackPower;
                _executor.ExecuteBackgroundDirectDamage(ctx,selected,boss,"summon-hawk",
                    "summon-hawk",new AbilityEffectDefinition {
                    Key="hawk-dive-impact",EffectType=AbilityEffectTypes.DirectDamage,
                    TargetType=AbilityTargetTypes.Enemy,SchoolKey="physical",
                    MitigationType=DamageMitigationTypes.Armor,
                    ResolutionType=CombatResolutionTypes.Melee,CanMiss=true,CanCrit=true,
                    MinimumValue=dive,MaximumValue=dive
                });
            }
            Buff(selected,"hawk-engaged",18m);
            _auto.Start(ctx,selected.Key,boss.Key,_hawkAuto,0m);
            Schedule(ctx,now+18m,"hawk-expire",selected.Key);
        }
        public void Process(SimulationContext context,CombatEvent ev)
        {
            var now=ev.TimeSeconds;
            if(ev.Type==CombatEventType.AbilityCastCompleted && ev.SourceActorKey==hunter.Key){
                if(ev.AbilityKey=="summon-hawk")SummonHawk(now);
                if(ev.AbilityKey=="bestial-wrath" && pet is not null && pet.IsAlive && _petEngagedAt>=0m){
                    _wrathUntil=now+18m;Buff(pet,"bestial-wrath",18m);
                }
                if(ev.AbilityKey=="intimidation" && pet is not null && pet.IsAlive){
                    IntimidationArmed=true;Buff(pet,"intimidation-next-hit",10m);
                }
                if(ev.AbilityKey=="mend-pet" && pet is not null && pet.IsAlive){
                    aura.RemoveAuras(ctx,hunter,"pet-needs-mend",hunter.Key,"mend-started");
                    for(int i=1;i<=5;i++)Schedule(ctx,now+i*3m,"pet-mend-tick");
                }
                if(ev.AbilityKey=="revive-pet" && pet is not null && !pet.IsAlive){
                    aura.RemoveAuras(ctx,hunter,"pet-needs-revive",hunter.Key,"revived");
                    var health=.40m+.15m*talents.Rank("improved-revive-pet");
                    pet.InitializeHealth(pet.MaximumHealth,pet.MaximumHealth*health);
                    pet.Resources["focus"].SetCurrent(100m);
                    _petDisengagedAt=0m;
                    Schedule(ctx,now+.5m,"pet-engage");
                }
            }
            if(ev.Type==CombatEventType.AuraApplied && ev.AbilityKey=="rapid-fire" &&
                ev.SourceActorKey==hunter.Key && ev.TargetActorKey==hunter.Key){
                _rapidUntil=now+15m;ApplyHaste(hunter,40m);
                Schedule(ctx,now+15m,"rapid-expire");
            }
            if(ev.Type==CombatEventType.Damage && ev.SourceActorKey==hunter.Key &&
                ev.AbilityKey=="auto-shot" && talents.Rank("deadly-aspects")>0 && now>=_deadlyUntil &&
                (decimal)ctx.Random.NextDouble()<.02m*talents.Rank("deadly-aspects")){
                _deadlyUntil=now+12m;ApplyHaste(hunter,30m);
                Buff(hunter,"deadly-aspects",12m);
                Schedule(ctx,now+12m,"deadly-expire");
            }
            if(pet is not null && ev.Type==CombatEventType.Damage && ev.SourceActorKey==pet.Key &&
                (ev.AbilityKey=="pet-melee" || ev.AbilityKey?.StartsWith("pet-",StringComparison.Ordinal)==true)){
                // The first outgoing attack (including a miss) breaks Prowl.
                // Resolve damage first so the hit can receive the stealth bonus.
                if(pet.ActiveAuras.Any(x=>x.Definition.Key=="pet-prowl-buff"&&x.IsActiveAt(now)))
                    aura.RemoveAuras(ctx,pet,"pet-prowl-buff",hunter.Key,"first attack broke Prowl");
                if(ev.ResultKey is "miss" or "dodge" or "parry")PetMisses++;
                if(ev.IsCritical && ev.Amount>0m){
                    PetCrits++;
                    if(talents.Rank("frenzy")>0 &&
                       (decimal)ctx.Random.NextDouble() <.20m*talents.Rank("frenzy")){
                        if(now>=_frenzyUntil)ApplyHaste(pet,30m);
                        _frenzyUntil=now+8m;FrenzyProcs++;
                        Buff(pet,"pet-frenzy",8m);
                        Schedule(ctx,now+8m,"frenzy-expire");
                    }
                }
                if(IntimidationArmed && ev.Amount>0m){
                    IntimidationArmed=false;
                    Buff(boss,"hunter-intimidation-stun",3m);
                    ThreatManipulator.Apply(ctx,boss,pet,
                        ThreatManipulationOperationTypes.Add,ev.Amount.Value);
                }
            }
            if(ev.Type==CombatEventType.AbilityCastCompleted && pet is not null && ev.SourceActorKey==pet.Key &&
                ev.AbilityKey=="pet-"+family.SpecialAbility){
                FamilySpecialCasts++;
                var special=family.SpecialAbility;
                if(special=="furious-howl"){
                    // This solo encounter models the wolf's own melee AP gain.
                    // The Hunter fires from ranged position (>15 yards), so
                    // an unsupported party-wide aura must not boost ranged DPS.
                    if(!_wolfHowlApplied){
                        pet.Stats.Set("attack-power",pet.Stats.Get("attack-power")+
                            t.WolfHowlMeleeAttackPowerBonus);
                        _wolfHowlApplied=true;
                    }
                    _wolfHowlUntil=now+60m;
                    Buff(pet,"wolf-furious-howl",60m);
                    Schedule(ctx,now+60m,"wolf-howl-expire");
                }
                if(special=="tricksters-dance"){
                    ApplyHaste(pet,30m);
                    Schedule(ctx,now+12m,"fox-haste-expire");
                }
                if(special is "scorpid-poison" or "savage-rend" or "tendon-rip" or "dismember" or
                    "pinch" or "web" or "sonic-blast" or "demoralizing-screech" or "dust-cloud" or
                    "mine" or "charge")
                    Buff(boss,"pet-"+special+"-debuff",special=="dismember"?6m:5m);
                if(special is "shell-shield" or "prowl" or "tricksters-dance")
                    Buff(pet,"pet-"+special+"-buff",
                        special=="prowl"?req.DurationSeconds: special=="tricksters-dance"?12m:8m);
            }
            if(ev.Type!=CombatEventType.RotationDecision || !ev.IsInternal ||
                ev.EffectKey is null)return;
            switch(ev.EffectKey){
                case "pet-engage":
                    if(pet is null||!pet.IsAlive||_petEngagedAt>=0m)return;
                    _petDisengagedAt=0m;
                    _petEngagedAt=now;Buff(pet,"pet-engaged",req.DurationSeconds-now);
                    Buff(hunter,"hunter-pet-active",req.DurationSeconds-now);
                    if(_auto is not null && _petAuto is not null)
                        _auto.Start(ctx,pet.Key,boss.Key,_petAuto,0m);
                    break;
                case "pet-disengage":
                    if(pet is null||_petDisengagedAt>0m)return;
                    _petDisengagedAt=now;
                    if(_petEngagedAt>=0m)_totalPetUptime+=now-_petEngagedAt;
                    _petEngagedAt=-1m;
                    aura.RemoveAuras(ctx,pet,"pet-engaged",hunter.Key,"pet-inactive");
                    aura.RemoveAuras(ctx,hunter,"hunter-pet-active",hunter.Key,"pet-inactive");
                    _auto?.Stop(ctx,pet.Key,"pet-melee");
                    break;
                case "pet-mend-tick":
                    if(pet is not null && pet.IsAlive){
                        var heal=pet.Heal(pet.MaximumHealth*.035m);
                        if(heal>0m)ctx.EmitEvent(new CombatEvent {
                            TimeSeconds=now,Type=CombatEventType.Healing,
                            SourceActorKey=hunter.Key,TargetActorKey=pet.Key,
                            AbilityKey="mend-pet",Amount=heal
                        });
                    }
                    break;
                case "pet-focus-report":
                    if(pet is not null && pet.IsAlive){
                        var gain=pet.Resources["focus"].AdvanceTo(now);
                        if(gain>0m)ctx.EmitEvent(new CombatEvent {
                            TimeSeconds=now,Type=CombatEventType.ResourceChanged,
                            SourceActorKey=pet.Key,TargetActorKey=pet.Key,
                            AbilityKey="pet-focus-regeneration",ResourceKey="focus",Amount=gain
                        });
                    }
                    Schedule(ctx,now+1m,"pet-focus-report");
                    break;
                case "spirit-bond-tick":
                    if(pet is not null && pet.IsAlive && _petEngagedAt>=0m && _petDisengagedAt==0m){
                        var healedHunter=hunter.Heal(hunter.MaximumHealth*.01m);
                        var healedPet=pet.Heal(pet.MaximumHealth*.01m);
                        if(healedHunter>0m) ctx.EmitEvent(new CombatEvent {
                            TimeSeconds=now,Type=CombatEventType.Healing,SourceActorKey=hunter.Key,
                            TargetActorKey=hunter.Key,AbilityKey="spirit-bond",Amount=healedHunter
                        });
                        if(healedPet>0m) ctx.EmitEvent(new CombatEvent {
                            TimeSeconds=now,Type=CombatEventType.Healing,SourceActorKey=hunter.Key,
                            TargetActorKey=pet.Key,AbilityKey="spirit-bond",Amount=healedPet
                        });
                    }
                    Schedule(ctx,now+(talents.Rank("spirit-bond")==2?5m:10m),"spirit-bond-tick");
                    break;
                case "pet-incoming-hit":
                    if(pet is null||!pet.IsAlive || _petDisengagedAt>0m)return;
                    var armor=pet.Stats.Get("armor");
                    var reduction=armor/(armor+400m+85m*63m);
                    var damage=req.PetIncomingHitDamage*(1m-reduction);
                    if(req.UseAspectOfTheMonkey){
                        var bonus=(8m+2m*talents.Rank("improved-aspect-of-the-monkey"))*.5m;
                        if((decimal)ctx.Random.NextDouble()*100m<bonus){
                            Schedule(ctx,now+req.PetIncomingHitEverySeconds,"pet-incoming-hit");
                            break;
                        }
                    }
                    if(pet.ActiveAuras.Any(x=>x.Definition.Key=="pet-tricksters-dance-buff"&&x.IsActiveAt(now)) &&
                       (decimal)ctx.Random.NextDouble()<.50m){
                        Schedule(ctx,now+req.PetIncomingHitEverySeconds,"pet-incoming-hit");
                        break;
                    }
                    if(boss.ActiveAuras.Any(x=>x.Definition.Key=="pet-demoralizing-screech-debuff"&&x.IsActiveAt(now)))
                        damage*=.85m;
                    if(pet.ActiveAuras.Any(x=>x.Definition.Key=="pet-shell-shield-buff"&&x.IsActiveAt(now)))
                        damage*=.50m;
                    if((decimal)ctx.Random.NextDouble()*100m>=t.PetAvoidancePercent){
                        var applied=pet.TakeDamage(damage);
                        if(applied>0m)ctx.EmitEvent(new CombatEvent{
                            TimeSeconds=now,Type=CombatEventType.Damage,
                            SourceActorKey=boss.Key,TargetActorKey=pet.Key,
                            AbilityKey="scripted-pet-incoming-hit",Amount=applied,
                            SchoolKey="physical",ResultKey="hit",
                            Description=$"{pet.Name} took {applied:F2} scripted incoming damage."
                        });
                    }
                    if(pet.IsAlive && req.UseMendPet &&
                        pet.CurrentHealth <pet.MaximumHealth*.55m &&
                        !hunter.ActiveAuras.Any(x=>x.Definition.Key=="pet-needs-mend"&&x.IsActiveAt(now)))
                        Buff(hunter,"pet-needs-mend",30m);
                    if(!pet.IsAlive){
                        if(_petEngagedAt>=0m)_totalPetUptime+=now-_petEngagedAt;
                        _petEngagedAt=-1m;
                        _petDisengagedAt=now;
                        if(req.UseRevivePet)Buff(hunter,"pet-needs-revive",30m);
                        _auto?.Stop(ctx,pet.Key,"pet-melee");
                        aura.RemoveAuras(ctx,pet,"pet-engaged",hunter.Key,"pet-death");
                        aura.RemoveAuras(ctx,hunter,"hunter-pet-active",hunter.Key,"pet-death");
                        ctx.EmitEvent(new CombatEvent{TimeSeconds=now,Type=CombatEventType.ActorDied,
                            SourceActorKey=boss.Key,TargetActorKey=pet.Key,
                            Description=$"{pet.Name} died from scripted incoming damage."});
                    }else Schedule(ctx,now+req.PetIncomingHitEverySeconds,"pet-incoming-hit");
                    break;
                case "rapid-expire":if(now>=_rapidUntil)ApplyHaste(hunter,-40m);break;
                case "deadly-expire":if(now>=_deadlyUntil)ApplyHaste(hunter,-30m);break;
                case "frenzy-expire":if(pet is not null&&now>=_frenzyUntil)ApplyHaste(pet,-30m);break;
                case "fox-haste-expire":if(pet is not null)ApplyHaste(pet,-30m);break;
                case "wolf-howl-expire":
                    if(pet is not null && _wolfHowlApplied && now>=_wolfHowlUntil){
                        pet.Stats.Set("attack-power",Math.Max(0m,
                            pet.Stats.Get("attack-power")-t.WolfHowlMeleeAttackPowerBonus));
                        _wolfHowlApplied=false;
                    }
                    break;
                case "hawk-expire":
                    var hawk=hawks.FirstOrDefault(x=>x.Key==ev.SourceActorKey);
                    if(hawk is not null && _hawkExpires.GetValueOrDefault(hawk.Key)<=now)
                        _auto?.Stop(ctx,hawk.Key,"hawk-auto");
                    break;
            }
        }
    }
}
