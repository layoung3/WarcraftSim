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
                Level = 63, MaximumHealth = request.TargetMaximumHealth
            };
            extra.InitializeHealth(extra.MaximumHealth);
            context.AddActor(extra);
        }
        foreach (var spell in ForeverMarksmanshipHunterSpellbook.Create(talents, t, ap))
            hunter.AddAbility(spell);
        var aura = new AuraManager();
        var executor = new AbilityExecutor(aura, new HunterRolls(t, talents),
            new HunterArmor(t.TargetArmor));
        var auto = new AutoAttackProcessor(executor, new ForeverAutoAttackTimingProvider());
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
        var marksmanship = new HunterReactiveProcessor("hunter", hunter, t, talents);
        var rotation = BuildRotation(request, hunter);
        var priority = new PriorityRotationExecutor(rotation, "hunter", "target", executor,
            autoAttackProcessor: auto, reactToTargetStateChanges: true);
        // Reactive processor follows AuraManager so it sees applied/expired aura events.
        var engine = new SimulationEngine([executor, aura, auto, marksmanship, regen, priority]);
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
        run.CustomMetrics["hunter-no-pet-lone-wolf"] = talents.Rank("lone-wolf") > 0 ? 1m : 0m;
        return new MarksmanshipHunterSimResponse {
            Dps = report.DamageDone / request.DurationSeconds,
            DamageDone = report.DamageDone, EndingMana = hunter.Resources["mana"].Current,
            MaximumMana = t.MaximumMana, RangedAttackPower = ap,
            RapidFireCasts = marksmanship.RapidFireCasts,
            SerpentStingApplications = marksmanship.SerpentApplications,
            DamageByAbility = new Dictionary<string, decimal>(report.DamageDoneByAbility,
                StringComparer.OrdinalIgnoreCase), Run = run,
            ModelingNotes = [
                "CP157 stationary, no-pet level-60 Marksmanship. Pet combat and movement/dead-zone checks are deferred.",
                "Aimed Shot and Multi-Shot use the shared engine cooldown; Arcane Shot is independent.",
                "Lone Wolf, Careful Aim, Trueshot Aura, improved stings, Barrage, Sniper Shot, Rapid Fire and Spirit recuperation are connected.",
                "Attack/damage coefficients, rank caps where client data is missing, Mana costs, and haste/timing are preview tuning values.",
                "Rapid Killing's target-death proc, detailed ammo inventory, trap logic and raid-wide Trueshot sharing are deferred."
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
        add(req.UseRapidFire, "rapid-fire");
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
        ForeverMarksmanshipHunterTalents talents) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState source,
            SimulationActorState target, AbilityDefinition spell, AbilityEffectDefinition effect) =>
            Resolve(ctx, source, target, spell, effect, CombatEffectDeliveryType.Direct);

        public CombatRollResult Resolve(SimulationContext ctx, SimulationActorState source,
            SimulationActorState target, AbilityDefinition spell, AbilityEffectDefinition effect,
            CombatEffectDeliveryType delivery)
        {
            if (effect.CanMiss && (decimal)ctx.Random.NextDouble() * 100m >= t.HitChancePercent)
                return CombatRollResult.Miss();
            if (!effect.CanCrit) return CombatRollResult.Hit();
            var crit = Math.Min(100m, t.CriticalChancePercent + talents.Rank("lethal-attacks"));
            if ((decimal)ctx.Random.NextDouble() * 100m >= crit) return CombatRollResult.Hit();
            var bonus = effect.SchoolKey == "physical" ? talents.Rank("mortal-shots") * .06m : 0m;
            return CombatRollResult.Critical(2m + bonus);
        }
    }

    private sealed class HunterArmor(decimal armor) : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context,
            SimulationActorState source, SimulationActorState target,
            AbilityDefinition ability, AbilityEffectDefinition effect, decimal rawAmount)
        {
            if (effect.MitigationType != DamageMitigationTypes.Armor)
                return DamageMitigationResult.Unmitigated(rawAmount);
            var reduction = armor / (armor + 400m + 85m * Math.Max(1, source.Level));
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
        private decimal _rapidEnds;
        private decimal _recuperationEnds;
        private decimal _lastManaSpendAt = decimal.MinValue;
        public int RapidFireCasts { get; private set; }
        public int SerpentApplications { get; private set; }
        public decimal RecuperationMana { get; private set; }
        public void Process(SimulationContext ctx, CombatEvent ev)
        {
            if (ev.SourceActorKey == hunterKey && ev.Type == CombatEventType.ResourceChanged &&
                ev.ResourceKey == "mana" && ev.Amount is < 0m)
                _lastManaSpendAt = ctx.CurrentTimeSeconds;
            if (ev.SourceActorKey == hunterKey && ev.Type == CombatEventType.AuraApplied &&
                ev.AbilityKey == "rapid-fire") {
                RapidFireCasts++;
                _rapidEnds = ctx.CurrentTimeSeconds + t.RapidFireDurationSeconds;
                hunter.Stats.Set(ForeverCombatStatKeys.HastePercent, t.RapidFireHastePercent);
            }
            if (ev.SourceActorKey == hunterKey && ev.Type == CombatEventType.AuraRemoved &&
                ev.AbilityKey == "rapid-fire" && ctx.CurrentTimeSeconds >= _rapidEnds) {
                hunter.Stats.Set(ForeverCombatStatKeys.HastePercent, 0m);
            }
            if (ev.SourceActorKey == hunterKey && ev.Type == CombatEventType.AuraApplied &&
                ev.AbilityKey == "serpent-sting" && ev.EffectKey == "serpent-sting-tick") {
                SerpentApplications++;
                if (talents.Rank("rapid-recuperation") > 0) {
                    _recuperationEnds = ctx.CurrentTimeSeconds + 15m;
                    var at = ctx.CurrentTimeSeconds + 1m;
                    if (at < ctx.Options.DurationSeconds)
                        ctx.ScheduleEvent(new CombatEvent {
                            TimeSeconds = at, Type = CombatEventType.RotationDecision,
                            SourceActorKey = hunterKey, EffectKey = "hunter-recuperation-tick",
                            IsInternal = true
                        });
                }
            }
            if (ev.Type == CombatEventType.RotationDecision && ev.IsInternal &&
                ev.SourceActorKey == hunterKey && ev.EffectKey == "hunter-recuperation-tick" &&
                ctx.CurrentTimeSeconds <= _recuperationEnds) {
                // Only grants casting Spirit regeneration while within the five-second rule.
                // Avoid double-counting passive out-of-casting Spirit regeneration.
                var mana = hunter.Resources["mana"];
                if (ctx.CurrentTimeSeconds < _lastManaSpendAt + 5m) {
                    mana.AdvanceTo(ctx.CurrentTimeSeconds);
                    var value = t.SpiritRegenPerSecond * Math.Min(1m, .5m * talents.Rank("rapid-recuperation"));
                    var before = mana.Current;
                    mana.Gain(value);
                    var gained = mana.Current - before;
                    RecuperationMana += gained;
                    if (gained > 0m) ctx.EmitEvent(new CombatEvent {
                        TimeSeconds = ctx.CurrentTimeSeconds, Type = CombatEventType.ResourceChanged,
                        SourceActorKey = hunterKey, TargetActorKey = hunterKey,
                        AbilityKey = "rapid-recuperation", ResourceKey = "mana", Amount = gained,
                        ResourceTotalAfter = mana.Current,
                        Description = "Rapid Recuperation restored Mana during casting."
                    });
                }
                var next = ctx.CurrentTimeSeconds + 1m;
                if (next <= _recuperationEnds && next < ctx.Options.DurationSeconds)
                    ctx.ScheduleEvent(new CombatEvent {
                        TimeSeconds = next, Type = CombatEventType.RotationDecision,
                        SourceActorKey = hunterKey, EffectKey = "hunter-recuperation-tick",
                        IsInternal = true
                    });
            }
        }
    }
}
