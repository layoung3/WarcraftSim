using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Auras;
using WarcraftSim.Core.Simulation;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Weapon-local poison configuration and charges. Physical hits, not incoming attacks,
/// trigger applications. Deadly ticks are independent of weapon swings and maintain
/// per-target stacks and refreshable expiration. Numerical damage/proc parameters
/// are provisional; the source-of-truth client values are not yet available.
/// </summary>
public sealed class RoguePoisonProcessor : ICombatEventProcessor
{
    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "none", "instant", "deadly", "wound", "crippling", "mind-numbing" };

    private sealed class DeadlyState
    {
        public int Stacks;
        public decimal Expires;
        public decimal NextTick;
    }
    private readonly RogueSimRequest _request;
    private readonly SimulationActorState _rogue;
    private readonly AuraManager _auras;
    private readonly AbilityExecutor _executor;
    private readonly Dictionary<string, DeadlyState> _deadly = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _remaining = new(StringComparer.OrdinalIgnoreCase);
    public int Applications { get; private set; }
    public int DeadlyTicks { get; private set; }
    public int ChargesRemaining(string hand) => _remaining.GetValueOrDefault(hand);
    public int DeadlyStacks(string target) => _deadly.TryGetValue(target, out var s) ? s.Stacks : 0;

    public RoguePoisonProcessor(RogueSimRequest request, SimulationActorState rogue,
        AuraManager auras, AbilityExecutor executor)
    {
        _request = request; _rogue = rogue; _auras = auras; _executor = executor;
        _remaining[WeaponHandKeys.MainHand] = request.PoisonChargesPerWeapon;
        _remaining[WeaponHandKeys.OffHand] = request.PoisonChargesPerWeapon;
    }
    public void Process(SimulationContext ctx, CombatEvent ev)
    {
        if (ev.SourceActorKey != _rogue.Key) return;
        if (ev.Type == CombatEventType.RotationDecision && ev.EffectKey == "rogue-deadly-tick" &&
            ev.TargetActorKey is { } tickTarget)
        {
            Tick(ctx, tickTarget);
            return;
        }
        if (ev.Type != CombatEventType.Damage || ev.Amount is not > 0m || ev.IsPeriodic ||
            ev.WeaponHandKey is null || ev.TargetActorKey is null ||
            ev.AbilityKey is "instant-poison" or "deadly-poison" ||
            ev.AbilityKey is "blade-flurry-cleave") return;
        var hand = ev.WeaponHandKey;
        if (hand is not (WeaponHandKeys.MainHand or WeaponHandKeys.OffHand)) return;
        var poison = hand == WeaponHandKeys.MainHand ? _request.MainHandPoison : _request.OffHandPoison;
        if (poison == "none" || _remaining[hand] == 0) return;
        var venomActive = _rogue.ActiveAuras.Any(a => a.Definition.Key == "venom" &&
            a.IsActiveAt(ctx.CurrentTimeSeconds));
        var chance = Math.Clamp(_request.Tuning.InstantPoisonChancePercent +
            _request.Talents.Rank("improved-poisons") * 2m +
            (venomActive ? 10m : 0m), 0m, 100m);
        if ((decimal)ctx.Random.NextDouble() * 100m >= chance) return;
        var target = ctx.GetActor(ev.TargetActorKey);
        if (target is not { IsAlive: true }) return;
        var preservation = Math.Clamp(_request.Talents.Rank("improved-poisons") *
            _request.Tuning.PoisonChargePreservationChancePerRank, 0m, 100m);
        if ((decimal)ctx.Random.NextDouble() * 100m >= preservation)
            --_remaining[hand];
        Applications++;
        switch (poison)
        {
            case "instant":
                _executor.ExecuteBackgroundDirectDamage(ctx, _rogue, target,
                    "instant-poison", "Instant Poison", new AbilityEffectDefinition {
                        Key = "poison-hit", EffectType = AbilityEffectTypes.DirectDamage,
                        TargetType = AbilityTargetTypes.Enemy, SchoolKey = "nature",
                        ResolutionType = CombatResolutionTypes.Spell, CanMiss = true, CanCrit = true,
                        MinimumValue = _request.Tuning.PoisonDamage, MaximumValue = _request.Tuning.PoisonDamage
                    });
                break;
            case "deadly":
                ApplyDeadly(ctx, target);
                break;
            default:
                // Utility poisons do not invent DPS on a stationary training target.
                _auras.ApplyAura(ctx, target, new AuraDefinition {
                    Key = $"{poison}-poison", Name = $"{poison} poison", DurationSeconds =
                        _request.Tuning.UtilityPoisonDurationSeconds,
                    HealingReceivedMultiplier = poison == "wound"
                        ? 1m - _request.Tuning.WoundPoisonHealingReductionPercent / 100m
                        : 1m,
                    CastTimeMultiplier = poison == "mind-numbing"
                        ? _request.Tuning.MindNumbingPoisonCastTimeMultiplier
                        : 1m
                }, _rogue.Key, $"{poison}-poison", $"{poison}-debuff");
                break;
        }
    }
    private void ApplyDeadly(SimulationContext ctx, SimulationActorState target)
    {
        if (!_deadly.TryGetValue(target.Key, out var s))
            _deadly[target.Key] = s = new DeadlyState();
        var wasActive = s.Stacks > 0 && s.Expires > ctx.CurrentTimeSeconds;
        s.Stacks = wasActive ? Math.Min(5, s.Stacks + 1) : 1;
        s.Expires = ctx.CurrentTimeSeconds + _request.Tuning.DeadlyPoisonDurationSeconds;
        _auras.ApplyAura(ctx, target, new AuraDefinition {
            Key = "deadly-poison", Name = "Deadly Poison", DurationSeconds =
                _request.Tuning.DeadlyPoisonDurationSeconds, StackingMode = AuraStackingMode.Stack, MaxStacks = 5
        }, _rogue.Key, "deadly-poison", "deadly-stack");
        if (!wasActive)
        {
            s.NextTick = ctx.CurrentTimeSeconds + _request.Tuning.DeadlyPoisonTickIntervalSeconds;
            Schedule(ctx, target.Key, s.NextTick);
        }
    }
    private void Tick(SimulationContext ctx, string targetKey)
    {
        if (!_deadly.TryGetValue(targetKey, out var s) || s.Stacks == 0 ||
            ctx.CurrentTimeSeconds > s.Expires || ctx.CurrentTimeSeconds != s.NextTick) return;
        var target = ctx.GetActor(targetKey);
        if (target is not { IsAlive: true }) return;
        var amount = _request.Tuning.DeadlyPoisonDamagePerStack * s.Stacks *
            (1m + _request.Talents.Rank("vile-poisons") * .04m);
        var effective = target.TakeDamage(amount);
        ctx.EmitEvent(new CombatEvent {
            TimeSeconds = ctx.CurrentTimeSeconds, Type = CombatEventType.Damage,
            SourceActorKey = _rogue.Key, TargetActorKey = targetKey,
            AbilityKey = "deadly-poison", EffectKey = "deadly-tick", SchoolKey = "nature",
            ResultKey = CombatResultTypes.Hit, RawAmount = amount, Amount = effective,
            EffectDeliveryType = CombatEffectDeliveryType.Periodic,
            Description = $"Deadly Poison dealt damage at {s.Stacks} stack(s)."
        });
        DeadlyTicks++;
        s.NextTick += _request.Tuning.DeadlyPoisonTickIntervalSeconds;
        if (s.NextTick <= s.Expires) Schedule(ctx, targetKey, s.NextTick);
        else s.Stacks = 0;
    }
    private void Schedule(SimulationContext ctx, string target, decimal time)
    {
        if (time > ctx.Options.DurationSeconds) return;
        ctx.ScheduleEvent(new CombatEvent {
            TimeSeconds = time, Type = CombatEventType.RotationDecision,
            SourceActorKey = _rogue.Key, TargetActorKey = target,
            EffectKey = "rogue-deadly-tick", IsInternal = true
        });
    }
}
