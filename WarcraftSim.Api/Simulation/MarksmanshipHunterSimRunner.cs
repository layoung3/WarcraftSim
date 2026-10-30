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
/// CP157: first Hunter specialization. Fixed-position, no-pet Marksmanship uses
/// the shared ability, auto-attack, aura, GCD, cooldown and summary engines.
/// </summary>
public sealed class MarksmanshipHunterSimRunner
{
    public MarksmanshipHunterSimResponse Run(MarksmanshipHunterSimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request=ForeverEquipmentStatBridge.Resolve(request);
        if (request.Talents is null || request.Tuning is null ||
            request.DurationSeconds is <= 0m or > 1200m ||
            request.TargetMaximumHealth <= 0m || request.AdditionalTargets is < 0 or > 8 ||
            request.AdditionalTargetMaximumHealth is <= 0m ||
            request.PetTuning is null || request.PetTalents is null ||
            request.PetAbilityPriority is null ||
            request.PetEngagementDelaySeconds < 0m ||
            request.PetIncomingHitDamage < 0m ||
            request.PetIncomingHitEverySeconds <= 0m)
            throw new ArgumentException("Invalid level-60 Marksmanship pet encounter configuration.");
        request.Talents.Validate();
        request.Tuning.Validate();
        request.PetTuning.Validate();
        request.PetTalents.Validate();
        if(request.PetTalents.Ranks.Values.Sum()>request.Talents.BeastMasteryPointsSpent)
            throw new ArgumentException("Off-tree pet talents exceed BM point allocation.");
        var family=request.HasPet?ForeverHunterPetFamilies.Get(request.PetFamily):null;
        var t = request.Tuning;
        var talents = request.Talents;
        var ap = t.RangedAttackPower + t.Intellect * talents.Rank("careful-aim") * .20m +
            (talents.Rank("trueshot-aura") > 0 ? t.TrueshotAttackPower : 0m);
        var stats = new StatCollection();
        stats.Set(ForeverCombatStatKeys.AttackPower, ap);
        stats.Set(ForeverCombatStatKeys.HastePercent, 0m);
        stats.Set("companion-attack-power",ap);
        stats.Set("ranged-hit-bonus-percent",request.PetTuning.HunterRangedHitBonusPercent);
        stats.Set("ranged-critical-chance-percent",t.CriticalChancePercent+talents.Rank("lethal-attacks"));
        stats.Set("melee-critical-chance-percent",request.PetTuning.HunterMeleeCriticalPercent);
        stats.Set(ForeverCombatStatKeys.DodgeReductionPercent,request.PetTuning.HunterExpertisePercent);
        stats.Set(ForeverCombatStatKeys.ParryReductionPercent,request.PetTuning.HunterExpertisePercent);
        var context = new SimulationContext(new SimulationRunOptions {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            PrimaryActorKey = "hunter", CaptureTimeline = request.CaptureTimeline,
            MaximumProcessedEvents = 50000, CancellationToken = cancellationToken
        });
        var hunter = new SimulationActorState {
            Key = "hunter", Name = "Marksmanship Hunter", TeamKey = "raid", Level = 60,
            MaximumHealth = t.MaximumHealth, Stats = stats
        };
        hunter.InitializeHealth(hunter.MaximumHealth);
        hunter.AddResource(new ResourceState("mana", t.MaximumMana, t.MaximumMana));
        var boss = new SimulationActorState {
            Key = "target", Name = "Level 63 training boss", TeamKey = "enemy",
            Level = 63, MaximumHealth = request.TargetMaximumHealth
        };
        boss.InitializeHealth(boss.MaximumHealth);
        context.AddActor(hunter);
        context.AddActor(boss);
        for (var i = 1; i <= request.AdditionalTargets; i++) {
            var extra = new SimulationActorState {
                Key = $"add-{i}", Name = $"Additional enemy {i}", TeamKey = "enemy",
                Level = 63, MaximumHealth = request.AdditionalTargetMaximumHealth ?? request.TargetMaximumHealth
            };
            extra.InitializeHealth(extra.MaximumHealth);
            context.AddActor(extra);
        }
        var petTuning=request.PetTuning;
        var petTarget=context.GetActor(request.PetTargetKey);
        if(request.HasPet && (petTarget is null || petTarget.TeamKey!="enemy"))
            throw new ArgumentException("Pet target must be a configured enemy actor.");
        SimulationActorState? pet=null;
        if(family is not null)
            pet=HunterCompanionComposition.Create(context,hunter,family,petTuning,
                request.PetTalents.Rank("endurance-training"),
                request.PetTalents.Rank("bestial-discipline"));
        foreach (var spell in ForeverMarksmanshipHunterSpellbook.Create(talents, t, ap,
            request.HasPet))
            hunter.AddAbility(spell);
        if(pet is not null)
        {
            var bmSpells=ForeverBeastMasteryHunterSpellbook.Hunter(
                request.PetTalents,petTuning);
            foreach(var key in new[]{"bestial-wrath","intimidation"})
                if(bmSpells.FirstOrDefault(a=>a.Key==key) is { } spell)
                    hunter.AddAbility(spell);
            if(request.UseRevivePet)
                hunter.AddAbility(bmSpells.Single(a=>a.Key=="revive-pet"));
        }
        var aura = new AuraManager();
        var marksmanship = new HunterReactiveProcessor("hunter", hunter, t, talents);
        var aspects = new HunterAspectProcessor(hunter, aura);
        var executor = new AbilityExecutor(aura, new HunterRolls(t, talents, marksmanship, petTuning,request.PetTalents),
            new HunterArmor(t.TargetArmor, marksmanship, talents, family,request.PetTalents));
        var auto = new AutoAttackProcessor(executor, new ForeverAutoAttackTimingProvider());
        marksmanship.AttachAuto(auto);
        var petRuntime=pet is not null && family is not null ?
            new HunterSharedCompanionRuntime(hunter,pet,petTarget!,family,petTuning,
                aura,auto,HunterCompanionComposition.Melee(family,petTuning),
                request.DurationSeconds,request.UsePetSpecial,
                request.PetEngagementDelaySeconds/
                    (request.PetTalents.Rank("bestial-swiftness")>0?1.3m:1m),
                request.PetIncomingHitDamage,request.PetIncomingHitEverySeconds,
                request.UseRevivePet,request.PetTalents.Rank("improved-revive-pet"),
                request.PetTalents.Rank("frenzy"),
                request.PetTalents.Rank("spirit-bond")) : null;
        var autoEffect = new AbilityEffectDefinition {
            Key = "ranged-auto-impact", EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy, ResolutionType = CombatResolutionTypes.Melee,
            SchoolKey = "physical", MitigationType = DamageMitigationTypes.Armor,
            CanMiss = true, CanCrit = true,
            MinimumValue = t.WeaponMinDamage, MaximumValue = t.WeaponMaxDamage,
            ScalingStatKey = ForeverCombatStatKeys.AttackPower,
            ScalingCoefficient = t.WeaponSpeedSeconds / 14m
        };
        var autoShot = new AutoAttackDefinition {
            Key = "auto-shot", Name = "Auto Shot", WeaponHandKey = WeaponHandKeys.MainHand,
            SwingIntervalSeconds = t.WeaponSpeedSeconds,
            DamageMultiplier = talents.Rank("lone-wolf") > 0 && !request.HasPet ? 1.20m : 1m,
            DamageEffect = autoEffect
        };
        var regen = new FiveSecondManaRegenerationProcessor("hunter",
            t.SpiritRegenPerSecond, t.ManaPerFiveSeconds, 0m);
        var rotation = BuildRotation(request, hunter);
        if(pet is not null && request.UseRevivePet)
            rotation.Entries.Insert(0,new RotationEntry {
                Priority=-1,AbilityKey="revive-pet"
            });
        var priority = new PriorityRotationExecutor(rotation, "hunter", "target", executor,
            autoAttackProcessor: auto, reactToTargetStateChanges: true);
        // Owner and pet share the same executor, event clock, autos and auras.
        var processors=new List<ICombatEventProcessor> {
            executor,aura,aspects,auto,marksmanship,regen
        };
        if(petRuntime is not null && pet is not null && family is not null)
        {
            processors.Add(petRuntime);
            processors.Add(new PriorityRotationExecutor(
                HunterCompanionComposition.Rotation(pet,family,request.UsePetSpecial,
                    request.UsePetBite,request.UsePetClaw,request.PetAbilityPriority),
                pet.Key,petTarget!.Key,executor,autoAttackProcessor:auto,
                reactToTargetStateChanges:true));
        }
        processors.Add(new ThreatManager(request.HasPet ?
            new HunterOwnerAndPetThreatResolver() : new SoloDamageThreatResolver("hunter")));
        processors.Add(priority);
        var engine = new SimulationEngine(processors);
        regen.Initialize(context);
        var run = engine.Run(context, started => {
            // Stationary shooting stance: Forever requires 0.5 seconds standing still.
            if (!auto.Start(started, "hunter", "target", autoShot, .5m))
                throw new InvalidOperationException("Hunter Auto Shot did not start.");
            petRuntime?.Start(started,executor);
            if (talents.Rank("trueshot-aura") > 0)
                aura.ApplyAura(started, hunter, new AuraDefinition {
                    Key = "hunter-trueshot-aura", Name = "Trueshot Aura", DurationSeconds = 3600m
                }, hunter.Key, "trueshot-aura", "precast-raid-aura");
        });
        var report = run.Summary.ActorSummaries["hunter"];
        var petReport=pet is not null?run.Summary.ActorSummaries[pet.Key]:null;
        var petDamage=petReport?.DamageDone??0m;
        var breakdown=new Dictionary<string,decimal>(report.DamageDoneByAbility,
            StringComparer.OrdinalIgnoreCase);
        if(petReport is not null)
            foreach(var pair in petReport.DamageDoneByAbility)
                breakdown["pet:"+pair.Key]=pair.Value;
        run.CustomMetrics["hunter-serpent-sting-applications"] = marksmanship.SerpentApplications;
        run.CustomMetrics["hunter-rapid-fire-casts"] = marksmanship.RapidFireCasts;
        run.CustomMetrics["hunter-rapid-recuperation-mana"] = marksmanship.RecuperationMana;
        run.CustomMetrics["hunter-rapid-killing-procs"] = marksmanship.RapidKillingProcs;
        run.CustomMetrics["hunter-rapid-killing-consumes"] = marksmanship.RapidKillingConsumes;
        run.CustomMetrics["hunter-no-pet-lone-wolf"] = talents.Rank("lone-wolf") > 0 &&
            (!request.HasPet || pet is { IsAlive:false }) ? 1m : 0m;
        run.CustomMetrics["hunter-hawk-applications"] = aspects.HawkApplications;
        return new MarksmanshipHunterSimResponse {
            Dps = (report.DamageDone+petDamage) / request.DurationSeconds,
            DamageDone = report.DamageDone+petDamage,HunterDamage=report.DamageDone,
            PetDamage=petDamage,PetFocus=pet?.Resources["focus"].Current??0m,
            PetThreat=pet is null?0m:petTarget!.ThreatTable.GetThreat(pet.Key),
            PetUptimeSeconds=petRuntime?.UptimeSeconds??0m,
            PetDied=petRuntime?.Died??false,EndingMana = hunter.Resources["mana"].Current,
            MaximumMana = t.MaximumMana, RangedAttackPower = hunter.Stats.Get(ForeverCombatStatKeys.AttackPower),
            RapidFireCasts = marksmanship.RapidFireCasts,
            SerpentStingApplications = marksmanship.SerpentApplications,
            DamageByAbility = breakdown, Run = run,
            ModelingNotes = [
                "CP169 optional shared-engine pet family, independent damage/focus/death; movement geometry is deferred.",
                "CP167 Hawk: opt-in +120 live ranged Attack Power and 120 Mana via exclusive source aura. Volley: 6x Arcane area pulses on configured fixed enemies; 8-yard geometry remains unmodeled.",
                "Aimed Shot and Multi-Shot use the shared engine cooldown; Arcane Shot is independent.",
                "Lone Wolf, Careful Aim, level-60 Trueshot Aura, improved stings, Barrage, Sniper Shot, Rapid Fire, Rapid Killing, and 15-second conditional recuperation are connected.",
                "Verified Forever data: Aimed Shot 2 seconds, Sniper Shot rank 3 (295 bonus/365 Mana), and level-60 Trueshot Aura 50 AP; remaining coefficients, Mana and encounter mechanics are preview tuning.",
                "Rapid Killing is supported on target deaths; scripted kill credit and raid member pet kills need future shared-encounter integration. Ammo, traps, raid-wide Trueshot sharing and range remain deferred."
            ]
        };
    }

