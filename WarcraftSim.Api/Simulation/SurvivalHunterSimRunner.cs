using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Forever Survival, deterministic positional scenario with real engine ability,
/// swing, aura, periodic-damage, resource and summary resolution.
/// </summary>
public sealed class SurvivalHunterSimRunner
{
    public SurvivalHunterSimResponse Run(SurvivalHunterSimRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request=ForeverEquipmentStatBridge.Resolve(request);
        if (request.Talents is null || request.Tuning is null ||
            request.DurationSeconds is <= 0m or > 1200m ||
            request.TargetMaximumHealth <= 0m || request.AdditionalTargets is < 0 or > 8 ||
            request.CombatMode is not ("melee" or "ranged" or "hybrid" or "weave") ||
            request.FireTrapKind is not ("explosive" or "immolation") ||
            request.FrostTrapKind is not ("frost" or "freezing") ||
            request.CombatMode == "hybrid" && (request.SwitchAtSeconds <= 0m ||
                request.SwitchAtSeconds >= request.DurationSeconds) ||
            request.TrapTriggerDelaySeconds < 0m || request.WeaveTravelSeconds < 0m ||
            request.WeaveMinimumMeleeSeconds < 0m || request.WeaveManaReserve < 0m ||
            request.ScriptedAvoidanceEverySeconds is <= 0m ||
            request.PetTuning is null || request.PetTalents is null ||
            request.PetAbilityPriority is null ||
            request.PetEngagementDelaySeconds < 0m ||
            request.PetIncomingHitDamage < 0m ||
            request.PetIncomingHitEverySeconds <= 0m ||
            request.DurationSeconds > 1200m)
            throw new ArgumentException("Survival requires valid deterministic melee/ranged and companion conditions.");
        request.Talents.Validate(); request.Tuning.Validate();
        request.PetTuning.Validate();
        request.PetTalents.Validate();
        if(request.PetTalents.Ranks.Values.Sum()+
            request.Talents.Ranks.Values.Sum()>51)
            throw new ArgumentException("Survival and off-tree pet talents exceed 51 points.");
        var family=request.HasPet?ForeverHunterPetFamilies.Get(request.PetFamily):null;
        var talents = request.Talents;
        var t = request.Tuning;
        var stats = new StatCollection();
        var agilityBonus = t.Agility * (.02m * talents.Rank("lightning-reflexes"));
        stats.Set(ForeverCombatStatKeys.AttackPower, t.MeleeAttackPower + agilityBonus);
        stats.Set("hunter-ranged-attack-power",t.RangedAttackPower+agilityBonus);
        stats.Set("companion-attack-power",Math.Max(t.MeleeAttackPower,t.RangedAttackPower)+agilityBonus);
        stats.Set("armor",request.PetTuning.HunterArmor);
        stats.Set("ranged-hit-bonus-percent",request.PetTuning.HunterRangedHitBonusPercent);
        stats.Set("ranged-critical-chance-percent",t.CriticalChancePercent);
        stats.Set("melee-critical-chance-percent",t.CriticalChancePercent);
        stats.Set(ForeverCombatStatKeys.DodgeReductionPercent,request.PetTuning.HunterExpertisePercent);
        stats.Set(ForeverCombatStatKeys.ParryReductionPercent,request.PetTuning.HunterExpertisePercent);
        var context = new SimulationContext(new SimulationRunOptions {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "hunter", CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 70000, CancellationToken = cancellationToken
        });
        var hunter = new SimulationActorState {
            Key = "hunter", Name = "Survival Hunter", TeamKey = "raid", Level = 60,
            MaximumHealth = t.MaximumHealth * (1m + .02m * talents.Rank("survivalist")), Stats = stats
        };
        hunter.InitializeHealth(hunter.MaximumHealth);
        hunter.AddResource(new ResourceState("mana", t.MaximumMana, t.MaximumMana));
        var boss = new SimulationActorState { Key = "target", Name = "Level 63 training boss",
            TeamKey = "enemy", Level = 63, MaximumHealth = request.TargetMaximumHealth };
        boss.InitializeHealth(boss.MaximumHealth);
        context.AddActor(hunter); context.AddActor(boss);
        for (var i = 1; i <= request.AdditionalTargets; i++) {
            var add = new SimulationActorState { Key = $"add-{i}", Name = $"Additional enemy {i}",
                TeamKey = "enemy", Level = 63, MaximumHealth = request.TargetMaximumHealth };
            add.InitializeHealth(add.MaximumHealth);
            context.AddActor(add);
        }
        var petTarget=context.GetActor(request.PetTargetKey);
        if(request.HasPet && (petTarget is null || petTarget.TeamKey!="enemy"))
            throw new ArgumentException("Pet target must be a configured enemy actor.");
        SimulationActorState? pet=null;
        if(family is not null)
            pet=HunterCompanionComposition.Create(context,hunter,family,request.PetTuning,
                request.PetTalents.Rank("endurance-training"),
                request.PetTalents.Rank("bestial-discipline"));
        var hawks=request.PetTalents.Rank("summon-hawk")>0?
            HunterSharedHawkRuntime.Create(context,hunter):
            new List<SimulationActorState>();
        foreach (var ability in ForeverSurvivalHunterSpellbook.Create(talents, t))
            hunter.AddAbility(ability);
        if(pet is not null || hawks.Count>0)
        {
            var bmSpells=ForeverBeastMasteryHunterSpellbook.Hunter(
                request.PetTalents,request.PetTuning);
            if(hawks.Count>0)
            {
                hunter.AddAbility(bmSpells.Single(a=>a.Key=="summon-hawk"));
                // The current client puts Summon Hawk and Arcane Shot on
                // the same six-second shared cooldown across specs.
                if(hunter.Abilities.TryGetValue("arcane-shot",out var arcane))
                {
                    arcane.Definition.SharedCooldownGroupKey="hunter-arcane-hawk";
                    arcane.Definition.SharedCooldownSeconds=6m;
                }
            }
            if(pet is not null)
                foreach(var key in new[]{"bestial-wrath","intimidation"})
                    if(bmSpells.FirstOrDefault(a=>a.Key==key) is { } spell)
                        hunter.AddAbility(spell);
            if(pet is not null && request.UseRevivePet)
                hunter.AddAbility(bmSpells.Single(a=>a.Key=="revive-pet"));
        }
        hunter.AddReactiveOpportunityDefinition(new ReactiveAbilityOpportunityDefinition {
            OpportunityKey = "hunter-mongoose-ready", AbilityKey = "mongoose-bite", DodgeWindowSeconds = 10m
        });
        if (talents.Rank("counterattack") > 0)
            hunter.AddReactiveOpportunityDefinition(new ReactiveAbilityOpportunityDefinition {
                OpportunityKey = "hunter-counterattack-ready", AbilityKey = "counterattack", DodgeWindowSeconds = 5m
            });
        var aura = new AuraManager();
        var rolls = new SurvivalRolls(t, talents,request.PetTuning,request.PetTalents);
        var mitigator = new SurvivalArmor(t, talents, request.TargetIsTracked,family,request.PetTalents);
        var executor = new AbilityExecutor(aura, rolls, mitigator);
        var autos = new AutoAttackProcessor(executor, new ForeverAutoAttackTimingProvider());
        var aspects=new HunterAspectProcessor(hunter,aura,primaryAttackPowerIsRanged:false);
        var hawkRuntime=hawks.Count>0?
            new HunterSharedHawkRuntime(hunter,boss,hawks,aura,executor,autos,
                request.PetTuning):null;
        var petRuntime=pet is not null && family is not null?
            new HunterSharedCompanionRuntime(hunter,pet,petTarget!,family,request.PetTuning,
                aura,autos,HunterCompanionComposition.Melee(family,request.PetTuning),
                request.DurationSeconds,request.UsePetSpecial,
                request.PetEngagementDelaySeconds/
                    (request.PetTalents.Rank("bestial-swiftness")>0?1.3m:1m),
                request.PetIncomingHitDamage,request.PetIncomingHitEverySeconds,
                request.UseRevivePet,request.PetTalents.Rank("improved-revive-pet"),
                request.PetTalents.Rank("frenzy"),
                request.PetTalents.Rank("spirit-bond")):null;
        var meleeAuto = new AutoAttackDefinition {
            Key = "melee-auto", Name = "Melee Auto Attack",
            SwingIntervalSeconds = t.MeleeWeaponSpeedSeconds,
            DamageEffect = new AbilityEffectDefinition {
                Key = "melee-auto-impact", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, ResolutionType = CombatResolutionTypes.Melee,
                SchoolKey = "physical", MitigationType = DamageMitigationTypes.Armor,
                CanMiss = true, CanCrit = true, CanBeDodged = true, CanBeParried = true,
                MinimumValue = t.MeleeWeaponMinDamage, MaximumValue = t.MeleeWeaponMaxDamage,
                ScalingStatKey = ForeverCombatStatKeys.AttackPower,
                ScalingCoefficient = t.MeleeWeaponSpeedSeconds / 14m
            }
        };
        AutoAttackDefinition? offhandAuto = null;
        if (request.UseOffhandWeapon) offhandAuto = new AutoAttackDefinition {
            Key = "offhand-auto", Name = "Offhand Melee Attack",
            WeaponHandKey = WeaponHandKeys.OffHand,
            SwingIntervalSeconds = t.OffhandWeaponSpeedSeconds,
            DamageMultiplier = .5m * (1m + .10m * talents.Rank("predators-edge")),
            DamageEffect = new AbilityEffectDefinition {
                Key = "offhand-impact", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, WeaponHandKey = WeaponHandKeys.OffHand,
                ResolutionType = CombatResolutionTypes.Melee, SchoolKey = "physical",
                MitigationType = DamageMitigationTypes.Armor, CanMiss = true,
                CanCrit = true, CanBeDodged = true, CanBeParried = true,
                MinimumValue = t.OffhandWeaponMinDamage,
                MaximumValue = t.OffhandWeaponMaxDamage,
                ScalingStatKey = ForeverCombatStatKeys.AttackPower,
                ScalingCoefficient = t.OffhandWeaponSpeedSeconds / 14m
            }
        };
        var rangedAuto = new AutoAttackDefinition {
            Key = "auto-shot", Name = "Auto Shot", SwingIntervalSeconds = t.RangedWeaponSpeedSeconds,
            DamageEffect = new AbilityEffectDefinition {
                Key = "ranged-auto-impact", EffectType = AbilityEffectTypes.DirectDamage,
                TargetType = AbilityTargetTypes.Enemy, ResolutionType = CombatResolutionTypes.Melee,
                SchoolKey = "physical", MitigationType = DamageMitigationTypes.Armor,
                CanMiss = true, CanCrit = true,
                MinimumValue = t.RangedWeaponMinDamage,
                MaximumValue = t.RangedWeaponMaxDamage,
                ScalingStatKey="hunter-ranged-attack-power",
                ScalingCoefficient=t.RangedWeaponSpeedSeconds/14m
            }
        };
        var special = new SurvivalReactiveProcessor(request, context, hunter, boss, aura,
            executor, autos, meleeAuto, offhandAuto, rangedAuto, talents, t);
        var regen = new FiveSecondManaRegenerationProcessor("hunter", t.SpiritRegenPerSecond,
            t.ManaPerFiveSeconds,request.PetTalents.Rank("bestial-discipline")*.25m);
        var rotation=BuildRotation(request,hunter);
        if(pet is not null && request.UseRevivePet)
            rotation.Entries.Insert(0,new RotationEntry{
                Priority=-1,AbilityKey="revive-pet"
            });
        var priority=new PriorityRotationExecutor(rotation,"hunter","target",
            executor,autoAttackProcessor:autos,reactToTargetStateChanges:true);
        // Owner melee/ranged streams and pet auto/focus execute on one timeline.
        var equipmentEffects=new ForeverEquipmentEffectProcessor(
            request.Equipment,"hunter","target",aura,executor,request.Seed,autos);
        var processors=new List<ICombatEventProcessor> {
            executor,aura,equipmentEffects,aspects,autos,
            new ReactiveAbilityOpportunityProcessor(),special,regen
        };
        if(petRuntime is not null && pet is not null && family is not null)
        {
            processors.Add(petRuntime);
            processors.Add(new PriorityRotationExecutor(
                HunterCompanionComposition.Rotation(pet,family,request.UsePetSpecial,
                    request.UsePetBite,request.UsePetClaw,request.PetAbilityPriority),
                pet.Key,petTarget!.Key,executor,autoAttackProcessor:autos,
                reactToTargetStateChanges:true));
        }
        if(hawkRuntime is not null)processors.Add(hawkRuntime);
        processors.Add(new ThreatManager(request.HasPet || hawks.Count>0?
            new HunterOwnerAndPetThreatResolver():new SoloDamageThreatResolver("hunter")));
        processors.Add(priority);
        var engine = new SimulationEngine(processors);
        regen.Initialize(context);
        var run = engine.Run(context, started => {
            petRuntime?.Start(started,executor);
            if (request.ApplyHuntersMark)
                aura.ApplyAura(started, boss, new AuraDefinition {
                    Key = "hunters-mark", Name = "Hunter's Mark", DurationSeconds = request.DurationSeconds
                }, hunter.Key, "hunters-mark", "precast-mark");
            if (request.CombatMode != "ranged") {
                aura.ApplyAura(started, hunter, new AuraDefinition {
                    Key = "hunter-melee-stance", Name = "Scripted melee engagement",
                    DurationSeconds = request.CombatMode == "hybrid" ? request.SwitchAtSeconds : request.DurationSeconds
                }, hunter.Key, "hunter-melee-engage", "scripted-melee-stance");
                autos.Start(started, hunter.Key, boss.Key, meleeAuto);
                if (offhandAuto is not null)
                    autos.Start(started, hunter.Key, boss.Key, offhandAuto);
            } else {
                aura.ApplyAura(started, hunter, new AuraDefinition {
                    Key = "hunter-ranged-stance", Name = "Ranged engagement",
                    DurationSeconds = request.DurationSeconds
                }, hunter.Key, "hunter-ranged-engage", "scripted-ranged-stance");
                autos.Start(started, hunter.Key, boss.Key, rangedAuto, .5m);
            }
            if (request.CombatMode == "weave")
                started.ScheduleEvent(new CombatEvent { TimeSeconds = .5m,
                    Type = CombatEventType.RotationDecision, SourceActorKey = hunter.Key,
                    TargetActorKey = boss.Key, EffectKey = "hunter-weave-evaluate", IsInternal = true });
            if (request.CombatMode == "hybrid")
                started.ScheduleEvent(new CombatEvent { TimeSeconds = request.SwitchAtSeconds,
                    Type = CombatEventType.RotationDecision, SourceActorKey = hunter.Key,
                    TargetActorKey = boss.Key, EffectKey = "hunter-phase-ranged", IsInternal = true });
            if (request.ScriptedAvoidanceEverySeconds is { } every && request.CombatMode != "ranged")
                for (var when = every; when < request.DurationSeconds &&
                    (request.CombatMode is "melee" or "weave" || when < request.SwitchAtSeconds); when += every)
                    started.ScheduleEvent(new CombatEvent { TimeSeconds = when,
                        Type = CombatEventType.RotationDecision, SourceActorKey = hunter.Key,
                        TargetActorKey = boss.Key, EffectKey = "hunter-scripted-avoidance", IsInternal = true });
        });
        equipmentEffects.WriteMetrics(run);
        var summary = run.Summary.ActorSummaries[hunter.Key];
        var petReport=pet is null?null:run.Summary.ActorSummaries[pet.Key];
        var petDamage=petReport?.DamageDone??0m;
        var hawkDamage=hawks.Sum(h=>run.Summary.ActorSummaries[h.Key].DamageDone);
        var damage = summary.DamageDoneByAbility;
        var breakdown=new Dictionary<string,decimal>(damage,StringComparer.OrdinalIgnoreCase);
        if(petReport is not null)
            foreach(var pair in petReport.DamageDoneByAbility)
                breakdown["pet:"+pair.Key]=pair.Value;
        foreach(var hawk in hawks)
            foreach(var pair in run.Summary.ActorSummaries[hawk.Key].DamageDoneByAbility)
                breakdown[hawk.Key+":"+pair.Key]=pair.Value;
        var traps = damage.Where(kv => kv.Key is "explosive-trap" or "immolation-trap")
            .Sum(kv => kv.Value);
        var melee = damage.Where(kv => kv.Key is "melee-auto" or "raptor-strike" or "mongoose-bite"
            or "wing-clip" or "strider-kick" or "counterattack" or "lacerate" or
            "lacerating-strikes" or "offhand-auto")
            .Sum(kv => kv.Value);
        run.CustomMetrics["survival-trap-triggers"] = special.TrapTriggers;
        run.CustomMetrics["survival-entrapment-roots"] = special.EntrapmentRoots;
        run.CustomMetrics["survival-expose-prey-procs"] = special.ExposePreyProcs;
        run.CustomMetrics["survival-lacerating-bleed-ticks"] = special.BleedTicks;
        run.CustomMetrics["survival-resourcefulness-procs"] = special.ResourcefulnessProcs;
        run.CustomMetrics["survival-resourcefulness-mana"] = special.ResourcefulnessMana;
        run.CustomMetrics["survival-scripted-avoids"] = special.ScriptedAvoids;
        run.CustomMetrics["survival-weave-excursions"] = special.WeaveExcursions;
        run.CustomMetrics["survival-rapid-fire-casts"] = special.RapidFireCasts;
        return new SurvivalHunterSimResponse {
            DamageDone = summary.DamageDone+petDamage+hawkDamage,
            Dps = (summary.DamageDone+petDamage+hawkDamage) / request.DurationSeconds,
            HunterDamage=summary.DamageDone,PetDamage=petDamage,
            HawkDamage=hawkDamage,HawkSummons=hawkRuntime?.Summons??0,
            HawkThreat=hawks.Sum(h=>boss.ThreatTable.GetThreat(h.Key)),
            PetThreat=pet is null?0m:petTarget!.ThreatTable.GetThreat(pet.Key),
            PetFocus=pet?.Resources["focus"].Current??0m,
            PetUptimeSeconds=petRuntime?.UptimeSeconds??0m,
            PetDied=petRuntime?.Died??false,
            MeleeDamage = melee, TrapDamage = traps,
            RangedDamage = summary.DamageDone - melee - traps,
            EndingMana = hunter.Resources["mana"].Current,
            DamageByAbility = breakdown,
            Run = run,
            ModelingNotes = [
                "Forever Survival (client 1.60.1.70450). Scripted legacy windows or melee-first cooldown weaving; no geometric player movement; optional shared-engine pet actor.",
                "Fire and Frost traps use independent cooldowns and can be simultaneously armed. Configured proximity triggers are deterministic.",
                "Trap fire Attack Power coefficient and several weapon bonuses are provisional editable tuning values.",
                "Optional dual wield uses the shared offhand swing stream with Predator's Edge offhand scaling.",
                "Deterrence and control auras are exposed, but crowd-control AI, trap geometric placement, disarm and movement remain deferred.",
                "Mongoose/Counterattack opportunities derive from Expose Prey and optional scripted incoming avoids, not invented enemy combat AI."
            ]
        };
    }

