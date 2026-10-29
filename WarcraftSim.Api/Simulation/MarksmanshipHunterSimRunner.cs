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
        if (request.Talents is null || request.Tuning is null ||
            request.DurationSeconds is <= 0m or > 1200m ||
            request.TargetMaximumHealth <= 0m || request.AdditionalTargets is < 0 or > 8 ||
            request.AdditionalTargetMaximumHealth is <= 0m ||
            request.HasPet)
            throw new ArgumentException("Invalid Hunter request: CP157 models fixed-position no-pet Marksmanship only.");
        request.Talents.Validate();
        request.Tuning.Validate();
        var t = request.Tuning;
        var talents = request.Talents;
        var ap = t.RangedAttackPower + t.Intellect * talents.Rank("careful-aim") * .20m +
            (talents.Rank("trueshot-aura") > 0 ? t.TrueshotAttackPower : 0m);
        var stats = new StatCollection();
        stats.Set(ForeverCombatStatKeys.AttackPower, ap);
        stats.Set(ForeverCombatStatKeys.HastePercent, 0m);
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
        foreach (var spell in ForeverMarksmanshipHunterSpellbook.Create(talents, t, ap))
            hunter.AddAbility(spell);
        var aura = new AuraManager();
        var marksmanship = new HunterReactiveProcessor("hunter", hunter, t, talents);
        var aspects = new HunterAspectProcessor(hunter, aura);
        var executor = new AbilityExecutor(aura, new HunterRolls(t, talents, marksmanship),
            new HunterArmor(t.TargetArmor, marksmanship));
        var auto = new AutoAttackProcessor(executor, new ForeverAutoAttackTimingProvider());
        marksmanship.AttachAuto(auto);
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
            DamageMultiplier = talents.Rank("lone-wolf") > 0 ? 1.20m : 1m,
            DamageEffect = autoEffect
        };
        var regen = new FiveSecondManaRegenerationProcessor("hunter",
            t.SpiritRegenPerSecond, t.ManaPerFiveSeconds, 0m);
        var rotation = BuildRotation(request, hunter);
        var priority = new PriorityRotationExecutor(rotation, "hunter", "target", executor,
            autoAttackProcessor: auto, reactToTargetStateChanges: true);
        // Reactive processor follows AuraManager so it sees applied/expired aura events.
        var engine = new SimulationEngine([executor, aura, aspects, auto, marksmanship, regen,
            new ThreatManager(new SoloDamageThreatResolver("hunter")), priority]);
        regen.Initialize(context);
        var run = engine.Run(context, started => {
            // Stationary shooting stance: Forever requires 0.5 seconds standing still.
            if (!auto.Start(started, "hunter", "target", autoShot, .5m))
                throw new InvalidOperationException("Hunter Auto Shot did not start.");
            if (talents.Rank("trueshot-aura") > 0)
                aura.ApplyAura(started, hunter, new AuraDefinition {
                    Key = "hunter-trueshot-aura", Name = "Trueshot Aura", DurationSeconds = 3600m
                }, hunter.Key, "trueshot-aura", "precast-raid-aura");
        });
        var report = run.Summary.ActorSummaries["hunter"];
        run.CustomMetrics["hunter-serpent-sting-applications"] = marksmanship.SerpentApplications;
        run.CustomMetrics["hunter-rapid-fire-casts"] = marksmanship.RapidFireCasts;
        run.CustomMetrics["hunter-rapid-recuperation-mana"] = marksmanship.RecuperationMana;
        run.CustomMetrics["hunter-rapid-killing-procs"] = marksmanship.RapidKillingProcs;
        run.CustomMetrics["hunter-rapid-killing-consumes"] = marksmanship.RapidKillingConsumes;
        run.CustomMetrics["hunter-no-pet-lone-wolf"] = talents.Rank("lone-wolf") > 0 ? 1m : 0m;
        run.CustomMetrics["hunter-hawk-applications"] = aspects.HawkApplications;
        return new MarksmanshipHunterSimResponse {
            Dps = report.DamageDone / request.DurationSeconds,
            DamageDone = report.DamageDone, EndingMana = hunter.Resources["mana"].Current,
            MaximumMana = t.MaximumMana, RangedAttackPower = hunter.Stats.Get(ForeverCombatStatKeys.AttackPower),
            RapidFireCasts = marksmanship.RapidFireCasts,
            SerpentStingApplications = marksmanship.SerpentApplications,
            DamageByAbility = new Dictionary<string, decimal>(report.DamageDoneByAbility,
                StringComparer.OrdinalIgnoreCase), Run = run,
            ModelingNotes = [
                "CP157 stationary, no-pet level-60 Marksmanship. Pet combat and movement/dead-zone checks are deferred.",
                "CP167 Hawk: opt-in +120 live ranged Attack Power and 120 Mana via exclusive source aura. Volley: 6x Arcane area pulses on configured fixed enemies; 8-yard geometry remains unmodeled.",
                "Aimed Shot and Multi-Shot use the shared engine cooldown; Arcane Shot is independent.",
                "Lone Wolf, Careful Aim, level-60 Trueshot Aura, improved stings, Barrage, Sniper Shot, Rapid Fire, Rapid Killing, and 15-second conditional recuperation are connected.",
                "Verified Forever data: Aimed Shot 2 seconds, Sniper Shot rank 3 (295 bonus/365 Mana), and level-60 Trueshot Aura 50 AP; remaining coefficients, Mana and encounter mechanics are preview tuning.",
                "Rapid Killing is supported on target deaths; scripted kill credit and raid member pet kills need future shared-encounter integration. Ammo, traps, raid-wide Trueshot sharing and range remain deferred."
            ]
        };
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
        add(req.UseAspectOfTheHawk, "aspect-of-the-hawk", new RotationConditionDefinition {
            ConditionType = RotationConditionTypes.SourceAuraMissing, Key = "hunter-aspect-hawk"
        });
        add(req.UseRapidFire, "rapid-fire");
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
        ForeverMarksmanshipHunterTalents talents, HunterReactiveProcessor procs) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState source,
            SimulationActorState target, AbilityDefinition spell, AbilityEffectDefinition effect) =>
            Resolve(ctx, source, target, spell, effect, CombatEffectDeliveryType.Direct);

        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState source,
            SimulationActorState target, AbilityDefinition spell, AbilityEffectDefinition effect,
            CombatEffectDeliveryType delivery)
        {
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

    private sealed class HunterArmor(decimal armor, HunterReactiveProcessor procs) : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect, decimal rawAmount)
        {
            // The kill-empowered next Shot affects every target hit by the same cast.
            // Cast completion is processed before its impact events at the same timestamp.
            rawAmount *= procs.ShotMultiplier(ability.Key, context.CurrentTimeSeconds);
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
                ev.Type == CombatEventType.ActorDied && ev.SourceActorKey == hunterKey &&
                ev.TargetActorKey != hunterKey && ev.TargetActorKey is not null &&
                ctx.GetActor(ev.TargetActorKey)?.TeamKey == "enemy") {
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