    // A death while the hunter's Sting remains active grants Rapid Killing even
    // when a different party member lands the killing blow. Do not count a
    // different hunter's Sting or an expired application as ownership.
    public static bool QualifiesRapidKillingDeath(SimulationContext context,
        CombatEvent ev, string hunterKey)
    {
        if(ev.Type!=CombatEventType.ActorDied ||
           ev.TargetActorKey is not { } dead || dead==hunterKey ||
           context.GetActor(dead) is not { TeamKey: "enemy" } defeated)
            return false;
        return ev.SourceActorKey==hunterKey ||
            defeated.ActiveAuras.Any(a=>a.Definition.Key=="serpent-sting" &&
                a.SourceActorKey==hunterKey &&
                a.IsActiveAt(context.CurrentTimeSeconds));
    }

    private static RotationProfile BuildRotation(MarksmanshipHunterSimRequest req, SimulationActorState hunter)
    {
        var rotation = new RotationProfile {
            Name = "CP157 Marksmanship stationary Lone Wolf",
            ClassKey = "hunter", RulesetKey = "wow-forever", SimulationType = SimulationType.Dps
        };
        var order = 0;
        void add(bool enabled, string key, params RotationConditionDefinition[] conditions) {
            if (!enabled || !hunter.Abilities.ContainsKey(key)) return;
            rotation.Entries.Add(new RotationEntry {
                Priority = ++order, AbilityKey = key, Conditions = conditions.ToList()
            });
        }
        if (req.CustomPriority is null)
            throw new ArgumentException("Marksmanship custom priority cannot be null.");
        if(req.CustomPriority.Count>0)
        {
            foreach(var key in req.CustomPriority.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (key is not ("aspect-of-the-hawk" or "aspect-of-the-beast" or
                    "rapid-fire" or "bestial-wrath" or "intimidation" or
                    "serpent-sting" or "sniper-shot" or
                    "multi-shot" or "aimed-shot" or "arcane-shot" or "volley") ||
                    !hunter.Abilities.ContainsKey(key))
                    throw new ArgumentException($"Unsupported Marksmanship priority: {key}");
                var conditions=key switch {
                    "serpent-sting" => new[] {new RotationConditionDefinition {
                        ConditionType=RotationConditionTypes.TargetAuraMissing,
                        Key="serpent-sting",RequireSourceOwnership=true
                    }},
                    "aspect-of-the-hawk" => new[] {new RotationConditionDefinition {
                        ConditionType=RotationConditionTypes.SourceAuraMissing,
                        Key="hunter-aspect-hawk"
                    }},
                    "aspect-of-the-beast" => new[] {new RotationConditionDefinition {
                        ConditionType=RotationConditionTypes.SourceAuraMissing,
                        Key="hunter-aspect-beast"
                    }},
                    _ => Array.Empty<RotationConditionDefinition>()
                };
                rotation.Entries.Add(new RotationEntry {
                    Priority=++order,AbilityKey=key,Conditions=conditions.ToList()
                });
            }
            return rotation;
        }
        add(req.UseAspectOfTheHawk, "aspect-of-the-hawk", new RotationConditionDefinition {
            ConditionType = RotationConditionTypes.SourceAuraMissing, Key = "hunter-aspect-hawk"
        });
        add(req.UseRapidFire, "rapid-fire");
        add(req.HasPet && req.PetTalents.Rank("bestial-wrath")>0,"bestial-wrath");
        add(req.HasPet && req.PetTalents.Rank("intimidation")>0,"intimidation");
        add(req.UseVolley && req.AdditionalTargets > 0, "volley");
        add(req.UseSerpentSting, "serpent-sting", new RotationConditionDefinition {
            ConditionType = RotationConditionTypes.TargetAuraMissing, Key = "serpent-sting"
        });
        add(req.UseSniperShot, "sniper-shot");
        // Multi-shot is only considered when the request has multiple targets.
        add(req.UseMultiShot && req.AdditionalTargets > 0, "multi-shot");
        add(req.UseAimedShot, "aimed-shot");
        add(req.UseArcaneShot, "arcane-shot");
        return rotation;
    }

