using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Restoration-specific effects that need per-target dynamic selection or
/// conditional mana returns. Ordinary casts, initial heals, Riptide HoT,
/// mana costs and cooldowns are resolved by Core's AbilityExecutor.
/// </summary>
public sealed class RestorationShamanCombatProcessor : ICombatEventProcessor
{
    private readonly ForeverRestorationShamanTuning _t;
    private readonly string _healer;
    private readonly AuraManager _auras;
    private readonly ForeverRestorationShamanTalents _talents;
    private int _shieldCharges;
    private decimal _nextShieldChargeAtSeconds;
    private readonly IReadOnlySet<string>? _eligibleChainTargetKeys;
    public decimal ShieldManaRestored { get; private set; }
    public decimal ManaTideRestored { get; private set; }
    public int ChainBounces { get; private set; }
    public int HealingStreamPulses { get; private set; }
    private int _streamGeneration;
    private int _tideGeneration;
    private decimal _tideExpiresAtSeconds;
    public int NatureSwiftnessConsumed { get; private set; }
    private readonly Dictionary<string, decimal> _normalCastTimes = new(StringComparer.OrdinalIgnoreCase);

    public RestorationShamanCombatProcessor(string healer, ForeverRestorationShamanTuning tuning,
        AuraManager auras, ForeverRestorationShamanTalents talents,
        IReadOnlySet<string>? eligibleChainTargetKeys = null)
    { _healer = healer; _t = tuning; _auras = auras; _talents = talents;
      _eligibleChainTargetKeys = eligibleChainTargetKeys; }