    private static RotationProfile BuildRotation(SurvivalHunterSimRequest req, SimulationActorState hunter)
    {
        var rotation = new RotationProfile { Name = "Survival scripted phase priority",
            ClassKey = "hunter", RulesetKey = "wow-forever", SimulationType = SimulationType.Dps };
        int order = 0;
        void add(bool enabled, string key, bool melee = false, string? targetMissing = null) {
            if (!enabled || !hunter.Abilities.ContainsKey(key)) return;
            var conditions = new List<RotationConditionDefinition> {
                new() { ConditionType = RotationConditionTypes.SourceAuraActive,
                    Key = melee ? "hunter-melee-stance" : "hunter-ranged-stance" }
            };
            if (targetMissing is not null)
                conditions.Add(new RotationConditionDefinition {
                    ConditionType = RotationConditionTypes.TargetAuraMissing, Key = targetMissing
                });
            rotation.Entries.Add(new RotationEntry { Priority = ++order, AbilityKey = key, Conditions = conditions });
        }
        // Traps can be placed in either range mode. Their target is the placement owner.

        void trap(bool enabled, string key)
        {
            if (!enabled || !hunter.Abilities.ContainsKey(key))
                return;
            void addTrapEntry(string stance)
            {
                rotation.Entries.Add(new RotationEntry
                {
                    Priority = ++order,
                    AbilityKey = key,
                    Conditions =
                    [
                        new RotationConditionDefinition
                {
                    ConditionType = RotationConditionTypes.SourceAuraActive,
                    Key = stance
                }
                    ]
                });
            }
            addTrapEntry("hunter-melee-stance");
            if (req.TriggerRangedTraps)
                addTrapEntry("hunter-ranged-stance");
        }

        if(req.UseAspectOfTheBeast)
            rotation.Entries.Add(new RotationEntry {
                Priority=++order,AbilityKey="aspect-of-the-beast",
                Conditions=[new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.SourceAuraMissing,Key="hunter-aspect-beast"
                }]
            });
        else if(req.UseAspectOfTheHawk)
            rotation.Entries.Add(new RotationEntry {
                Priority=++order,AbilityKey="aspect-of-the-hawk",
                Conditions=[new RotationConditionDefinition {
                    ConditionType=RotationConditionTypes.SourceAuraMissing,Key="hunter-aspect-hawk"
                }]
            });
        if(req.UseVolley && req.AdditionalTargets>0)
            add(true,"volley");
        if (req.UseRapidFire) rotation.Entries.Add(new RotationEntry {
            Priority = ++order, AbilityKey = "rapid-fire" });
        if(hunter.Abilities.ContainsKey("summon-hawk"))
            rotation.Entries.Add(new RotationEntry {
                Priority=++order,AbilityKey="summon-hawk"
            });
        foreach(var key in new[]{"bestial-wrath","intimidation"})
            if(req.HasPet && hunter.Abilities.ContainsKey(key))
                rotation.Entries.Add(new RotationEntry {
                    Priority=++order,AbilityKey=key
                });
        trap(req.UseFireTraps, req.FireTrapKind + "-trap");
        trap(req.UseFrostTraps, req.FrostTrapKind + "-trap");
        add(true, "mongoose-bite", melee:true);
        add(true, "counterattack", melee:true);
        add(true, "strider-kick", melee:true);
        add(true, "raptor-strike", melee:true);
        add(true, "lacerate", melee:true, targetMissing:"hunter-lacerate");
        add(req.UseWingClip, "wing-clip", melee:true, targetMissing:"hunter-wing-clip-slow");
        add(true, "serpent-sting", targetMissing:"serpent-sting");
        add(req.UseAimedShot && req.AdditionalTargets == 0, "aimed-shot");
        add(req.UseMultiShot && req.AdditionalTargets > 0, "multi-shot");
        add(req.UseAimedShot, "aimed-shot");
        add(true, "arcane-shot");
        if(req.CustomPriority is null)
            throw new ArgumentException("Survival custom priority cannot be null.");
        if(req.CustomPriority.Count>0)
        {
            if(req.CustomPriority.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=
                req.CustomPriority.Count)
                throw new ArgumentException("Survival custom priority repeats an ability.");
            var rank=0;
            var ordered=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            foreach(var key in req.CustomPriority)
            {
                if(!rotation.Entries.Any(e=>
                    string.Equals(e.AbilityKey,key,StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"Unavailable Survival action: {key}");
                ordered[key]=++rank;
            }
            foreach(var entry in rotation.Entries)
            {
                var previous=entry.Priority;
                entry.Priority=ordered.TryGetValue(entry.AbilityKey,out var position)
                    ?position:1000+previous;
            }
        }
        return rotation;
    }

    private sealed class SurvivalRolls(ForeverSurvivalHunterTuning t,
        ForeverSurvivalHunterTalents talents,ForeverBeastMasteryHunterTuning petTuning,
        ForeverBeastMasteryHunterTalents petTalents) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            Resolve(context, source, target, ability, effect, CombatEffectDeliveryType.Direct);
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType delivery)
        {
            if(source.Key=="hunter-pet" ||
               source.Key.StartsWith("hawk-",StringComparison.Ordinal))
                return HunterCompanionComposition.Resolve(context,source,target,effect,petTuning,
                    petTalents.Rank("ferocity"),
                    refreshOwnerStats:source.Key=="hunter-pet");
            bool melee = effect.CanBeDodged || effect.CanBeParried || ability.Key == "melee-auto";
            decimal hit = Math.Min(100m, t.HitChancePercent + talents.Rank("surefooted") +
                (ability.Key.Contains("trap", StringComparison.OrdinalIgnoreCase) ?
                    5m * talents.Rank("survival-tactics") : 0m));
            if (effect.CanMiss && (decimal)context.Random.NextDouble() * 100m >= hit)
                return CombatRollResult.Miss();
            if (effect.CanBeDodged && (decimal)context.Random.NextDouble() * 100m < t.TargetDodgePercent)
                return CombatRollResult.Dodge();
            if (effect.CanBeParried && (decimal)context.Random.NextDouble() * 100m < t.TargetParryPercent)
                return CombatRollResult.Parry();
            if (!effect.CanCrit) return CombatRollResult.Hit();
            decimal crit = t.CriticalChancePercent +
                (melee && ability.Key != "melee-auto" ? 2m * talents.Rank("savage-strikes") : 0m);
            if ((decimal)context.Random.NextDouble()*100m >= crit)
                return CombatRollResult.Hit();
            decimal bonus = melee && ability.Key != "melee-auto" ? .06m * talents.Rank("predators-edge") : 0m;
            return CombatRollResult.Critical(2m + bonus);
        }
    }

    private sealed class SurvivalArmor(ForeverSurvivalHunterTuning t,
        ForeverSurvivalHunterTalents talents,bool tracked,ForeverHunterPetFamily? petFamily,
        ForeverBeastMasteryHunterTalents petTalents) : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability,
            AbilityEffectDefinition effect, decimal rawAmount)
        {
            if (tracked && source.Key!="hunter-pet" && ability.Key != "lacerating-strikes")
                rawAmount *= 1m + .01m * talents.Rank("improved-tracking");
            if(source.Key=="hunter-pet" && petFamily is not null)
                rawAmount*=(1m+petFamily.DamageModifier)*
                    (1m+.03m*petTalents.Rank("unleashed-fury"));
            if(source.Key.StartsWith("hawk-",StringComparison.Ordinal))
                rawAmount*=1m+.03m*petTalents.Rank("unleashed-fury");
            if(source.Key=="hunter-pet" &&
               source.ActiveAuras.Any(a=>a.Definition.Key=="bestial-wrath" &&
                   a.SourceActorKey=="hunter" && a.IsActiveAt(context.CurrentTimeSeconds)))
                rawAmount*=1.5m;
            if(petFamily is not null &&
               context.GetActor("hunter-pet") is { IsAlive:true } &&
               (source.Key is ("hunter" or "hunter-pet") ||
                source.Key.StartsWith("hawk-",StringComparison.Ordinal)))
                rawAmount*=1m+.01m*petTalents.Rank("focused-fire");
            if (effect.MitigationType != DamageMitigationTypes.Armor)
                return DamageMitigationResult.Unmitigated(rawAmount);
            var reduction = ForeverArmorMitigationMath.ClampArmorReductionFraction(
                t.TargetArmor / (t.TargetArmor + 400m + 85m * Math.Max(1, source.Level)));
            return new DamageMitigationResult { RawAmount = rawAmount,
                FinalAmount = rawAmount * (1m - reduction),
                MitigatedAmount = rawAmount * reduction, ReductionPercent = 100m * reduction,
                DefenseValue = t.TargetArmor };
        }
    }

    private sealed class SurvivalReactiveProcessor(
        SurvivalHunterSimRequest request, SimulationContext ctx, SimulationActorState hunter,
        SimulationActorState boss, AuraManager aura, AbilityExecutor executor,
        AutoAttackProcessor autos, AutoAttackDefinition meleeAuto,
        AutoAttackDefinition? offhandAuto, AutoAttackDefinition rangedAuto,
        ForeverSurvivalHunterTalents talents, ForeverSurvivalHunterTuning t) : ICombatEventProcessor
    {
        private int _fireGeneration, _frostGeneration, _bleedGeneration;
        private decimal _bleedTickValue, _resourcefulExpires, _lastManaSpent = decimal.MinValue;
        private bool _resourceTickPending;
        private bool _weaveInTransit;
        private decimal _weaveLastReturned;
        private decimal _rapidEnds;
        // Some same-timestamp casts are dispatched after a melee-to-ranged
        // transition even though the player started them while still in melee.
        // Preserve the departure boundary for instant trap placement only.
        private decimal _lastMeleeDepartureAt = decimal.MinValue;
        private readonly Dictionary<string, (int Count, decimal Last)> _rootDr = new();
        // Remember proximity at the instant the trap was placed, rather than
        // reevaluating stance after other same-timestamp events (such as a weave).
        private readonly Dictionary<string, bool> _trapCastInMelee = new(StringComparer.Ordinal);
        public int WeaveExcursions { get; private set; }
        public int RapidFireCasts { get; private set; }
        public int TrapTriggers { get; private set; }
        public int EntrapmentRoots { get; private set; }
        public int ExposePreyProcs { get; private set; }
        public int BleedTicks { get; private set; }
        public int ResourcefulnessProcs { get; private set; }
        public int ScriptedAvoids { get; private set; }
        public decimal ResourcefulnessMana { get; private set; }
        private void schedule(decimal at, string effect, int? generation = null, string? target = null,
            decimal? amount = null)
        {
            if (at >= request.DurationSeconds) return;
            ctx.ScheduleEvent(new CombatEvent { TimeSeconds = at, Type = CombatEventType.RotationDecision,
                SourceActorKey = hunter.Key, TargetActorKey = target ?? boss.Key,
                EffectKey = effect, EncounterSequenceHitNumber = generation,
                Amount = amount, IsInternal = true });
        }
        private void status(SimulationActorState target, string key, decimal duration, string origin) {
            if (duration <= 0m) return;
            aura.ApplyAura(ctx, target, new AuraDefinition {
                Key = key, Name = key, DurationSeconds = duration
            }, hunter.Key, origin, "survival-status");
        }
        private bool IsMelee(decimal now) =>
            hunter.ActiveAuras.Any(a => a.Definition.Key == "hunter-melee-stance" && a.IsActiveAt(now));
        private void ApplyStance(string key, decimal now) =>
            aura.ApplyAura(ctx, hunter, new AuraDefinition {
                Key = key, Name = key, DurationSeconds = request.DurationSeconds - now
            }, hunter.Key, key, "survival-stance");
        private void SwitchToRanged(decimal now)
        {
            if (request.CombatMode == "weave" || IsMelee(now))
                _lastMeleeDepartureAt = now;
            autos.Stop(ctx, hunter.Key, "melee-auto");
            autos.Stop(ctx, hunter.Key, "offhand-auto");
            aura.RemoveAuras(ctx, hunter, "hunter-melee-stance", hunter.Key, "ranged excursion");
            if (request.CombatMode != "weave" || request.WeaveTravelSeconds <= 0m) {
                ApplyStance("hunter-ranged-stance", now);
                autos.Start(ctx, hunter.Key, boss.Key, rangedAuto, .5m);
            } else schedule(now + request.WeaveTravelSeconds, "hunter-weave-arrive-ranged");
        }
        private void SwitchToMelee(decimal now)
        {
            autos.Stop(ctx, hunter.Key, "auto-shot");
            aura.RemoveAuras(ctx, hunter, "hunter-ranged-stance", hunter.Key, "returning to melee");
            if (request.WeaveTravelSeconds <= 0m) {
                ApplyStance("hunter-melee-stance", now);
                autos.Start(ctx, hunter.Key, boss.Key, meleeAuto);
                if (offhandAuto is not null) autos.Start(ctx, hunter.Key, boss.Key, offhandAuto);
                _weaveLastReturned = now;
                _weaveInTransit = false;
            } else schedule(now + request.WeaveTravelSeconds, "hunter-weave-arrive-melee");
        }
        private void ApplyEntrapment(SimulationActorState target, decimal now)
        {
            if (request.BossImmuneToRoots && target.Key == boss.Key) return;
            _rootDr.TryGetValue(target.Key, out var dr);
            var count = now - dr.Last >= 18m ? 0 : dr.Count;
            // Three-step diminishing returns; fourth application is immune until reset.
            var scale = count switch { 0 => 1m, 1 => .5m, 2 => .25m, _ => 0m };
            _rootDr[target.Key] = (Math.Min(3, count + 1), now);
            if (scale <= 0m) return;
            status(target, "hunter-entrapment", talents.Rank("entrapment") * scale, "entrapment");
            EntrapmentRoots++;
        }
        private void damage(string ability, string key, string targetKey, decimal min, decimal max,
            string school, bool periodic = false, bool crit = true) {
            var target = ctx.GetActor(targetKey);
            if (target is null || !target.IsAlive) return;
            executor.ExecuteBackgroundDirectDamage(ctx, hunter, target, ability, ability,
                new AbilityEffectDefinition { Key = key,
                    EffectType = AbilityEffectTypes.DirectDamage,
                    TargetType = AbilityTargetTypes.Enemy, SchoolKey = school,
                    MitigationType = school == "physical" && !periodic ? DamageMitigationTypes.Armor : DamageMitigationTypes.None,
                    ResolutionType = CombatResolutionTypes.Spell, CanMiss = !periodic,
                    CanCrit = crit, MinimumValue = min, MaximumValue = max },
                deliveryType: periodic ? CombatEffectDeliveryType.Periodic : CombatEffectDeliveryType.Direct);
        }
        public void Process(SimulationContext context, CombatEvent ev)
        {
            var now = context.CurrentTimeSeconds;
            if (ev.Type == CombatEventType.AbilityCastCompleted &&
                ev.SourceActorKey == hunter.Key && ev.AbilityKey == "raptor-strike") {
                var replacement = new NextSwingReplacementDefinition {
                    Key = "raptor-strike", Name = "Raptor Strike", WeaponHandKey = WeaponHandKeys.MainHand,
                    ResourceCosts = [new AbilityResourceCost { ResourceKey = "mana",
                        Amount = 100m * (1m - .30m * talents.Rank("resourcefulness")) }],
                    DamageEffect = new AbilityEffectDefinition {
                        Key = "raptor-queued-impact", EffectType = AbilityEffectTypes.DirectDamage,
                        TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
                        WeaponHandKey = WeaponHandKeys.MainHand, ResolutionType = CombatResolutionTypes.Melee,
                        MitigationType = DamageMitigationTypes.Armor, CanMiss = true, CanCrit = true,
                        CanBeDodged = true, CanBeParried = true,
                        MinimumValue = t.MeleeWeaponMinDamage + t.RaptorStrikeBonusDamage,
                        MaximumValue = t.MeleeWeaponMaxDamage + t.RaptorStrikeBonusDamage,
                        ScalingStatKey = ForeverCombatStatKeys.AttackPower,
                        ScalingCoefficient = t.MeleeWeaponSpeedSeconds / 14m
                    }
                };
                autos.QueueNextSwingReplacement(ctx, hunter.Key, "melee-auto", replacement);
            }
            if (ev.Type == CombatEventType.AuraApplied && ev.SourceActorKey == hunter.Key &&
                ev.TargetActorKey == hunter.Key && ev.AbilityKey == "rapid-fire") {
                RapidFireCasts++;
                var hasteBefore = hunter.Stats.Get(ForeverCombatStatKeys.HastePercent);
                _rapidEnds = now + 15m;
                hunter.Stats.Set(ForeverCombatStatKeys.HastePercent, hasteBefore + 40m);
                autos.RescheduleForHasteChange(ctx, hunter.Key, hasteBefore, hasteBefore + 40m);
            }
            if (ev.Type == CombatEventType.AuraRemoved && ev.SourceActorKey == hunter.Key &&
                ev.TargetActorKey == hunter.Key && ev.AbilityKey == "rapid-fire" && now >= _rapidEnds) {
                var hasteBefore = hunter.Stats.Get(ForeverCombatStatKeys.HastePercent);
                var hasteAfter = Math.Max(0m, hasteBefore - 40m);
                hunter.Stats.Set(ForeverCombatStatKeys.HastePercent, hasteAfter);
                autos.RescheduleForHasteChange(ctx, hunter.Key, hasteBefore, hasteAfter);
            }
            if (ev.Type == CombatEventType.ResourceChanged && ev.SourceActorKey == hunter.Key &&
                ev.ResourceKey == "mana" && ev.Amount < 0m)
                _lastManaSpent = now;
            if (ev.Type == CombatEventType.AbilityCastStarted && ev.SourceActorKey == hunter.Key &&
                ev.AbilityKey is ("explosive-trap" or "immolation-trap" or "frost-trap" or "freezing-trap")) {
                // The action may be queued while in melee, but dispatched after
                // departure at the exact same timestamp. That placement still
                // belongs to the melee action window, not the travel window.
                _trapCastInMelee[ev.AbilityKey] = IsMelee(now) ||
                    _lastMeleeDepartureAt == now;
            }
            if (ev.Type == CombatEventType.AuraApplied && ev.SourceActorKey == hunter.Key &&
                ev.TargetActorKey == hunter.Key &&
                ev.AbilityKey is ("explosive-trap" or "immolation-trap" or "frost-trap" or "freezing-trap")) {
                bool fire = ev.AbilityKey is "explosive-trap" or "immolation-trap";
                int gen = fire ? ++_fireGeneration : ++_frostGeneration;
                // The placement is legitimate if the hunter was in melee when
                // casting, even when a stance transition shares its timestamp.
                bool placedInMelee = _trapCastInMelee.Remove(ev.AbilityKey, out var atPlacement)
                    ? atPlacement : IsMelee(now) || _lastMeleeDepartureAt == now;
                if (request.TriggerTraps && (request.TriggerRangedTraps || placedInMelee))
                    schedule(now + request.TrapTriggerDelaySeconds, "hunter-trap-activate-" + ev.AbilityKey, gen);
            }
            if (ev.Type == CombatEventType.Damage && ev.SourceActorKey == hunter.Key &&
                ev.TargetActorKey is not null && ev.Amount > 0m) {
                // Damage removes Freezing Trap's incapacitation. Frost slow remains.
                var recipient = ctx.GetActor(ev.TargetActorKey);
                if (recipient is not null)
                    aura.RemoveAuras(ctx, recipient, "hunter-frozen", hunter.Key, "broken by damage");
                if (ev.AbilityKey == "strider-kick")
                    status(hunter,"hunter-strider-speed",3m,"strider-kick");
                if (ev.AbilityKey == "wing-clip") {
                    status(ctx.GetActor(ev.TargetActorKey)!,"hunter-wing-clip-slow",10m,"wing-clip");
                    var chance = talents.Rank("improved-wing-clip") switch {
                        1 => 7m, 2 => 14m, 3 => 20m, _ => 0m
                    };
                    if ((decimal)ctx.Random.NextDouble()*100m < chance)
                        status(ctx.GetActor(ev.TargetActorKey)!,"hunter-wing-clip-root",5m,"wing-clip");
                }
                if (ev.AbilityKey == "mongoose-bite" && talents.Rank("lacerating-strikes") > 0) {
                    _bleedTickValue = ev.Amount.Value * .40m / 7m;
                    ++_bleedGeneration;
                    for (int i=1;i<=7;i++)
                        schedule(now + i*3m, "hunter-lacerate-tick", _bleedGeneration,
                            ev.TargetActorKey, _bleedTickValue);
                }
                // Expose Prey can proc on melee/ranged hits on the marked enemy; periodic
                // effects and trap ticks are not attacks for this talent.
                if (talents.Rank("expose-prey") > 0 && !ev.IsPeriodic &&
                    ev.AbilityKey is not ("explosive-trap" or "immolation-trap" or "lacerating-strikes") &&
                    ctx.GetActor(ev.TargetActorKey)?.ActiveAuras.Any(a =>
                        a.Definition.Key == "hunters-mark" && a.IsActiveAt(now)) == true &&
                    (decimal)ctx.Random.NextDouble()*100m < 5m*talents.Rank("expose-prey")) {
                    hunter.GrantReactiveOpportunity("hunter-mongoose-ready", ev.TargetActorKey, now, 10m);
                    ExposePreyProcs++;
                    ctx.EmitEvent(new CombatEvent { TimeSeconds = now,
                        Type = CombatEventType.ReactiveOpportunityGranted,
                        SourceActorKey = hunter.Key, TargetActorKey = ev.TargetActorKey,
                        AbilityKey = "mongoose-bite", Description = "Expose Prey: Mongoose Bite activated." });
                }
                // Resourcefulness is only caused by critical melee/ranged abilities,
                // never auto attacks, traps, or periodic ticks (Oct 16 update).
                if (ev.IsCritical && !ev.IsPeriodic && talents.Rank("resourcefulness") > 0 &&
                    ev.AbilityKey is not ("melee-auto" or "offhand-auto" or "auto-shot" or "explosive-trap" or
                        "immolation-trap" or "lacerating-strikes") &&
                    (decimal)ctx.Random.NextDouble()*100m < 50m*talents.Rank("resourcefulness")) {
                    _resourcefulExpires = now + 30m;
                    ResourcefulnessProcs++;
                    status(hunter, "hunter-resourcefulness", 30m, "resourcefulness");
                    if (!_resourceTickPending) {
                        _resourceTickPending = true;
                        schedule(now + 1m,"hunter-resourcefulness-regen");
                    }
                }
            }
            if (ev.Type != CombatEventType.RotationDecision || !ev.IsInternal || ev.SourceActorKey != hunter.Key)
                return;
            if (ev.EffectKey == "hunter-phase-ranged") {
                SwitchToRanged(now);
                return;
            }
            if (ev.EffectKey == "hunter-weave-evaluate") {
                schedule(now + .5m, "hunter-weave-evaluate");
                // On cleave, Multi-Shot is the reason to leave melee. Do not
                // require Aimed Shot to be enabled for those excursions.
                var desiredRangedAbility = request.AdditionalTargets > 0 && request.UseMultiShot
                    ? "multi-shot"
                    : request.UseAimedShot ? "aimed-shot" : null;
                if (desiredRangedAbility is null)
                    return;
                var rangedAbility = hunter.Abilities[desiredRangedAbility];
                var rangedManaCost = rangedAbility.Definition.ResourceCosts
                    .Where(cost => cost.ResourceKey == "mana").Sum(cost => cost.Amount);
                if (_weaveInTransit || !IsMelee(now) ||
                    now - _weaveLastReturned < request.WeaveMinimumMeleeSeconds ||
                    hunter.Resources["mana"].Current < rangedManaCost + request.WeaveManaReserve ||
                    now + request.WeaveTravelSeconds * 2m + 5m >= request.DurationSeconds ||
                    !rangedAbility.IsReady(now) ||
                    !hunter.IsSharedCooldownReady("hunter-aimed-multi", now))
                    return;
                _weaveInTransit = true;
                WeaveExcursions++;
                SwitchToRanged(now);
                schedule(now + request.WeaveTravelSeconds + 5m, "hunter-weave-return");
                return;
            }
            if (ev.EffectKey == "hunter-weave-arrive-ranged") {
                ApplyStance("hunter-ranged-stance", now);
                autos.Start(ctx, hunter.Key, boss.Key, rangedAuto, .5m);
                return;
            }
            if (ev.EffectKey == "hunter-weave-return") {
                SwitchToMelee(now);
                return;
            }
            if (ev.EffectKey == "hunter-weave-arrive-melee") {
                ApplyStance("hunter-melee-stance", now);
                autos.Start(ctx, hunter.Key, boss.Key, meleeAuto);
                if (offhandAuto is not null) autos.Start(ctx, hunter.Key, boss.Key, offhandAuto);
                _weaveLastReturned = now;
                _weaveInTransit = false;
                return;
            }
            if (ev.EffectKey == "hunter-scripted-avoidance") {
                ScriptedAvoids++;
                var isDodge = ScriptedAvoids % 2 == 1;
                if (isDodge || talents.Rank("counterattack") > 0)
                    hunter.GrantReactiveOpportunity(isDodge ? "hunter-mongoose-ready" : "hunter-counterattack-ready",
                        boss.Key, now, isDodge ? 10m : 5m);
                ctx.EmitEvent(new CombatEvent { TimeSeconds = now,
                    Type = CombatEventType.ReactiveOpportunityGranted,
                    SourceActorKey = hunter.Key, TargetActorKey = boss.Key,
                    AbilityKey = isDodge ? "mongoose-bite" : "counterattack",
                    ResultKey = isDodge ? "dodge" : "parry",
                    Description = "Scripted boss attack was " + (isDodge ? "dodged" : "parried") + "." });
                return;
            }
            if (ev.EffectKey == "hunter-resourcefulness-regen") {
                _resourceTickPending = false;
                if (now >= _resourcefulExpires) return;
                if (now < _lastManaSpent + 5m) {
                    var mana = hunter.Resources["mana"];
                    mana.AdvanceTo(now);
                    var before = mana.Current;
                    mana.Gain(t.SpiritRegenPerSecond * .5m);
                    var actual = mana.Current - before;
                    ResourcefulnessMana += actual;
                    if (actual > 0m) ctx.EmitEvent(new CombatEvent { TimeSeconds = now,
                        Type = CombatEventType.ResourceChanged, SourceActorKey = hunter.Key,
                        TargetActorKey = hunter.Key, AbilityKey = "resourcefulness",
                        ResourceKey = "mana", Amount = actual, ResourceTotalAfter = mana.Current,
                        Description = "Resourcefulness restored Mana during casting." });
                }
                _resourceTickPending = now + 1m < request.DurationSeconds;
                if (_resourceTickPending) schedule(now + 1m,"hunter-resourcefulness-regen");
                return;
            }
            if (ev.EffectKey == "hunter-lacerate-tick") {
                if (ev.EncounterSequenceHitNumber != _bleedGeneration) return;
                BleedTicks++;
                damage("lacerating-strikes", "lacerating-bleed-tick", ev.TargetActorKey ?? boss.Key,
                    ev.Amount ?? 0m, ev.Amount ?? 0m, "physical", periodic:true);
                return;
            }
            if (ev.EffectKey is "hunter-trap-fire-tick" or "hunter-trap-immolation-tick") {
                // Applied DoTs continue after a new trap has been placed.
                var ability = ev.EffectKey == "hunter-trap-fire-tick" ? "explosive-trap" : "immolation-trap";
                damage(ability, "trap-fire-periodic", ev.TargetActorKey ?? boss.Key,
                    ev.Amount ?? 0m, ev.Amount ?? 0m, "fire", periodic:true);
                return;
            }
            if (ev.EffectKey?.StartsWith("hunter-trap-activate-", StringComparison.Ordinal) != true)
                return;
            var key = ev.EffectKey["hunter-trap-activate-".Length..];
            var fireTrap = key is "immolation-trap" or "explosive-trap";
            if (ev.EncounterSequenceHitNumber != (fireTrap ? _fireGeneration : _frostGeneration)) return;
            TrapTriggers++;
            var victims = key == "explosive-trap" || key == "frost-trap" ?
                ctx.Actors.Values.Where(a => a.TeamKey == "enemy" && a.IsAlive).ToArray() : new[] { boss };
            // Entrapment affects every victim of either Fire or Frost trap.
            if (talents.Rank("entrapment") > 0) {
                foreach (var target in victims) {
                    ApplyEntrapment(target, now);
                }
            }
            if (key == "explosive-trap") {
                foreach (var target in victims) {
                    var buff = 1m + .15m*talents.Rank("clever-traps");
                    var ap = t.RangedAttackPower * t.TrapAttackPowerCoefficient;
                    damage(key,"explosive-impact",target.Key,
                        (t.ExplosiveTrapMinDamage+ap)*buff, (t.ExplosiveTrapMaxDamage+ap)*buff,"fire");
                    for (int tick=1;tick<=5;tick++)
                        schedule(now+4m*tick,"hunter-trap-fire-tick",ev.EncounterSequenceHitNumber,
                            target.Key, t.ExplosiveTrapDotTotalDamage*buff/5m);
                }
            } else if (key == "immolation-trap") {
                var buff = 1m + .15m*talents.Rank("clever-traps");
                for (int tick=1;tick<=5;tick++)
                    schedule(now+3m*tick,"hunter-trap-immolation-tick",ev.EncounterSequenceHitNumber,
                        boss.Key, (t.ImmolationTrapTotalDamage+t.RangedAttackPower*t.TrapAttackPowerCoefficient)*buff/5m);
            } else {
                foreach (var target in victims) {
                    decimal duration = key == "freezing-trap" ? 20m : 30m;
                    duration *= 1m + .15m*talents.Rank("clever-traps");
                    status(target,key == "freezing-trap" ? "hunter-frozen" : "hunter-frost-slow",
                        duration,key);
                }
            }
        }
    }
}
