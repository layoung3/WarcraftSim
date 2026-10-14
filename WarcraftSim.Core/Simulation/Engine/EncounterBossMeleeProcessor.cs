using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Encounters;
using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Shared boss melee stream for solo, party and raid simulations. Selects a
/// living target at swing impact; a killed target cannot receive later hits.
/// Ruleset-specific modifiers (stances, Thunder Clap, etc.) are supplied by
/// adapters, keeping this processor independent of Warrior and Forever.
/// </summary>
public sealed class EncounterBossMeleeProcessor : ICombatEventProcessor
{
    public const string SwingEffectKey = "encounter-boss-melee-swing";

    private readonly EncounterBossMeleeDefinition _settings;
    private readonly ICombatRollResolver _rolls;
    private readonly IDamageMitigationResolver _mitigation;
    private readonly AbsorbManager _absorbs;
    private readonly Func<SimulationContext, decimal>? _intervalMultiplier;
    private readonly Func<SimulationContext, SimulationActorState, decimal, decimal>? _rawDamageModifier;
    private readonly AbilityDefinition _ability;
    private readonly AbilityEffectDefinition _effect;
    private decimal? _nextSwingAt;
    private int _generation;
    private string? _currentTargetKey;

    public string? CurrentTargetActorKey => _currentTargetKey;

    public EncounterBossMeleeProcessor(
        EncounterBossMeleeDefinition definition,
        ICombatRollResolver rolls,
        IDamageMitigationResolver mitigation,
        AbsorbManager? absorbs = null,
        Func<SimulationContext, decimal>? intervalMultiplier = null,
        Func<SimulationContext, SimulationActorState, decimal, decimal>? rawDamageModifier = null)
    {
        _settings = definition ?? throw new ArgumentNullException(nameof(definition));
        _rolls = rolls ?? throw new ArgumentNullException(nameof(rolls));
        _mitigation = mitigation ?? throw new ArgumentNullException(nameof(mitigation));
        _absorbs = absorbs ?? new AbsorbManager();
        _intervalMultiplier = intervalMultiplier;
        _rawDamageModifier = rawDamageModifier;
        Validate(definition);
        _ability = new AbilityDefinition
        {
            Key = definition.AbilityKey, Name = "Boss Melee Attack"
        };
        _effect = new AbilityEffectDefinition
        {
            Key = "boss-main-hand-effect", EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
            ResolutionType = definition.ResolutionType,
            MitigationType = DamageMitigationTypes.Armor,
            CanMiss = true, CanBeDodged = true, CanBeParried = true,
            CanBeBlocked = true, CanCrit = true,
            TargetDefenseSkillStatKey = definition.TargetDefenseSkillStatKey,
            CanCrush = definition.CanCrush
        };
    }