    private sealed class HunterRolls(ForeverMarksmanshipHunterTuning t,
        ForeverMarksmanshipHunterTalents talents,HunterReactiveProcessor procs,
        ForeverBeastMasteryHunterTuning petTuning,
        ForeverBeastMasteryHunterTalents petTalents) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState source,
            SimulationActorState target, AbilityDefinition spell, AbilityEffectDefinition effect) =>
            Resolve(ctx, source, target, spell, effect, CombatEffectDeliveryType.Direct);

        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState source,
            SimulationActorState target, AbilityDefinition spell, AbilityEffectDefinition effect,
            CombatEffectDeliveryType delivery)
        {
            if(source.Key=="hunter-pet")
                return HunterCompanionComposition.Resolve(ctx,source,target,effect,petTuning,
                    petTalents.Rank("ferocity"));
            // Consume on the Shot's first attack roll, including a miss. The damage
            // multiplier remains available to every target of the same Multi-Shot.
            procs.ConsumeRapidKillingForShot(ctx, spell.Key);
            if (effect.CanMiss && (decimal)ctx.Random.NextDouble() * 100m >= t.HitChancePercent)
                return CombatRollResult.Miss();
            if (!effect.CanCrit) return CombatRollResult.Hit();
            var crit = Math.Min(100m, t.CriticalChancePercent + talents.Rank("lethal-attacks"));
            if ((decimal)ctx.Random.NextDouble() * 100m >= crit) return CombatRollResult.Hit();
            // Forever Mortal Shots covers ranged spells and Serpent Sting, not only Physical damage.
            var isRangedHunter = (spell.ClassKey == "hunter" &&
                spell.SpecializationKey == "marksmanship") ||
                effect.Key == "ranged-auto-impact";
            var bonus = isRangedHunter ? talents.Rank("mortal-shots") * .06m : 0m;
            return CombatRollResult.Critical(2m + bonus);
        }
    }

    private sealed class HunterArmor(decimal armor,HunterReactiveProcessor procs,
        ForeverMarksmanshipHunterTalents talents,ForeverHunterPetFamily? petFamily,
        ForeverBeastMasteryHunterTalents petTalents) : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect, decimal rawAmount)
        {
            // The kill-empowered next Shot affects every target hit by the same cast.
            // Cast completion is processed before its impact events at the same timestamp.
            rawAmount *= procs.ShotMultiplier(ability.Key, context.CurrentTimeSeconds);
            if(source.Key=="hunter-pet" && petFamily is not null)
                rawAmount*=(1m+petFamily.DamageModifier)*
                    (1m+.03m*petTalents.Rank("unleashed-fury"));
            if(source.Key=="hunter-pet" &&
               source.ActiveAuras.Any(a=>a.Definition.Key=="bestial-wrath" &&
                   a.SourceActorKey=="hunter" && a.IsActiveAt(context.CurrentTimeSeconds)))
                rawAmount*=1.5m;
            if(petFamily is not null &&
               context.GetActor("hunter-pet") is { IsAlive:true } &&
               source.Key is ("hunter" or "hunter-pet"))
                rawAmount*=1m+.01m*petTalents.Rank("focused-fire");
            // The permanent Lone Wolf spellbook and Auto Shot multiplier
            // already cover requests that start without a pet. When the
            // selected pet dies mid-fight, grant the same bonus dynamically;
            // reviving it removes the bonus on the very next owner hit.
            if(source.Key=="hunter" && petFamily is not null &&
               context.GetActor("hunter-pet") is { IsAlive:false } &&
               talents.Rank("lone-wolf")>0)
                rawAmount*=1.20m;
            // Ranged Weapon Specialization is a ranged-weapon damage modifier,
            // not a blanket bonus to magical Stings, Arcane Shot or Volley.
            if (effect.SchoolKey == "physical" &&
                ability.Key is ("auto-shot" or "aimed-shot" or "multi-shot" or "sniper-shot"))
                rawAmount *= 1m + .01m*talents.Rank("ranged-weapon-specialization");
            if (effect.MitigationType != DamageMitigationTypes.Armor)
                return DamageMitigationResult.Unmitigated(rawAmount);
            var reduction = ForeverArmorMitigationMath.ClampArmorReductionFraction(
                armor / (armor + 400m + 85m * Math.Max(1, source.Level)));
            var actual = rawAmount * (1m - reduction);
            return new DamageMitigationResult {
                RawAmount = rawAmount, FinalAmount = actual,
                MitigatedAmount = rawAmount - actual, ReductionPercent = reduction * 100m,
                DefenseValue = armor
            };
        }
    }

    private sealed class HunterReactiveProcessor(string hunterKey, SimulationActorState hunter,
        ForeverMarksmanshipHunterTuning t, ForeverMarksmanshipHunterTalents talents) : ICombatEventProcessor
    {
        private AutoAttackProcessor? _autos;
        public void AttachAuto(AutoAttackProcessor autos) => _autos = autos;
        private decimal _rapidEnds;
        private decimal _recuperationEnds;
        private decimal _recuperationEmpoweredEnds;
        private bool _recuperationTickScheduled;
        private decimal _lastManaSpendAt = decimal.MinValue;
        private decimal _rapidKillingExpires;
        private decimal _empoweredShotAt = -1m;
        private string? _empoweredShotKey;
        public int RapidFireCasts { get; private set; }
        public int SerpentApplications { get; private set; }
        public int RapidKillingProcs { get; private set; }
        public int RapidKillingConsumes { get; private set; }
        public decimal RecuperationMana { get; private set; }

        // Exact timestamp guards keep an empowered Multi-Shot applied to all three
        // victims, without empowering a later unrelated cast of the same ability.
        public decimal ShotMultiplier(string key, decimal at) =>
            _empoweredShotAt == at &&
            string.Equals(_empoweredShotKey, key, StringComparison.OrdinalIgnoreCase)
                ? 1m + .10m * talents.Rank("rapid-killing") : 1m;

        public void ConsumeRapidKillingForShot(SimulationContext ctx, string key)
        {
            if (!IsShot(key) || _rapidKillingExpires <= ctx.CurrentTimeSeconds ||
                (_empoweredShotAt == ctx.CurrentTimeSeconds &&
                 string.Equals(_empoweredShotKey, key, StringComparison.OrdinalIgnoreCase)))
                return;
            _rapidKillingExpires = 0m;
            _empoweredShotAt = ctx.CurrentTimeSeconds;
            _empoweredShotKey = key;
            RapidKillingConsumes++;
            if (talents.Rank("rapid-recuperation") > 0 &&
                _recuperationEnds > ctx.CurrentTimeSeconds)
                _recuperationEmpoweredEnds = _recuperationEnds;
            ctx.EmitEvent(new CombatEvent {
                TimeSeconds = ctx.CurrentTimeSeconds,
                Type = CombatEventType.ReactiveOpportunityGranted,
                SourceActorKey = hunterKey, TargetActorKey = hunterKey,
                AbilityKey = "rapid-killing", EffectKey = "hunter-rapid-killing-consumed",
                Description = "Rapid Killing was consumed by " + key + "."
            });
        }

        private static bool IsShot(string? key) => key is
            "aimed-shot" or "multi-shot" or "arcane-shot" or "sniper-shot";

        public void Process(SimulationContext ctx, CombatEvent ev)
        {
            if (ev.SourceActorKey == hunterKey && ev.Type == CombatEventType.ResourceChanged &&
                ev.ResourceKey == "mana" && ev.Amount is < 0m)
                _lastManaSpendAt = ctx.CurrentTimeSeconds;

            if (ev.SourceActorKey == hunterKey && ev.Type == CombatEventType.AuraApplied &&
                ev.AbilityKey == "rapid-fire") {
                RapidFireCasts++;
                _rapidEnds = ctx.CurrentTimeSeconds + t.RapidFireDurationSeconds;
                var previous = hunter.Stats.Get(ForeverCombatStatKeys.HastePercent);
                var updated = previous + t.RapidFireHastePercent;
                hunter.Stats.Set(ForeverCombatStatKeys.HastePercent, updated);
                _autos?.RescheduleForHasteChange(ctx, hunterKey, previous, updated);
            }
            if (ev.SourceActorKey == hunterKey && ev.Type == CombatEventType.AuraRemoved &&
                ev.AbilityKey == "rapid-fire" && ctx.CurrentTimeSeconds >= _rapidEnds)
            {
                var previous = hunter.Stats.Get(ForeverCombatStatKeys.HastePercent);
                var updated = Math.Max(0m, previous - t.RapidFireHastePercent);
                hunter.Stats.Set(ForeverCombatStatKeys.HastePercent, updated);
                _autos?.RescheduleForHasteChange(ctx, hunterKey, previous, updated);
            }

            if (talents.Rank("rapid-killing") > 0 &&
                QualifiesRapidKillingDeath(ctx,ev,hunterKey)) {
                _rapidKillingExpires = ctx.CurrentTimeSeconds + 20m;
                RapidKillingProcs++;
                ctx.EmitEvent(new CombatEvent {
                    TimeSeconds = ctx.CurrentTimeSeconds,
                    Type = CombatEventType.ReactiveOpportunityGranted,
                    SourceActorKey = hunterKey, TargetActorKey = hunterKey,
                    AbilityKey = "rapid-killing", EffectKey = "hunter-rapid-killing-ready",
                    Description = "Rapid Killing enabled the next Shot for 20 seconds."
                });
            }

            if (ev.SourceActorKey == hunterKey && ev.Type == CombatEventType.AuraApplied &&
                ev.AbilityKey == "serpent-sting" && ev.EffectKey == "serpent-sting-tick") {
                SerpentApplications++;
                if (talents.Rank("rapid-recuperation") > 0) {
                    _recuperationEnds = ctx.CurrentTimeSeconds + 15m;
                    // A new Sting refreshes the window without multiplying scheduled ticks.
                    _recuperationEmpoweredEnds = 0m;
                    if (!_recuperationTickScheduled) {
                        _recuperationTickScheduled = true;
                        ScheduleRecuperation(ctx, ctx.CurrentTimeSeconds + 1m);
                    }
                }
            }
            if (ev.Type == CombatEventType.RotationDecision && ev.IsInternal &&
                ev.SourceActorKey == hunterKey && ev.EffectKey == "hunter-recuperation-tick") {
                _recuperationTickScheduled = false;
                if (ctx.CurrentTimeSeconds >= _recuperationEnds) return;
                // Spirit only continues inside the 5-second rule. FiveSecondManaRegenerationProcessor
                // already handles ordinary out-of-casting Spirit and MP5 separately.
                var mana = hunter.Resources["mana"];
                if (ctx.CurrentTimeSeconds < _lastManaSpendAt + 5m) {
                    mana.AdvanceTo(ctx.CurrentTimeSeconds);
                    var fraction = .25m * talents.Rank("rapid-recuperation") *
                        (_recuperationEmpoweredEnds > ctx.CurrentTimeSeconds ? 2m : 1m);
                    var before = mana.Current;
                    mana.Gain(t.SpiritRegenPerSecond * fraction);
                    var gained = mana.Current - before;
                    RecuperationMana += gained;
                    if (gained > 0m) ctx.EmitEvent(new CombatEvent {
                        TimeSeconds = ctx.CurrentTimeSeconds,
                        Type = CombatEventType.ResourceChanged,
                        SourceActorKey = hunterKey, TargetActorKey = hunterKey,
                        AbilityKey = "rapid-recuperation", ResourceKey = "mana", Amount = gained,
                        ResourceTotalAfter = mana.Current,
                        Description = "Rapid Recuperation restored Mana during casting."
                    });
                }
                var next = ctx.CurrentTimeSeconds + 1m;
                if (next < _recuperationEnds && next < ctx.Options.DurationSeconds) {
                    _recuperationTickScheduled = true;
                    ScheduleRecuperation(ctx, next);
                }
            }
        }

        private static void ScheduleRecuperation(SimulationContext ctx, decimal at) {
            if (at >= ctx.Options.DurationSeconds) return;
            ctx.ScheduleEvent(new CombatEvent {
                TimeSeconds = at, Type = CombatEventType.RotationDecision,
                SourceActorKey = "hunter", EffectKey = "hunter-recuperation-tick", IsInternal = true
            });
        }
    }
}
