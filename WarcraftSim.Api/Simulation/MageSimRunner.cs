using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Rotations;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Building;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Simulation.Healing;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Simulation;
using WarcraftSim.Data.Forever.Mage;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP132: three Mage single-target rotations with event-driven Arcane,
/// Fire, Frost proc states. All damage calibration remains provisional.</summary>
public sealed class MageSimRunner
{
    public MageSimResponse Run(MageSimRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Character is null || request.Tuning is null || request.Talents is null ||
            request.Character.Level != 60 ||
            !string.Equals(request.Character.ClassKey, "mage", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.Character.SpecializationKey, request.Specialization, StringComparison.OrdinalIgnoreCase) ||
            request.DurationSeconds is <= 0m or > 1200m || request.TargetMaximumHealth <= 0m ||
            request.MageMaximumHealth <= 0m || request.StartingManaPercent is < 0m or > 100m ||
            request.BaseSpellHitPercent is < 0m or > 100m || request.SpellCritPercent is < 0m or > 100m ||
            request.ArcaneBlastsBeforeRelease is < 1 or > 4 ||
            request.EvocationBelowManaPercent is < 0m or > 100m ||
            request.MajorManaPotionBelowManaPercent is < 0m or > 100m ||
            request.MajorManaPotionCharges is < 0 or > 20 || request.CombatItems is null)
            throw new ArgumentException("Mage requires a valid level-60 configuration, duration, health, hit and crit.");
        request.CombatItems.Validate("mage");
        request.Talents.Validate(request.Specialization);
        ValidateTuning(request.Tuning);
        var stats = ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_ => request.MageMaximumHealth)
            .Calculate(request.Character).EffectiveStats;
        var intellect = Math.Max(0m, stats.Get("intellect")) *
            (1m + request.Talents.Rank("arcane-mind") * .02m);
        var spellPower = Math.Max(0m, stats.Get("spell-power"));
        var spirit = Math.Max(0m, stats.Get("spirit"));
        var maxMana = 1376m + Math.Max(0m, intellect - 20m) * 15m;
        var context = new SimulationContext(new SimulationRunOptions
        {
            Seed = request.Seed, DurationSeconds = request.DurationSeconds,
            CaptureTimeline = request.CaptureTimeline, PrimaryActorKey = "mage",
            MaximumProcessedEvents = 60000, CancellationToken = cancellationToken
        });
        var mage = new SimulationActorState
        {
            Key = "mage", Name = request.Character.Name, TeamKey = "raid",
            Level = 60, MaximumHealth = request.MageMaximumHealth, Stats = stats
        };
        mage.InitializeHealth(mage.MaximumHealth);
        mage.AddResource(new ResourceState("mana", maxMana, maxMana * request.StartingManaPercent / 100m));
        var boss = new SimulationActorState
        {
            Key = "target", Name = "Level 63 Mage training boss", TeamKey = "enemy",
            Level = 63, MaximumHealth = request.TargetMaximumHealth
        };
        boss.InitializeHealth(boss.MaximumHealth);
        context.AddActor(mage);
        context.AddActor(boss);
        foreach (var ability in ForeverMageDpsSpellbook.Create(request.Talents, request.Tuning, spellPower, spirit, maxMana))
            mage.AddAbility(ability);
        var rotation = MageAdaptiveRotation.Build(request);
        var state = new MageSpecializationProcessor(request, rotation, mage);
        var auraManager = new AuraManager();
        var executor = new AbilityExecutor(auraManager, new MageRollResolver(request, state),
            new MageNoMitigation(), abilityTimingProvider: state);
        state.Attach(auraManager);
        var items = new CombatUseItemProcessor(request.CombatItems, executor, mage, "target", "mage");
        var priority = new PriorityRotationExecutor(rotation, "mage", "target", executor, true);
        var regen = new FiveSecondManaRegenerationProcessor("mage",
            request.Tuning.ManaRegenPerSecond, Math.Max(0m, stats.Get("mp5")),
            request.Talents.Rank("arcane-meditation") * .05m);
        var engine = new SimulationEngine([executor, auraManager, regen, state,
            new MageIgniteRollingProcessor(auraManager,
                request.UseIgnite ? request.Talents.Rank("ignite") : 0),
            items, new ThreatManager(new MageDamageThreatResolver(request)), priority]);
        regen.Initialize(context);
        var run = engine.Run(context);
        var owner = run.Summary.ActorSummaries["mage"];
        var breakdown = new Dictionary<string, decimal>(owner.DamageDoneByAbility, StringComparer.OrdinalIgnoreCase);
        return new MageSimResponse
        {
            Specialization = request.Specialization,
            ResolvedRotation = string.Join(" > ",rotation.Entries.Select(x=>x.AbilityKey)),
            DamageDone = owner.DamageDone, Dps = owner.DamageDone / request.DurationSeconds,
            EndingMana = mage.Resources["mana"].Current, MaximumMana = maxMana, SpellPower = spellPower,
            MissileBarrageProcs = state.MissileBarrageProcs,
            ClearcastingProcs = state.ClearcastingProcs,
            ClearcastingConsumed = state.ClearcastingConsumed,
            EvocationCasts = owner.Abilities.TryGetValue("evocation", out var evoc) ? evoc.ChannelStartedCount : 0,
            MajorManaPotionUses = state.MajorManaPotionUses,
            CombatItemUses = new Dictionary<string, int>(items.Uses, StringComparer.OrdinalIgnoreCase),
            ManaRestoredByEvocation = state.EvocationManaGained,
            ManaRestoredByPotions = state.PotionManaGained,
            ArcaneWrathCasts = owner.Abilities.TryGetValue("arcane-wrath", out var wrath) ? wrath.CastCompletedCount : 0,
            ArcaneBlastCasts = owner.Abilities.TryGetValue("arcane-blast", out var blast) ? blast.CastCompletedCount : 0,
            HeatingUpPyroblasts = state.HeatingUpPyroblasts,
            FingersOfFrostProcs = state.FingersOfFrostProcs,
            FingersOfFrostIceLances = state.FingersOfFrostIceLances,
            IgniteDamage = breakdown.GetValueOrDefault("mage-ignite"),
            DamageByAbility = breakdown, Run = run,
            ModelingNotes =
            [
                "CP132 models three Mage solo DPS priorities with the shared cast, channel, aura and resource event engine.",
                "70425 Arcane Blast casts in 1.5s and grants +15% Intellect as Spell Damage per stack, max 4, for 15 seconds from first application; the stacks persist through other spells.",
                "70425 Missile Barrage has 2 ranks: 20/40% Blast, 10/20% other eligible spell and 5/10% per Missiles hit. Procs affect only one channel.",
                "Heating Up stacks on non-periodic eligible Fire criticals, reducing Pyroblast casting time by 25% per stack up to 3.",
                "70425 frozen Ice Lance bonus is +200%, not +300%; Winter's Chill no longer gives crit chance and instead stores a Cone of Cold damage bonus (Cone not yet modeled).",
                "Frostfire Bolt is modeled as Fire damage with both Fire/Frost talent multipliers and a periodic effect; dual-school resistance selection and stacking require later validation.",
                "Ignite is a provisional 4-second two-tick periodic effect and can refresh; exact Forever Ignite rollover requires later calibration.",
                "Talents include modeled subset with non-effective filler points; full 51-point tree prerequisites and final spell ranks are not verified.",
                "CP132.1 adds cross-spec Evocation (8s/480s, Spirit-based restoration) and optional Major Mana Potion (1350-2250, 120s cooldown, configured inventory charge limit).",
                "CP132.2 adds a reusable item catalog with Mana Ruby, Dark/Demonic Runes, Dense Dynamite, Goblin Sapper and Mighty Rage Potion. Their inventory, resource use, cooldown group and engineering requirements are modeled.",
                "CP139 adaptive default: Arcane varies Blast release stacks by remaining duration and Mana; Fire limits proc Pyro use at the encounter end; Frost only uses Ice Lance with Fingers charges. CustomPriority overrides default spell decisions.",
                "CP139.1 replaces old Arcane Blast release damage with timed Int-based Spell Damage, adds provisional Arcane Wrath and Frost Blast ranks, and applies the Fire/Frost changes from October 15. Remaining levels/consumes and crowd-control/AoE are provisional.",
                "No Mage Armor, Arcane Power, Combustion, Water Elemental, PvP control or multi-target damage yet; this is a single-target DPS baseline."
            ]
        };
    }

    private static void ValidateTuning(ForeverMageTuning t)
    {
        var values = new[] { t.FireballDamage,t.FireballMana,t.FireballCastSeconds,
            t.FrostboltDamage,t.FrostboltMana,t.FrostboltCastSeconds,
            t.ArcaneBlastDamage,t.ArcaneBlastMana,t.ArcaneBlastBonusDamagePerStackPercent,
            t.ScorchDamage,t.ScorchMana,
            t.ArcaneBlastExtraManaPerStackPercent,t.ArcaneWrathDamage,t.ArcaneWrathMana,t.FrostBlastDamage,t.FrostBlastMana,t.MissileBarrageArcaneMissileChancePercentPerRank,t.ArcaneMissilesTick,t.ArcaneMissilesMana,
            t.PyroblastDamage,t.PyroblastMana,t.FireBlastDamage,t.FireBlastMana,
            t.FrostfireBoltDamage,t.FrostfireBoltMana,t.FrostfireDotTick,
            t.IceLanceDamage,t.IceLanceMana,t.MissileBarrageArcaneBlastChancePercent,
            t.MissileBarrageOtherChancePercent,t.FingersOfFrostChancePercentPerRank,
            t.ManaRegenPerSecond };
        if (values.Any(v => v < 0m) || t.FireballCastSeconds <= 0m ||
            t.FrostboltCastSeconds <= 0m ||
            t.MissileBarrageArcaneBlastChancePercent > 100m ||
            t.MissileBarrageOtherChancePercent > 100m ||
            t.MissileBarrageArcaneMissileChancePercentPerRank > 50m ||
            t.FingersOfFrostChancePercentPerRank > 50m)
            throw new ArgumentException("Mage damage/cost/timing must be nonnegative; proc chances must be valid.");
    }

    private sealed class MageNoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability,
            AbilityEffectDefinition effect, decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
    }

    private sealed class MageDamageThreatResolver(MageSimRequest request) : IThreatGenerationResolver
    {
        public IReadOnlyList<ThreatGenerationContribution> Resolve(
            SimulationContext context, CombatEvent ev)
        {
            if (ev.Type != CombatEventType.Damage || ev.SourceActorKey != "mage" ||
                ev.TargetActorKey is null || ev.Amount is not > 0m ||
                context.GetActor(ev.TargetActorKey) is not { TeamKey: "enemy" })
                return [];
            // Frost Channeling reduces FROST threat by 10% per talent rank.
            // Frostfire Bolt is a dual-school spell, represented as Fire for
            // damage; its Frost threat qualification is kept explicit.
            var frost = ev.SchoolKey == "frost" || ev.AbilityKey == "frostfire-bolt";
            var modifier = frost ? 1m - .10m * request.Talents.Rank("frost-channeling") : 1m;
            return [new ThreatGenerationContribution {
                ThreatOwnerActorKey = ev.TargetActorKey,
                ThreatSourceActorKey = "mage",
                Amount = Math.Max(0m, ev.Amount.Value * modifier)
            }];
        }
    }

    private sealed class MageRollResolver(MageSimRequest request, MageSpecializationProcessor state) : ICombatRollResolver
    {
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect) =>
            Resolve(context, source, target, ability, effect, CombatEffectDeliveryType.Direct);
        public CombatRollResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability, AbilityEffectDefinition effect,
            CombatEffectDeliveryType deliveryType)
        {
            if (!effect.CanMiss && !effect.CanCrit) return CombatRollResult.Hit();
            var arcane = effect.SchoolKey == "arcane";
            var elemental = effect.SchoolKey is "fire" or "frost";
            var hitChance = Math.Min(100m, request.BaseSpellHitPercent + source.Stats.Get("spell-hit-percent") + source.Stats.Get("spell-hit-chance-percent") +
                (arcane ? request.Talents.Rank("arcane-focus") : 0) +
                (elemental ? request.Talents.Rank("elemental-precision") : 0));
            if (effect.CanMiss && (decimal)context.Random.NextDouble() * 100m >= hitChance)
                return CombatRollResult.Miss();
            var crit = request.SpellCritPercent + source.Stats.Get("spell-critical-chance-percent") +
                effect.BonusCriticalChancePercent + request.Talents.Rank("arcane-instability");
            if (effect.SchoolKey == "arcane") crit += request.Talents.Rank("arcane-impact") * 2m;
            if (effect.SchoolKey == "fire") crit += request.Talents.Rank("critical-mass") * 2m;
            if (effect.SchoolKey == "fire" && deliveryType == CombatEffectDeliveryType.Direct &&
                state.CombustionActive) crit += state.CombustionHits * 10m;
            // Forever Incineration affects the named spells across all three
            // schools, not every Fire spell. Three ranks, +2% each.
            if (ability.Key is "fire-blast" or "arcane-blast" or "ice-lance" or "scorch")
                crit += request.Talents.Rank("incineration") * 2m;
                        if (ability.Key == "ice-lance" && state.CurrentIceLanceFrozen)
                crit += request.Talents.Rank("shatter") * 10m;
            if (effect.CanCrit && (decimal)context.Random.NextDouble() * 100m < Math.Min(100m, crit))
            {
                var multiplier = effect.SchoolKey == "frost" || ability.Key == "frostfire-bolt" ?
                    1.5m + request.Talents.Rank("ice-shards") * .1m : 1.5m;
                // +20% of the ordinary +50% CRITICAL BONUS per rank,
                // rather than a flat +2% critical multiplier per rank.
                if (arcane) multiplier += request.Talents.Rank("arcane-mind") * .10m;
                return CombatRollResult.Critical(multiplier);
            }
            return CombatRollResult.Hit();
        }
    }

    private sealed class MageSpecializationProcessor : ICombatEventProcessor, IAbilityTimingProvider
    {
        private readonly MageSimRequest _r;
        private readonly RotationProfile _rotation;
        private readonly SimulationActorState _actor;
        private readonly Dictionary<string, decimal> _baseMultipliers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, decimal> _baseAmounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, decimal> _baseManaCosts = new(StringComparer.OrdinalIgnoreCase);
        private AuraManager? _auras;
        private bool _clearcasting;
        private decimal _clearcastingExpiresAt;
        private decimal _clearcastingNextProcAt;
        private Guid? _clearcastingReservedCast;
        public int ClearcastingProcs { get; private set; }
        public int ClearcastingConsumed { get; private set; }
        private readonly Dictionary<string, decimal> _activeFireEffects = new(StringComparer.OrdinalIgnoreCase);
        private decimal _intellect;
        private decimal _arcaneSpellDamage;
        public decimal EvocationManaGained { get; private set; }
        public decimal PotionManaGained { get; private set; }
        private int _combustionHits;
        private int _combustionCrits;
        private bool _combustionActive;
        private int _arcaneStacks;
        private decimal _currentTime;
        private decimal _arcaneExpires;
        private int _heatingUp;
        private int _fingers;
        private bool _barrage;
        private bool _barrageCastPending;
        private decimal _winterExpires;
        private int _winter;
        public int MajorManaPotionUses { get; private set; }
        public int MissileBarrageProcs { get; private set; }
        public int HeatingUpPyroblasts { get; private set; }
        public int FingersOfFrostProcs { get; private set; }
        public int FingersOfFrostIceLances { get; private set; }
        public int WintersChillStacks => _winter;
        public bool CurrentIceLanceFrozen { get; private set; }
        public bool CombustionActive => _combustionActive; 
        public int CombustionHits => _combustionHits;
        public MageSpecializationProcessor(MageSimRequest r, RotationProfile rotation, SimulationActorState actor)
        {
            _r = r; _rotation = rotation; _actor = actor;
            foreach (var a in actor.Abilities.Values)
                foreach (var effect in a.Definition.Effects)
                {
                    var id=$"{a.Definition.Key}:{effect.Key}";
                    _baseMultipliers[id] = effect.DamageMultiplier;
                    _baseAmounts[id] = effect.MinimumValue;
                }

            foreach (var entry in actor.Abilities.Values)
                if (IsDamageSpell(entry.Definition) &&
                    entry.Definition.ResourceCosts.FirstOrDefault(c => c.ResourceKey == "mana") is { } mana)
                    _baseManaCosts[entry.Definition.Key] = mana.Amount;
            _intellect=Math.Max(0m,actor.Stats.Get("intellect"))*(1m+r.Talents.Rank("arcane-mind")*.02m);
            RefreshRotation();
        }
        public void Attach(AuraManager auras) => _auras = auras;

        private static bool IsDamageSpell(AbilityDefinition spell) =>
            spell.Key != "mage-ignite" &&
            spell.Effects.Any(e => e.EffectType is
                AbilityEffectTypes.DirectDamage or AbilityEffectTypes.PeriodicDamage);

        // The executor validates resource availability when the cast starts
        // and spends the cost when it completes. Keep a free spell's cost at
        // zero throughout its cast, restoring its current stacked cost only
        // after the successful completion event.
        private void RefreshClearcastingCosts()
        {
            foreach (var (key, baseCost) in _baseManaCosts)
            {
                if (key is "arcane-blast" or "arcane-missiles") continue;
                if (_actor.Abilities.TryGetValue(key, out var entry))
                {
                    var cost = entry.Definition.ResourceCosts
                        .FirstOrDefault(c => c.ResourceKey == "mana");
                    if (cost is not null) cost.Amount = _clearcasting ? 0m : baseCost;
                }
            }
            RefreshArcaneCost();
            RefreshMissilesCost();
        }

        private void EndClearcasting(SimulationContext context, string reason)
        {
            if (!_clearcasting) return;
            _clearcasting = false;
            _clearcastingReservedCast = null;
            _auras?.RemoveAuras(context, _actor, "mage-clearcasting", _actor.Key, reason);
            RefreshClearcastingCosts();
        }

        private void TryProcClearcasting(SimulationContext context, CombatEvent ev)
        {
            var rank = _r.Talents.Rank("arcane-concentration");
            if (rank <= 0 || _clearcasting || context.CurrentTimeSeconds < _clearcastingNextProcAt ||
                ev.IsPeriodic || ev.Amount is not > 0m ||
                ev.ResultKey is not ("hit" or "critical") ||
                ev.AbilityKey is null || !_actor.Abilities.TryGetValue(ev.AbilityKey, out var spell) ||
                !IsDamageSpell(spell.Definition)) return;
            // Source-backed 2% per rank; one-second proc cooldown prevents
            // multiple simultaneous AoE/channel ticks from granting duplicates.
            if ((decimal)context.Random.NextDouble() * 100m >= 2m * rank) return;
            _clearcasting = true;
            _clearcastingExpiresAt = context.CurrentTimeSeconds + 15m;
            _clearcastingNextProcAt = context.CurrentTimeSeconds + 1m;
            ClearcastingProcs++;
            _auras?.ApplyAura(context, _actor, new AuraDefinition {
                Key = "mage-clearcasting", Name = "Clearcasting",
                DurationSeconds = 15m
            }, _actor.Key, "arcane-concentration", "clearcasting");
            RefreshClearcastingCosts();
        }
        public AbilityTimingSnapshot Resolve(SimulationContext context, SimulationActorState source, AbilityDefinition ability)
        {
            var pyroTime = ability.Key == "pyroblast" && _r.UseHeatingUp ?
                Math.Max(1.5m, ability.CastTimeSeconds * (1m - _heatingUp * .25m)) : ability.CastTimeSeconds;
            return new AbilityTimingSnapshot
            {
                CastTimeSeconds = pyroTime,
                GlobalCooldownSeconds = ability.GlobalCooldownSeconds,
                ChannelDurationSeconds = ability.Key == "arcane-missiles" && _barrage ? 2.5m : ability.ChannelDurationSeconds,
                ChannelTickIntervalSeconds = ability.Key == "arcane-missiles" && _barrage ? .5m : ability.ChannelTickIntervalSeconds
            };
        }
        public void Process(SimulationContext context, CombatEvent ev)
        {
            if (ev.Type == CombatEventType.SimulationEnded) return;
            _currentTime=context.CurrentTimeSeconds;
            if (_clearcasting && _clearcastingReservedCast is null &&
                context.CurrentTimeSeconds >= _clearcastingExpiresAt)
                EndClearcasting(context, "clearcasting expired");
            if (_arcaneStacks > 0 && context.CurrentTimeSeconds >= _arcaneExpires)
            {
                _arcaneStacks = 0;
                _arcaneSpellDamage=0m;
                RefreshArcaneCost();
            }
            if (_winter > 0 && context.CurrentTimeSeconds >= _winterExpires) _winter = 0;
            if (!string.Equals(ev.SourceActorKey, "mage", StringComparison.OrdinalIgnoreCase))
            {
                if (ev.Type == CombatEventType.RotationDecision) RefreshRotation();
                return;
            }
            switch (ev.Type)
            {
                case CombatEventType.AbilityCastStarted:
                    if (_clearcasting && ev.AbilityExecutionId.HasValue &&
                        ev.AbilityKey is not null &&
                        _actor.Abilities.TryGetValue(ev.AbilityKey, out var freeSpell) &&
                        IsDamageSpell(freeSpell.Definition))
                        _clearcastingReservedCast = ev.AbilityExecutionId;
                    if (ev.AbilityKey == "mage-ignite") break;
                    if (ev.AbilityKey == "arcane-missiles")
                    {
                        _barrageCastPending = _barrage;
                        _barrage = false;
                    }
                    if (ev.AbilityKey == "pyroblast" && _heatingUp > 0)
                    {
                        HeatingUpPyroblasts++;
                        _heatingUp = 0;
                    }
                    if (ev.AbilityKey == "ice-lance")
                    {
                        CurrentIceLanceFrozen = _fingers > 0;
                        if (_fingers > 0) { _fingers--; FingersOfFrostIceLances++; }
                    }
                    if (ev.AbilityKey is not null && _actor.Abilities.TryGetValue(ev.AbilityKey, out var castAbility))
                    {
                        // Snapshot additional spell damage when the cast starts.
                        // Do not mutate reactive Ignite, potions or item effects.
                        foreach (var effect in castAbility.Definition.Effects)
                        {
                            var id=$"{castAbility.Definition.Key}:{effect.Key}";
                            if (!_baseMultipliers.TryGetValue(id,out var multiplier) ||
                                !_baseAmounts.TryGetValue(id,out var raw) ||
                                ev.AbilityKey == "mage-ignite") continue;
                            if (ev.AbilityKey == "cone-of-cold")
                            {
                                // Winter's Chill 70532: each stack increases
                                // the NEXT Cone's damage by 5%, and adds 7%
                                // of the Mage's spell power to its scaling.
                                // Only spells that actually LAND consume it.
                                var stacks = Math.Min(5, _winter);
                                var bonusPower = Math.Max(0m,_actor.Stats.Get("spell-power"));
                                effect.MinimumValue=raw + .129m*_arcaneSpellDamage +
                                    stacks*.07m*bonusPower;
                                effect.MaximumValue=effect.MinimumValue;
                                effect.DamageMultiplier=multiplier*(1m+stacks*.05m);
                                continue;
                            }
                            var fireEffects=_activeFireEffects.Values.Count(expires=>expires>context.CurrentTimeSeconds);
                            var fireBonus=effect.SchoolKey=="fire" &&
                                ev.AbilityKey!="frostfire-bolt" ?
                                1m+Math.Min(4,fireEffects)*_r.Talents.Rank("fire-power")*.01m:1m;
                            effect.DamageMultiplier=multiplier*fireBonus*
                                (ev.AbilityKey=="ice-lance"&&CurrentIceLanceFrozen?3m:1m);
                            decimal coef=ev.AbilityKey switch
                            {
                                "arcane-blast" => .4284m, "arcane-wrath" => .857m,
                                "arcane-missiles" => .19m, "fireball" => .857m,
                                "frostbolt" => .814m, "frost-blast" => .429m,
                                "fire-blast" => .429m, "scorch" => .429m, "pyroblast" => 1.15m,
                                "ice-lance" => .143m, "cone-of-cold" => .129m,
                                "frostfire-bolt" => .857m,
                                _ => 0m
                            };
                            // The Frostfire DoT gains only its own .08
                            // coefficient from Arcane Blast spell-damage
                            // stacks, not the direct impact's .857.
                            if (effect.EffectType == AbilityEffectTypes.PeriodicDamage &&
                                ev.AbilityKey == "frostfire-bolt")
                                coef = .08m;
                            effect.MinimumValue=raw+coef*_arcaneSpellDamage;
                            effect.MaximumValue=raw+coef*_arcaneSpellDamage;
                        }
                    }
                    RefreshRotation();
                    break;
                case CombatEventType.AbilityCastCompleted:
                    if (ev.AbilityKey == "cold-snap")
                    {
                        foreach (var frostKey in new[] { "frost-blast", "cone-of-cold" })
                            if (_actor.Abilities.TryGetValue(frostKey, out var frostAbility))
                                frostAbility.ResetCooldown(context.CurrentTimeSeconds);
                    }
                    if (_clearcastingReservedCast.HasValue &&
                        ev.AbilityExecutionId == _clearcastingReservedCast)
                    {
                        ClearcastingConsumed++;
                        EndClearcasting(context, "clearcasting consumed");
                    }
                    if (ev.AbilityKey == "major-mana-potion") MajorManaPotionUses++;
                    // 70425: Fire Blast grants a Heating Up charge per cast, even without a critical.
                    if (ev.AbilityKey == "fire-blast" && _r.UseHeatingUp &&
                        _r.Talents.Rank("heating-up") > 0)
                        _heatingUp=Math.Min(3,_heatingUp+1);
                    if (ev.AbilityKey == "arcane-missiles") _barrageCastPending = false;
                    if (ev.AbilityKey == "arcane-blast")
                    {
                        if (_arcaneStacks == 0) _arcaneExpires = context.CurrentTimeSeconds + 15m;
                        _arcaneStacks = Math.Min(4, _arcaneStacks + 1);
                        _arcaneSpellDamage = _arcaneStacks * _intellect * .15m;
                        RefreshArcaneCost();
                    }
                    if (_r.UseMissileBarrage && _r.Talents.Rank("missile-barrage") > 0 &&
                        (ev.AbilityKey is "arcane-blast" or "arcane-wrath" or "fireball" or "frostbolt" or "frostfire-bolt"))
                    {
                        var ranks = _r.Talents.Rank("missile-barrage");
                        var chance = (ev.AbilityKey == "arcane-blast" ?
                            _r.Tuning.MissileBarrageArcaneBlastChancePercent :
                            _r.Tuning.MissileBarrageOtherChancePercent)*ranks/2m;
                        if (!_barrage && (decimal)context.Random.NextDouble()*100m<chance)
                        { _barrage=true; MissileBarrageProcs++; }
                    }
                    RefreshMissilesCost(); RefreshRotation();
                    break;
                case CombatEventType.AbilityCastCancelled:
                    if (ev.AbilityExecutionId == _clearcastingReservedCast)
                        _clearcastingReservedCast = null;
                    break;
                case CombatEventType.Damage:
                    if (_combustionActive && !ev.IsPeriodic && !ev.IsChannelTick &&
                        ev.SchoolKey == "fire" && ev.ResultKey is ("hit" or "critical"))
                    {
                        _combustionHits++;
                        if (ev.IsCritical) _combustionCrits++;
                        if (_combustionCrits >= 3)
                        {
                            _combustionActive = false;
                            _combustionHits = 0;
                            _auras?.RemoveAuras(context, _actor, "mage-combustion", _actor.Key, "three direct Fire crits");
                        }
                    }
                    var landed = ev.ResultKey is "hit" or "critical";
                    var empoweredLance = ev.AbilityKey == "ice-lance" &&
                        CurrentIceLanceFrozen;
                    if (ev.AbilityKey == "ice-lance") CurrentIceLanceFrozen = false;
                    if (ev.AbilityKey == "cone-of-cold" && landed)
                    {
                        _winter = 0;
                        _winterExpires = 0m;
                    }
                    if (ev.IsChannelTick && ev.AbilityKey == "arcane-missiles" &&
                        (ev.ResultKey is "hit" or "critical") && !_barrage &&
                        _r.UseMissileBarrage && _r.Talents.Rank("missile-barrage") > 0 &&
                        (decimal)context.Random.NextDouble()*100m <
                        _r.Tuning.MissileBarrageArcaneMissileChancePercentPerRank*_r.Talents.Rank("missile-barrage"))
                    { _barrage=true; MissileBarrageProcs++; }
                    if ((ev.ResultKey is "hit" or "critical") && (ev.AbilityKey is "frostbolt" or "frost-blast" or "frostfire-bolt"))
                    {
                        if (_r.UseFingersOfFrost && _r.Talents.Rank("fingers-of-frost") > 0 &&
                            (decimal)context.Random.NextDouble() * 100m <
                            _r.Tuning.FingersOfFrostChancePercentPerRank * _r.Talents.Rank("fingers-of-frost"))
                        { _fingers = Math.Min(2, _fingers + 2); FingersOfFrostProcs++; }
                    }
                    // Winter's Chill only procs from Frostbolt, Frost Blast,
                    // and Fingers of Frost-empowered Ice Lance after a hit.
                    if (landed && !ev.IsPeriodic && !ev.IsChannelTick &&
                        _r.UseWintersChill &&
                        _r.Talents.Rank("winters-chill") > 0 &&
                        (ev.AbilityKey is "frostbolt" or "frost-blast" ||
                            empoweredLance) &&
                        (decimal)context.Random.NextDouble() * 100m <
                            20m * _r.Talents.Rank("winters-chill"))
                    {
                        _winter = Math.Min(5, _winter + 1);
                        _winterExpires = context.CurrentTimeSeconds + 15m;
                    }
                    if ((ev.ResultKey is "hit" or "critical") && !ev.IsPeriodic && !ev.IsChannelTick &&
                        (ev.AbilityKey is "fireball" or "pyroblast"))
                        _activeFireEffects[ev.AbilityKey!]=context.CurrentTimeSeconds+10m;
                                        if (!ev.IsPeriodic && !ev.IsChannelTick &&
                        (ev.AbilityKey is "fireball" or "frostfire-bolt" or "fire-blast" or "scorch") &&
                        _r.UseHeatingUp && _r.Talents.Rank("heating-up") > 0)
                    {
                        if (ev.IsCritical) _heatingUp=Math.Min(3,_heatingUp+1);
                    }

                    TryProcClearcasting(context, ev);
                    RefreshRotation();
                    break;
                case CombatEventType.AuraApplied:
                    if (ev.AbilityKey == "combustion")
                    {
                        _combustionActive = true;
                        _combustionHits = 0;
                        _combustionCrits = 0;
                    }
                    if (ev.AbilityKey == "mage-ignite")
                        _activeFireEffects["ignite"]=context.CurrentTimeSeconds+4m;
                    break;
                case CombatEventType.AuraRemoved:
                    if (ev.AbilityKey == "combustion") _combustionActive = false;
                    break;
                case CombatEventType.ResourceChanged:
                    if (ev.Amount > 0m && ev.AbilityKey=="evocation")
                        EvocationManaGained+=ev.Amount.Value;
                    if (ev.Amount > 0m && ev.AbilityKey=="major-mana-potion")
                        PotionManaGained+=ev.Amount.Value;
                    break;
                case CombatEventType.RotationDecision: RefreshRotation(); break;
            }
        }
        private void RefreshArcaneCost()
        {
            if (!_actor.Abilities.TryGetValue("arcane-blast", out var s)) return;
            s.Definition.ResourceCosts[0].Amount = _clearcasting ? 0m :
                _r.Tuning.ArcaneBlastMana *
                (1m + _arcaneStacks * _r.Tuning.ArcaneBlastExtraManaPerStackPercent / 100m);
        }
        private void RefreshMissilesCost()
        {
            if (!_actor.Abilities.TryGetValue("arcane-missiles", out var s)) return;
            s.Definition.ResourceCosts[0].Amount = _clearcasting || _barrage || _barrageCastPending
                ? 0m : _r.Tuning.ArcaneMissilesMana;
        }
        private void RefreshRotation()
        {
            var remaining=Math.Max(0m,_r.DurationSeconds-_currentTime);
            var mana=_actor.Resources.TryGetValue("mana",out var pool) ? pool.Current : 0m;
            var manaPercent=pool is not null&&pool.Maximum>0m ? mana*100m/pool.Maximum : 100m;
            // Longer fights with low resources favor shorter Arcane Blast cycles.
            // Never raise the user-configured stack ceiling.
            var releaseAt=_r.ArcaneBlastsBeforeRelease;
            if(_r.RotationPolicy=="adaptive"&&_r.CustomPriority.Count==0&&remaining>30m)
            {
                if(manaPercent<40m)releaseAt=Math.Min(releaseAt,1);
                else if(manaPercent<70m)releaseAt=Math.Min(releaseAt,2);
            }
            foreach (var entry in _rotation.Entries)
                entry.IsEnabled = entry.AbilityKey switch
                {
                    "evocation" => _r.UseEvocation &&
                        (_r.RotationPolicy=="legacy" || remaining>9m),
                    "major-mana-potion" => _r.UseMajorManaPotion &&
                        MajorManaPotionUses < _r.MajorManaPotionCharges &&
                        (_r.RotationPolicy=="legacy" || remaining>2m),
                    "arcane-missiles" => _r.CustomPriority.Count>0 || _barrage,
                    "arcane-wrath" => _r.CustomPriority.Count>0 || _r.Talents.Rank("arcane-wrath")>0 && !_barrage,
                    "arcane-blast" => _r.CustomPriority.Count>0 || !_barrage &&
                        (_arcaneStacks < releaseAt || _arcaneExpires-_currentTime < 2m),
                    "fireball" or "frostfire-bolt" when _r.Specialization == "arcane" =>
                        _r.CustomPriority.Count>0 || _r.Talents.Rank("arcane-blast")==0 ||
                        !_barrage && _arcaneStacks >= releaseAt,
                    "pyroblast" => _r.CustomPriority.Count>0 || _heatingUp>=3 ||
                        (_r.RotationPolicy=="adaptive"&&remaining<=6m&&_heatingUp>=2),
                    "ice-lance" => _r.CustomPriority.Count>0 || _fingers > 0,
                    _ => true
                };
            RefreshMissilesCost();
        }
    }
}
