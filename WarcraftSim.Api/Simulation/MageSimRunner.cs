using WarcraftSim.Core.Abilities;
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
            request.ArcaneBlastsBeforeRelease is < 1 or > 4)
            throw new ArgumentException("Mage requires a valid level-60 configuration, duration, health, hit and crit.");
        request.Talents.Validate(request.Specialization);
        ValidateTuning(request.Tuning);
        var stats = ForeverCharacterSimulationRuntimeCalculatorFactory.CreateStandard(_ => request.MageMaximumHealth)
            .Calculate(request.Character).EffectiveStats;
        var intellect = Math.Max(0m, stats.Get("intellect")) *
            (1m + request.Talents.Rank("arcane-mind") * .03m);
        var spellPower = Math.Max(0m, stats.Get("spell-power"));
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
        foreach (var ability in ForeverMageDpsSpellbook.Create(request.Talents, request.Tuning, spellPower))
            mage.AddAbility(ability);
        var rotation = CreateRotation(request);
        var state = new MageSpecializationProcessor(request, rotation, mage);
        var auraManager = new AuraManager();
        var executor = new AbilityExecutor(auraManager, new MageRollResolver(request, state),
            new MageNoMitigation(), abilityTimingProvider: state);
        state.Attach(executor);
        var priority = new PriorityRotationExecutor(rotation, "mage", "target", executor, true);
        var regen = new FiveSecondManaRegenerationProcessor("mage",
            request.Tuning.ManaRegenPerSecond, Math.Max(0m, stats.Get("mp5")),
            request.Talents.Rank("arcane-meditation") * .05m);
        var engine = new SimulationEngine([executor, auraManager, regen, state, priority]);
        regen.Initialize(context);
        var run = engine.Run(context);
        var owner = run.Summary.ActorSummaries["mage"];
        var breakdown = new Dictionary<string, decimal>(owner.DamageDoneByAbility, StringComparer.OrdinalIgnoreCase);
        return new MageSimResponse
        {
            Specialization = request.Specialization,
            DamageDone = owner.DamageDone, Dps = owner.DamageDone / request.DurationSeconds,
            EndingMana = mage.Resources["mana"].Current, MaximumMana = maxMana, SpellPower = spellPower,
            MissileBarrageProcs = state.MissileBarrageProcs,
            ArcaneBlastCasts = owner.Abilities.TryGetValue("arcane-blast", out var blast) ? blast.CastCompletedCount : 0,
            HeatingUpPyroblasts = state.HeatingUpPyroblasts,
            FingersOfFrostProcs = state.FingersOfFrostProcs,
            FingersOfFrostIceLances = state.FingersOfFrostIceLances,
            IgniteDamage = breakdown.GetValueOrDefault("mage-ignite"),
            DamageByAbility = breakdown, Run = run,
            ModelingNotes =
            [
                "CP132 models three Mage solo DPS priorities with the shared cast, channel, aura and resource event engine.",
                "Arcane Blast increases its own Mana cost per stack, and empowers the next other Mage spell, which consumes the stacks. Stacks expire at 8 seconds.",
                "Missile Barrage proc chance is 40% on Arcane Blast, 20% on Fireball, Frostbolt or Frostfire Bolt; the next Arcane Missiles is free and twice as fast.",
                "Heating Up stacks on non-periodic eligible Fire criticals, reducing Pyroblast casting time by 25% per stack up to 3.",
                "Fingers of Frost grants charges from eligible Chill spells; Ice Lance consumes them, gains frozen damage bonus and uses Shatter crit bonus. Winter's Chill affects this Mage only.",
                "Frostfire Bolt is modeled as Fire damage with both Fire/Frost talent multipliers and a periodic effect; dual-school resistance selection and stacking require later validation.",
                "Ignite is a provisional 4-second two-tick periodic effect and can refresh; exact Forever Ignite rollover requires later calibration.",
                "Talents include modeled subset with non-effective filler points; full 51-point tree prerequisites and final spell ranks are not verified.",
                "No Mage Armor, Arcane Power, Combustion, Water Elemental, PvP control, multi-target damage or consumables yet; this is a single-target DPS baseline."
            ]
        };
    }

    private static void ValidateTuning(ForeverMageTuning t)
    {
        var values = new[] { t.FireballDamage,t.FireballMana,t.FireballCastSeconds,
            t.FrostboltDamage,t.FrostboltMana,t.FrostboltCastSeconds,
            t.ArcaneBlastDamage,t.ArcaneBlastMana,t.ArcaneBlastBonusDamagePerStackPercent,
            t.ArcaneBlastExtraManaPerStackPercent,t.ArcaneMissilesTick,t.ArcaneMissilesMana,
            t.PyroblastDamage,t.PyroblastMana,t.FireBlastDamage,t.FireBlastMana,
            t.FrostfireBoltDamage,t.FrostfireBoltMana,t.FrostfireDotTick,
            t.IceLanceDamage,t.IceLanceMana,t.MissileBarrageArcaneBlastChancePercent,
            t.MissileBarrageOtherChancePercent,t.FingersOfFrostChancePercentPerRank,
            t.ManaRegenPerSecond };
        if (values.Any(v => v < 0m) || t.FireballCastSeconds <= 0m ||
            t.FrostboltCastSeconds <= 0m ||
            t.MissileBarrageArcaneBlastChancePercent > 100m ||
            t.MissileBarrageOtherChancePercent > 100m ||
            t.FingersOfFrostChancePercentPerRank > 50m)
            throw new ArgumentException("Mage damage/cost/timing must be nonnegative; proc chances must be valid.");
    }

    private static RotationProfile CreateRotation(MageSimRequest r)
    {
        var profile = new RotationProfile { Name = $"CP132 {r.Specialization} Mage",
            ClassKey = "mage", RulesetKey = "wow-forever", SimulationType = SimulationType.Dps };
        var p = 0;
        void add(string key) => profile.Entries.Add(new RotationEntry { AbilityKey = key, Priority = p++ });
        switch (r.Specialization)
        {
            case "arcane":
                add("arcane-missiles"); add("arcane-blast");
                add(r.UseFrostfireBolt ? "frostfire-bolt" : "fireball");
                break;
            case "fire":
                add("pyroblast"); add("fire-blast");
                add(r.UseFrostfireBolt ? "frostfire-bolt" : "fireball");
                break;
            case "frost":
                add("ice-lance");
                add(r.UseFrostfireBolt ? "frostfire-bolt" : "frostbolt");
                break;
        }
        return profile;
    }

    private sealed class MageNoMitigation : IDamageMitigationResolver
    {
        public DamageMitigationResult Resolve(SimulationContext context, SimulationActorState source,
            SimulationActorState target, AbilityDefinition ability,
            AbilityEffectDefinition effect, decimal rawAmount) => DamageMitigationResult.Unmitigated(rawAmount);
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
            var hitChance = Math.Min(100m, request.BaseSpellHitPercent + source.Stats.Get("spell-hit-percent") +
                (arcane ? request.Talents.Rank("arcane-focus") : 0) +
                (elemental ? request.Talents.Rank("elemental-precision") : 0));
            if (effect.CanMiss && (decimal)context.Random.NextDouble() * 100m >= hitChance)
                return CombatRollResult.Miss();
            var crit = request.SpellCritPercent + effect.BonusCriticalChancePercent;
            if (effect.SchoolKey == "arcane") crit += request.Talents.Rank("arcane-impact") * 2m;
            if (effect.SchoolKey == "fire") crit += request.Talents.Rank("critical-mass") * 2m;
            if ((ability.Key is "frostbolt" or "ice-lance") && request.UseWintersChill)
                crit += state.WintersChillStacks * request.Talents.Rank("winters-chill");
            if (ability.Key == "ice-lance" && state.CurrentIceLanceFrozen)
                crit += request.Talents.Rank("shatter") * 10m;
            if (effect.CanCrit && (decimal)context.Random.NextDouble() * 100m < Math.Min(100m, crit))
            {
                var multiplier = effect.SchoolKey == "frost" || ability.Key == "frostfire-bolt" ?
                    1.5m + request.Talents.Rank("ice-shards") * .1m : 1.5m;
                if (arcane) multiplier += request.Talents.Rank("arcane-mind") * .02m;
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
        private AbilityExecutor? _executor;
        private int _arcaneStacks;
        private decimal _arcaneExpires;
        private int _heatingUp;
        private int _fingers;
        private bool _barrage;
        private bool _barrageCastPending;
        private decimal _winterExpires;
        private int _winter;
        public int MissileBarrageProcs { get; private set; }
        public int HeatingUpPyroblasts { get; private set; }
        public int FingersOfFrostProcs { get; private set; }
        public int FingersOfFrostIceLances { get; private set; }
        public int WintersChillStacks => _winter;
        public bool CurrentIceLanceFrozen { get; private set; }
        public MageSpecializationProcessor(MageSimRequest r, RotationProfile rotation, SimulationActorState actor)
        {
            _r = r; _rotation = rotation; _actor = actor;
            foreach (var a in actor.Abilities.Values)
                foreach (var effect in a.Definition.Effects)
                    _baseMultipliers[$"{a.Definition.Key}:{effect.Key}"] = effect.DamageMultiplier;
            RefreshRotation();
        }
        public void Attach(AbilityExecutor executor) => _executor = executor;
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
            if (_arcaneStacks > 0 && context.CurrentTimeSeconds >= _arcaneExpires)
            {
                _arcaneStacks = 0;
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
                        var bonus = ev.AbilityKey == "arcane-blast" ? 1m :
                            1m + _arcaneStacks * _r.Tuning.ArcaneBlastBonusDamagePerStackPercent / 100m;
                        foreach (var effect in castAbility.Definition.Effects)
                            effect.DamageMultiplier = _baseMultipliers[$"{castAbility.Definition.Key}:{effect.Key}"] * bonus *
                                (ev.AbilityKey == "ice-lance" && CurrentIceLanceFrozen ? 4m : 1m);
                    }
                    if (ev.AbilityKey != "arcane-blast" && _arcaneStacks > 0)
                    {
                        _arcaneStacks = 0; RefreshArcaneCost();
                    }
                    RefreshRotation();
                    break;
                case CombatEventType.AbilityCastCompleted:
                    if (ev.AbilityKey == "arcane-missiles") _barrageCastPending = false;
                    if (ev.AbilityKey == "arcane-blast")
                    {
                        _arcaneStacks = Math.Min(4, _arcaneStacks + 1);
                        _arcaneExpires = context.CurrentTimeSeconds + 8m;
                        RefreshArcaneCost();
                    }
                    if (_r.UseMissileBarrage && _r.Talents.Rank("missile-barrage") > 0 &&
                        (ev.AbilityKey is "arcane-blast" or "fireball" or "frostbolt" or "frostfire-bolt"))
                    {
                        var chance = ev.AbilityKey == "arcane-blast" ?
                            _r.Tuning.MissileBarrageArcaneBlastChancePercent : _r.Tuning.MissileBarrageOtherChancePercent;
                        if ((decimal)context.Random.NextDouble() * 100m < chance)
                        { _barrage = true; MissileBarrageProcs++; }
                    }
                    RefreshMissilesCost(); RefreshRotation();
                    break;
                case CombatEventType.Damage:
                    if (ev.AbilityKey == "ice-lance") CurrentIceLanceFrozen = false;
                    if ((ev.ResultKey is "hit" or "critical") && (ev.AbilityKey is "frostbolt" or "frostfire-bolt"))
                    {
                        if (_r.UseFingersOfFrost && _r.Talents.Rank("fingers-of-frost") > 0 &&
                            (decimal)context.Random.NextDouble() * 100m <
                            _r.Tuning.FingersOfFrostChancePercentPerRank * _r.Talents.Rank("fingers-of-frost"))
                        { _fingers = Math.Min(2, _fingers + 2); FingersOfFrostProcs++; }
                        if (ev.AbilityKey == "frostbolt" && _r.UseWintersChill)
                            { _winter = Math.Min(5, _winter + 1); _winterExpires = context.CurrentTimeSeconds + 15m; }
                    }
                    if (ev.IsCritical && !ev.IsPeriodic && !ev.IsChannelTick &&
                        (ev.AbilityKey is "fireball" or "frostfire-bolt" or "fire-blast"))
                    {
                        if (_r.UseHeatingUp && _r.Talents.Rank("heating-up") > 0)
                            _heatingUp = Math.Min(3, _heatingUp + 1);
                    }
                    if (ev.IsCritical && !ev.IsPeriodic && !ev.IsChannelTick &&
                        (ev.AbilityKey is "fireball" or "frostfire-bolt" or "fire-blast" or "pyroblast") &&
                        _r.UseIgnite && _r.Talents.Rank("ignite") > 0 && ev.Amount > 0m)
                    {
                        if (_actor.Abilities.TryGetValue("mage-ignite", out var ignite))
                        {
                            var tick = ev.Amount.GetValueOrDefault() * .04m * _r.Talents.Rank("ignite");
                            ignite.Definition.Effects[0].MinimumValue = tick;
                            ignite.Definition.Effects[0].MaximumValue = tick;
                            _executor?.TryStartAbility(context, "mage", "target", "mage-ignite");
                        }
                    }
                    RefreshRotation();
                    break;
                case CombatEventType.RotationDecision: RefreshRotation(); break;
            }
        }
        private void RefreshArcaneCost()
        {
            if (!_actor.Abilities.TryGetValue("arcane-blast", out var s)) return;
            s.Definition.ResourceCosts[0].Amount = _r.Tuning.ArcaneBlastMana *
                (1m + _arcaneStacks * _r.Tuning.ArcaneBlastExtraManaPerStackPercent / 100m);
        }
        private void RefreshMissilesCost()
        {
            if (!_actor.Abilities.TryGetValue("arcane-missiles", out var s)) return;
            s.Definition.ResourceCosts[0].Amount = _barrage || _barrageCastPending ? 0m : _r.Tuning.ArcaneMissilesMana;
        }
        private void RefreshRotation()
        {
            foreach (var entry in _rotation.Entries)
                entry.IsEnabled = entry.AbilityKey switch
                {
                    "arcane-missiles" => _barrage,
                    "arcane-blast" => !_barrage && _arcaneStacks < _r.ArcaneBlastsBeforeRelease,
                    "fireball" or "frostfire-bolt" when _r.Specialization == "arcane" =>
                        !_barrage && _arcaneStacks >= _r.ArcaneBlastsBeforeRelease,
                    "pyroblast" => _heatingUp >= 3,
                    "ice-lance" => _fingers > 0,
                    _ => true
                };
            RefreshMissilesCost();
        }
    }
}