    public void ScheduleFirst(SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_settings.FirstSwingSeconds < context.Options.DurationSeconds)
            Schedule(context, _settings.FirstSwingSeconds);
    }

    public void Process(SimulationContext context, CombatEvent combatEvent)
    {
        // A player's parried frontal swing can accelerate the boss's pending
        // attack. The attacker is not restricted to any particular class.
        if (combatEvent.Type == CombatEventType.Damage &&
            _settings.EnableBossParryHaste &&
            string.Equals(combatEvent.TargetActorKey, _settings.BossActorKey,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(combatEvent.SourceActorKey, _settings.BossActorKey,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(combatEvent.ResultKey, CombatResultTypes.Parry,
                StringComparison.OrdinalIgnoreCase) &&
            _nextSwingAt is { } next && next > context.CurrentTimeSeconds)
        {
            var speed = Interval(context);
            var remaining = next - context.CurrentTimeSeconds;
            var shortened = Math.Max(speed * 0.20m, remaining - speed * 0.40m);
            if (shortened < remaining)
                Schedule(context, context.CurrentTimeSeconds + shortened);
            return;
        }

        if (combatEvent.Type != CombatEventType.RotationDecision ||
            !combatEvent.IsInternal ||
            !string.Equals(combatEvent.EffectKey, SwingEffectKey,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(combatEvent.SourceActorKey, _settings.BossActorKey,
                StringComparison.OrdinalIgnoreCase) ||
            combatEvent.EncounterSequenceHitNumber != _generation)
            return;

        var boss = context.GetActor(_settings.BossActorKey);
        if (boss is null || !boss.IsAlive) return;
        var victim = SelectTarget(context, boss);
        if (victim is not null)
            ResolveSwing(context, boss, victim);

        // Empty threat lists and dead targets do not permanently stop the
        // stream: future actors or threat changes may make a target available.
        Schedule(context, context.CurrentTimeSeconds + Interval(context));
    }

    private SimulationActorState? SelectTarget(
        SimulationContext context, SimulationActorState boss)
    {
        var eligible = context.Actors.Values.Where(actor =>
                actor.IsAlive &&
                !string.Equals(actor.Key, boss.Key, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(_settings.EligibleTargetTeamKey) ||
                 string.Equals(actor.TeamKey, _settings.EligibleTargetTeamKey,
                     StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(_settings.EligibleTargetTeamKey)
                    ? string.IsNullOrWhiteSpace(boss.TeamKey) ||
                      string.IsNullOrWhiteSpace(actor.TeamKey) ||
                      !string.Equals(actor.TeamKey, boss.TeamKey, StringComparison.OrdinalIgnoreCase)
                    : true))
            .OrderBy(actor => actor.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.Equals(_settings.TargetMode, EncounterBossMeleeTargetModes.Fixed,
                StringComparison.OrdinalIgnoreCase))
            return eligible.FirstOrDefault(actor =>
                string.Equals(actor.Key, _settings.PreferredTargetActorKey,
                    StringComparison.OrdinalIgnoreCase));

        var forced = boss.ForcedTarget;
        if (forced is not null && forced.IsActiveAt(context.CurrentTimeSeconds))
        {
            var taunted = eligible.FirstOrDefault(actor =>
                string.Equals(actor.Key, forced.ForcedTargetActorKey,
                    StringComparison.OrdinalIgnoreCase));
            if (taunted is not null)
            {
                _currentTargetKey = taunted.Key;
                return taunted;
            }
        }

        var current = eligible.FirstOrDefault(actor =>
            string.Equals(actor.Key, _currentTargetKey, StringComparison.OrdinalIgnoreCase));
        var highKey = boss.ThreatTable.GetHighestThreatActorKey(eligible.Select(a => a.Key));
        var highest = eligible.FirstOrDefault(actor =>
            string.Equals(actor.Key, highKey, StringComparison.OrdinalIgnoreCase));
        if (highest is null)
        {
            // Explicit preferred target is only used before any threat exists.
            current ??= eligible.FirstOrDefault(actor =>
                string.Equals(actor.Key, _settings.PreferredTargetActorKey,
                    StringComparison.OrdinalIgnoreCase));
            _currentTargetKey = current?.Key;
            return current;
        }

        var currentThreat = current is null ? 0m : boss.ThreatTable.GetThreat(current.Key);
        var contenderThreat = boss.ThreatTable.GetThreat(highest.Key);
        if (current is null || currentThreat <= 0m ||
            contenderThreat >= currentThreat * _settings.ThreatSwapThresholdPercent / 100m)
            current = highest;

        _currentTargetKey = current.Key;
        return current;
    }

    private void ResolveSwing(SimulationContext context,
        SimulationActorState boss, SimulationActorState victim)
    {
        var roll = _rolls.Resolve(context, boss, victim, _ability, _effect);
        decimal raw = 0m, mitigated = 0m, reduction = 0m, blocked = 0m;
        decimal absorbed = 0m, damage = 0m;
        if (roll.Landed)
        {
            var span = _settings.MaximumDamage - _settings.MinimumDamage;
            raw = (_settings.MinimumDamage +
                (decimal)context.Random.NextDouble() * span) * roll.AmountMultiplier;
            if (_rawDamageModifier is not null)
                raw = Math.Max(0m, _rawDamageModifier(context, victim, raw));
            var mitigation = _mitigation.Resolve(context, boss, victim, _ability, _effect, raw);
            mitigated = mitigation.MitigatedAmount;
            reduction = mitigation.ReductionPercent;
            blocked = roll.IsBlocked ? Math.Min(mitigation.FinalAmount, roll.BlockValue) : 0m;
            var incoming = Math.Max(0m, mitigation.FinalAmount - blocked);
            if (_settings.PreserveTargetHealth)
            {
                damage = incoming;
            }
            else
            {
                var result = _absorbs.ApplyDamage(context, victim, incoming,
                    boss.Key, _ability.Key, _effect.Key);
                absorbed = result.AbsorbedDamage;
                damage = result.HealthDamage;
            }
        }
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.Damage,
            SourceActorKey = boss.Key, TargetActorKey = victim.Key,
            AbilityKey = _ability.Key, EffectKey = _effect.Key,
            ResolutionType = _effect.ResolutionType, MitigationType = _effect.MitigationType,
            SchoolKey = _effect.SchoolKey, ResultKey = roll.ResultKey,
            IsCritical = roll.IsCritical, RawAmount = raw,
            MitigatedAmount = mitigated, MitigationPercent = reduction,
            BlockedAmount = blocked, AbsorbedAmount = absorbed, Amount = damage,
            Description = roll.Landed
                ? $"Boss swings at {victim.Name}: {roll.ResultKey}, {damage:0.##} damage ({blocked:0.##} blocked)."
                : $"Boss swings at {victim.Name}: {roll.ResultKey}."
        });
        if (!_settings.PreserveTargetHealth && !victim.IsAlive)
            context.EmitEvent(new CombatEvent
            {
                TimeSeconds = context.CurrentTimeSeconds,
                Type = CombatEventType.ActorDied,
                SourceActorKey = boss.Key, TargetActorKey = victim.Key,
                AbilityKey = _ability.Key,
                Description = $"{victim.Name} died to {_ability.Name}."
            });
    }

    private decimal Interval(SimulationContext context)
    {
        var multiplier = _intervalMultiplier?.Invoke(context) ?? 1m;
        if (multiplier <= 0m)
            throw new InvalidOperationException("Boss melee interval multiplier must be positive.");
        return _settings.SwingSpeedSeconds * multiplier;
    }

    private void Schedule(SimulationContext context, decimal at)
    {
        if (at >= context.Options.DurationSeconds)
        {
            _nextSwingAt = null;
            return;
        }
        _nextSwingAt = at;
        var generation = ++_generation;
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = at, Type = CombatEventType.RotationDecision,
            EffectKey = SwingEffectKey, IsInternal = true,
            EncounterSequenceHitNumber = generation,
            SourceActorKey = _settings.BossActorKey
        });
    }

    private static void Validate(EncounterBossMeleeDefinition value)
    {
        if (string.IsNullOrWhiteSpace(value.BossActorKey) ||
            string.IsNullOrWhiteSpace(value.AbilityKey) ||
            value.FirstSwingSeconds < 0m || value.SwingSpeedSeconds <= 0m ||
            value.MinimumDamage < 0m || value.MaximumDamage < value.MinimumDamage ||
            value.ThreatSwapThresholdPercent < 100m ||
            !new[] { EncounterBossMeleeTargetModes.Fixed, EncounterBossMeleeTargetModes.HighestThreat }
                .Contains(value.TargetMode, StringComparer.OrdinalIgnoreCase) ||
            (string.Equals(value.TargetMode, EncounterBossMeleeTargetModes.Fixed,
                StringComparison.OrdinalIgnoreCase) &&
             string.IsNullOrWhiteSpace(value.PreferredTargetActorKey)))
            throw new ArgumentException("Invalid boss melee encounter definition.", nameof(value));
    }
}