    public void Process(SimulationContext ctx, CombatEvent ev)
    {
        var healer = ctx.GetActor(_healer);
        if (healer is null || !healer.IsAlive) return;
        // Water Shield also responds to attacks hitting its caster.
        // Incoming damage comes from the enemy and cannot pass the
        // normal source-only filter used by outgoing healer mechanics.
        if (ev.Type == CombatEventType.Damage &&
            ev.TargetActorKey == _healer && ev.SourceActorKey != _healer &&
            ev.Amount is > 0m)
            TryTriggerWaterShield(ctx, healer);
        if (ev.SourceActorKey != _healer) return;
        // One Water elemental slot: replace the active water totem when the
        // alternative is summoned, including active periodic-heal auras.
        if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "mana-tide-totem")
        {
            _tideGeneration++;
            _tideExpiresAtSeconds = ctx.CurrentTimeSeconds + 12m;
            foreach (var ally in ctx.Actors.Values.Where(a => a.TeamKey == healer.TeamKey))
                _auras.RemoveAuras(ctx, ally, "healing-stream-totem", _healer,
                    "water totem replaced by Mana Tide");
        }
        // Replacement cancels future pulses. Natural expiration at exactly
        // twelve seconds does not suppress the scheduled boundary tick.
        if (ev.Type == CombatEventType.AuraRemoved &&
            ev.EffectKey == "mana-tide-aura" &&
            ctx.CurrentTimeSeconds < _tideExpiresAtSeconds)
            _tideGeneration++;
        if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "healing-stream-totem")
        {
            _auras.RemoveAuras(ctx, healer, "mana-tide-active", _healer,
                "water totem replaced by Healing Stream");
            _streamGeneration++;
            ScheduleStreamPulse(ctx, _streamGeneration, ctx.CurrentTimeSeconds + 2m);
        }
        if (ev.Type == CombatEventType.RotationDecision &&
            ev.EffectKey == "restoration-healing-stream-pulse")
        {
            if (ev.EncounterSequenceHitNumber == _streamGeneration &&
                healer.ActiveAuras.Any(a => a.Definition.Key == "healing-stream-totem" &&
                    a.IsActiveAt(ctx.CurrentTimeSeconds)))
            {
                ApplyHealingStreamPulse(ctx, healer);
                ScheduleStreamPulse(ctx, _streamGeneration, ctx.CurrentTimeSeconds + 2m);
            }
            return;
        }
        // Nature's Swiftness makes the next eligible cast heal instant. Restore
        // all original times when the aura is consumed or expires.
        if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "nature-swiftness")
        {
            foreach (var key in new[] { "healing-wave", "lesser-healing-wave", "chain-heal" })
                if (healer.Abilities.TryGetValue(key, out var state))
                {
                    _normalCastTimes.TryAdd(key, state.Definition.CastTimeSeconds);
                    state.Definition.CastTimeSeconds = 0m;
                }
        }
        if (ev.Type == CombatEventType.AbilityCastCompleted &&
            ev.AbilityKey is ("healing-wave" or "lesser-healing-wave" or "chain-heal") &&
            healer.ActiveAuras.Any(a => a.Definition.Key == "nature-swiftness-ready" &&
                a.IsActiveAt(ctx.CurrentTimeSeconds)))
        {
            NatureSwiftnessConsumed++;
            _auras.RemoveAuras(ctx, healer, "nature-swiftness-ready", _healer,
                "consumed by a Nature healing cast");
        }
        if (ev.Type == CombatEventType.AuraRemoved &&
            (ev.AbilityKey == "nature-swiftness" || ev.EffectKey == "nature-swiftness-aura"))
            foreach (var (key, castTime) in _normalCastTimes)
                if (healer.Abilities.TryGetValue(key, out var state))
                    state.Definition.CastTimeSeconds = castTime;
        if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "water-shield")
        {
            _shieldCharges = 3;
            _nextShieldChargeAtSeconds = ctx.CurrentTimeSeconds;
        }
        if (ev.Type == CombatEventType.AuraRemoved && ev.AbilityKey == "water-shield")
            _shieldCharges = 0;
        if (ev.Type == CombatEventType.AbilityCastCompleted && ev.AbilityKey == "chain-heal" &&
            ev.TargetActorKey is not null)
            ApplyChainBounces(ctx, healer, ev.TargetActorKey, ev.AbilityExecutionId);
        if (ev.Type == CombatEventType.Healing && ev.IsCritical &&
            _talents.Rank("ancestral-healing") > 0 && ev.TargetActorKey is { } healedKey)
        {
            var target = ctx.GetActor(healedKey);
            if (target is not null)
                _auras.ApplyAura(ctx, target, new WarcraftSim.Core.Auras.AuraDefinition {
                    Key = "ancestral-healing-armor", Name = "Ancestral Healing",
                    DurationSeconds = 15m
                }, _healer, "ancestral-healing", "armor-buff");
        }
        // October 16 beta: any critically successful Shaman spell may trigger
        // Water Shield; this healing preview generates healing spell crits only.
        if ((ev.Type is CombatEventType.Healing or CombatEventType.Damage) &&
            ev.IsCritical && !ev.IsPeriodic &&
            ev.AbilityKey != "water-shield")
            TryTriggerWaterShield(ctx, healer);
        if (ev.Type == CombatEventType.AuraApplied && ev.AbilityKey == "mana-tide-totem")
            for (var tick = 1; tick <= 4; tick++)
            {
                var time = ctx.CurrentTimeSeconds + tick * 3m;
                if (time < ctx.Options.DurationSeconds)
                    ctx.ScheduleEvent(new CombatEvent {
                        Type = CombatEventType.RotationDecision, TimeSeconds = time,
                        SourceActorKey = _healer, TargetActorKey = _healer,
                        EffectKey = "restoration-mana-tide-tick", IsInternal = true,
                        EncounterSequenceHitNumber = _tideGeneration
                    });
            }
        if (ev.Type == CombatEventType.RotationDecision &&
            ev.EffectKey == "restoration-mana-tide-tick" &&
            ev.EncounterSequenceHitNumber == _tideGeneration &&
            ctx.CurrentTimeSeconds <= _tideExpiresAtSeconds)
        {
            var restored = Gain(ctx, healer, healer.Resources["mana"].Maximum *
                _t.ManaTideTickPercent / 100m, "mana-tide-totem", "mana-tide-mana");
            ManaTideRestored += restored;
        }
    }

    private void TryTriggerWaterShield(SimulationContext ctx, SimulationActorState healer)
    {
        if (_shieldCharges <= 0 ||
            ctx.CurrentTimeSeconds < _nextShieldChargeAtSeconds ||
            !healer.ActiveAuras.Any(a => a.Definition.Key == "water-shield" &&
                a.IsActiveAt(ctx.CurrentTimeSeconds))) return;
        // 70532 tooltip: 2% of maximum Mana per charge, 3 charges.
        // The original flat 140-mana preview would scale incorrectly with gear.
        var gained = Gain(ctx, healer, healer.Resources["mana"].Maximum * .02m,
            "water-shield", "water-shield-charge");
        ShieldManaRestored += gained;
        _nextShieldChargeAtSeconds = ctx.CurrentTimeSeconds +
            _t.WaterShieldInternalCooldownSeconds;
        if (--_shieldCharges == 0)
            _auras.RemoveAuras(ctx, healer, "water-shield", _healer,
                "last charge consumed");
    }

    private void ScheduleStreamPulse(SimulationContext ctx, int generation, decimal time)
    {
        if (time >= ctx.Options.DurationSeconds) return;
        ctx.ScheduleEvent(new CombatEvent {
            TimeSeconds = time, Type = CombatEventType.RotationDecision,
            SourceActorKey = _healer, TargetActorKey = _healer,
            EffectKey = "restoration-healing-stream-pulse", IsInternal = true,
            EncounterSequenceHitNumber = generation
        });
    }

    private void ApplyHealingStreamPulse(SimulationContext ctx, SimulationActorState healer)
    {
        // Deterministic, simplified party-range contract: eligible alive team
        // members sorted by missing-health ratio. No invented geometry.
        // Healing Stream applies to all living members of the Shaman's
        // eligible group every pulse, not just the most-injured actor.
        // Group/range geometry is not available in the solo preview.
        var allies = ctx.Actors.Values.Where(a => a.TeamKey == healer.TeamKey && a.IsAlive)
            .OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase).ToArray();
        if (allies.Length == 0) return;
        var amount = _t.HealingStreamTick *
            (1m + _talents.Rank("restorative-totems") * .10m) *
            (1m + _talents.Rank("purification") * _t.PurificationHealingPercentPerRank / 100m);
        foreach (var target in allies)
        {
            // The aura is shared, but every actor has independent critical
            // and healing-received resolution through existing event types.
            var crit = (decimal)ctx.Random.NextDouble() * 100m <
                Math.Min(100m, _t.HealingCritPercent + _talents.Rank("tidal-mastery"));
            var raw = HealingReceivedModifiers.Apply(target, ctx.CurrentTimeSeconds,
                amount * (crit ? 1.5m : 1m));
            var effective = target.Heal(raw);
            ctx.EmitEvent(new CombatEvent {
                TimeSeconds = ctx.CurrentTimeSeconds, Type = CombatEventType.Healing,
                SourceActorKey = _healer, TargetActorKey = target.Key,
                AbilityKey = "healing-stream-totem", EffectKey = "healing-stream-tick",
                SchoolKey = "nature", RawAmount = raw, Amount = effective,
                OverhealingAmount = raw - effective, IsCritical = crit,
                ResultKey = crit ? CombatResultTypes.Critical : CombatResultTypes.Hit,
                EffectDeliveryType = CombatEffectDeliveryType.Periodic,
                Description = "Healing Stream pulse healed an eligible group member."
            });
        }
        HealingStreamPulses++;
    }

    private static decimal Gain(SimulationContext ctx, SimulationActorState caster, decimal amount,
        string ability, string effect)
    {
        caster.RefreshResources(ctx.CurrentTimeSeconds);
        var mana = caster.Resources["mana"];
        var gained = mana.Gain(amount);
        if (gained > 0m) ctx.EmitEvent(new CombatEvent {
            TimeSeconds = ctx.CurrentTimeSeconds, Type = CombatEventType.ResourceChanged,
            SourceActorKey = caster.Key, TargetActorKey = caster.Key,
            AbilityKey = ability, EffectKey = effect, ResourceKey = "mana",
            Amount = gained, ResourceTotalAfter = mana.Current
        });
        return gained;
    }

    private void ApplyChainBounces(SimulationContext ctx, SimulationActorState caster,
        string primaryKey, Guid? executionId)
    {
        var primary = ctx.GetActor(primaryKey);
        var riptideBonus = primary is not null && primary.ActiveAuras.Any(a =>
            a.Definition.Key == "riptide" && a.IsActiveAt(ctx.CurrentTimeSeconds)) ? 1.25m : 1m;
        var initial = (_t.ChainHealBase + _t.HealingPower * .714m) * riptideBonus *
            (1m + _talents.Rank("purification") * _t.PurificationHealingPercentPerRank / 100m);
        var allies = ctx.Actors.Values.Where(a => a.IsAlive && a.TeamKey == caster.TeamKey &&
                a.Key != primaryKey && a.CurrentHealth < a.MaximumHealth &&
                (_eligibleChainTargetKeys is null || _eligibleChainTargetKeys.Contains(a.Key)))
            .OrderBy(a => a.CurrentHealth / a.MaximumHealth)
            .ThenBy(a => a.Key, StringComparer.OrdinalIgnoreCase).Take(2).ToList();
        for (var i = 0; i < allies.Count; i++)
        {
            var target = allies[i];
            var amount = initial * (i == 0 ? _t.ChainHealBounceMultiplier :
                _t.ChainHealBounceMultiplier * _t.ChainHealBounceMultiplier);
            var crit = (decimal)ctx.Random.NextDouble() * 100m <
                Math.Min(100m, _t.HealingCritPercent + _talents.Rank("tidal-mastery"));
            var raw = HealingReceivedModifiers.Apply(target, ctx.CurrentTimeSeconds,
                amount * (crit ? 1.5m : 1m));
            var effective = target.Heal(raw);
            ctx.EmitEvent(new CombatEvent {
                TimeSeconds = ctx.CurrentTimeSeconds, Type = CombatEventType.Healing,
                SourceActorKey = caster.Key, TargetActorKey = target.Key,
                AbilityKey = "chain-heal", AbilityExecutionId = executionId,
                EffectKey = $"chain-heal-bounce-{i + 1}", SchoolKey = "nature",
                ResultKey = crit ? CombatResultTypes.Critical : CombatResultTypes.Hit,
                RawAmount = raw, Amount = effective, OverhealingAmount = raw - effective,
                IsCritical = crit, Description = "Chain Heal selected an injured friendly bounce target."
            });
            ChainBounces++;
        }
    }
}
