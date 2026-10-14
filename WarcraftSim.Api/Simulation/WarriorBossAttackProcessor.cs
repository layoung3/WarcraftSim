using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;
using WarcraftSim.Data.Forever.Abilities.Warrior;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Enemy white-swing stream for the Warrior preview. Uses the same physical
/// roll, mitigation, absorb and combat-event pipelines as normal attacks.
/// All tuning inputs are explicit on the request, not hard-coded Classic odds.
/// Future raid combat can replace the fixed warrior target with threat selection.
/// </summary>
public sealed class WarriorBossAttackProcessor : ICombatEventProcessor
{
    public const string SwingEffect = "warrior-preview-boss-swing";
    private readonly WarriorSimRequest _settings;
    private readonly ICombatRollResolver _rolls;
    private readonly IDamageMitigationResolver _mitigation;
    private decimal? _nextSwingAt;
    private int _swingGeneration;
    private readonly Func<decimal, bool>? _thunderClapActive;
    private readonly AbilityDefinition _ability = new()
    {
        Key = "boss-main-hand-auto", Name = "Boss Melee Attack"
    };
    private readonly AbilityEffectDefinition _effect;

    public WarriorBossAttackProcessor(WarriorSimRequest settings,
        ICombatRollResolver rolls, IDamageMitigationResolver mitigation,
        Func<decimal, bool>? thunderClapActive = null)
    {
        _settings = settings;
        _rolls = rolls;
        _mitigation = mitigation;
        _thunderClapActive = thunderClapActive;
        _effect = new AbilityEffectDefinition
        {
            Key = "boss-main-hand-effect", EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy, SchoolKey = "physical",
            ResolutionType = ForeverCombatResolutionTypes.CreatureMeleeAuto,
            MitigationType = DamageMitigationTypes.Armor,
            CanMiss = true, CanBeDodged = true, CanBeParried = true,
            CanBeBlocked = true, CanCrit = true,
            TargetDefenseSkillStatKey = ForeverCombatStatKeys.DefenseSkill,
            CanCrush = settings.BossMeleeCrushPercent > 0m
        };
    }

    public void ScheduleFirst(SimulationContext context)
    {
        if (!_settings.EnableBossAutoAttacks ||
            _settings.BossFirstSwingSeconds >= context.Options.DurationSeconds) return;
        Schedule(context, _settings.BossFirstSwingSeconds);
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        // Every successful parry against a Warrior attack can advance the boss's
        // next white swing (unless the encounter opts out). This must react to
        // misses/parries as well as landed damage, which is why it listens to
        // the resolved Damage event and not the damage amount.
        if (ev.Type == CombatEventType.Damage && _settings.BossParryHaste &&
            ev.SourceActorKey == "warrior" && ev.TargetActorKey == "target" &&
            ev.ResultKey == "parry" && _nextSwingAt is { } pending &&
            pending > context.CurrentTimeSeconds)
        {
            var currentInterval = Interval(context.CurrentTimeSeconds);
            var remaining = pending - context.CurrentTimeSeconds;
            var shortened = Math.Max(currentInterval * 0.20m,
                remaining - currentInterval * 0.40m);
            if (shortened < remaining)
                Schedule(context, context.CurrentTimeSeconds + shortened);
            return;
        }
        if (ev.Type != CombatEventType.RotationDecision ||
            ev.EffectKey != SwingEffect || !ev.IsInternal ||
            ev.EncounterSequenceHitNumber != _swingGeneration) return;
        var boss = context.GetActor("target");
        var warrior = context.GetActor("warrior");
        if (boss is null || warrior is null || !warrior.IsAlive || !boss.IsAlive) return;

        var roll = _rolls.Resolve(context, boss, warrior, _ability, _effect);
        decimal raw = 0m, mitigated = 0m, reduced = 0m, blocked = 0m, absorbed = 0m, dealt = 0m;
        if (roll.Landed)
        {
            var span = _settings.BossSwingMaximumDamage - _settings.BossSwingMinimumDamage;
            raw = (_settings.BossSwingMinimumDamage +
                (decimal)context.Random.NextDouble() * span) * roll.AmountMultiplier;
            // Berserker Stance takes 10% increased incoming physical damage.
            if (warrior.ActiveAuras.Any(a => a.Definition.Key == ForeverWarriorAuraKeys.BerserkerStance &&
                a.IsActiveAt(context.CurrentTimeSeconds)))
                raw *= 1.10m;
            else if (warrior.ActiveAuras.Any(a => a.Definition.Key == ForeverWarriorAuraKeys.DefensiveStance &&
                a.IsActiveAt(context.CurrentTimeSeconds)))
                raw *= 0.90m;
            if (warrior.Stats.Get(WarriorProtectionProcessor.ShieldWallActiveStat) > 0m)
                raw *= 0.40m;
            var armor = _mitigation.Resolve(context, boss, warrior, _ability, _effect, raw);
            mitigated = armor.MitigatedAmount;
            reduced = armor.ReductionPercent;
            blocked = roll.IsBlocked ? Math.Min(armor.FinalAmount, roll.BlockValue) : 0m;
            var incoming = Math.Max(0m, armor.FinalAmount - blocked);
            // Fixed-duration solo comparison: score the full incoming damage
            // without depleting HP. This is NOT a simulated healer, and the
            // summary's conventional death-rate is not a valid survival metric.
            // Keep using the absorb system for future explicit-healer encounters.
            dealt = incoming;
        }
        context.EmitEvent(new CombatEvent
        {
            TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.Damage,
            SourceActorKey = boss.Key, TargetActorKey = warrior.Key,
            AbilityKey = _ability.Key, EffectKey = _effect.Key,
            ResolutionType = _effect.ResolutionType, MitigationType = _effect.MitigationType,
            SchoolKey = _effect.SchoolKey, ResultKey = roll.ResultKey,
            IsCritical = roll.IsCritical, RawAmount = raw,
            MitigatedAmount = mitigated, MitigationPercent = reduced,
            BlockedAmount = blocked, AbsorbedAmount = absorbed, Amount = dealt,
            Description = roll.Landed
                ? $"Boss swings at Warrior: {roll.ResultKey}, {dealt:0.##} damage ({blocked:0.##} blocked)."
                : $"Boss swings at Warrior: {roll.ResultKey}."
        });
        Schedule(context, context.CurrentTimeSeconds + Interval(context.CurrentTimeSeconds));
    }

    private decimal Interval(decimal at) => _settings.BossSwingSpeedSeconds *
        (_thunderClapActive?.Invoke(at) == true ? 1.20m : 1m);

    private void Schedule(SimulationContext context, decimal at)
    {
        if (at >= context.Options.DurationSeconds) { _nextSwingAt = null; return; }
        _nextSwingAt = at;
        var generation = ++_swingGeneration;
        context.ScheduleEvent(new CombatEvent
        {
            TimeSeconds = at, Type = CombatEventType.RotationDecision,
            EffectKey = SwingEffect, IsInternal = true,
            EncounterSequenceHitNumber = generation,
            SourceActorKey = "target", TargetActorKey = "warrior"
        });
    }
}
