using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Reusable conditional extra *attack* processor. A landed eligible main-hand hit
/// rolls a seeded chance, then resolves one or more additional melee rolls with
/// the source's existing weapon snapshot and a bonus AP contribution. Triggered
/// swings do not reset the background timer or recursively trigger this proc.
/// </summary>
public sealed class TriggeredMeleeExtraAttackProcessor : ICombatEventProcessor
{
    private readonly AbilityExecutor _executor;
    private readonly string _sourceActorKey;
    private readonly AutoAttackDefinition _weapon;
    private readonly string _procAbilityKey;
    private readonly decimal _chancePercent;
    private readonly decimal _bonusAttackPower;
    private readonly int _attacksPerProc;
    private readonly decimal _internalCooldownSeconds;
    private readonly bool _grantsAutoAttackResources;
    private readonly Func<SimulationContext, bool>? _isEnabled;
    // Some main-hand melee specials (notably Bloodthirst) have no weapon hand
    // on their damage event. Only the caller-approved keys may act as triggers.
    private readonly HashSet<string> _additionalMainHandSpecialAbilityKeys;
    private decimal _nextEligibleAt;
    public int ProcCount { get; private set; }
    public int ExtraAttackCount { get; private set; }

    public TriggeredMeleeExtraAttackProcessor(AbilityExecutor executor, string sourceActorKey,
        AutoAttackDefinition mainHandWeapon, string procAbilityKey, decimal chancePercent,
        decimal bonusAttackPower, int attacksPerProc, decimal internalCooldownSeconds = 0m,
        bool grantsAutoAttackResources = false, Func<SimulationContext, bool>? isEnabled = null,
        IEnumerable<string>? additionalMainHandSpecialAbilityKeys = null)
    {
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(mainHandWeapon);
        if (string.IsNullOrWhiteSpace(sourceActorKey) || string.IsNullOrWhiteSpace(procAbilityKey) ||
            chancePercent is < 0m or > 100m || bonusAttackPower < 0m ||
            attacksPerProc is < 1 or > 5 || internalCooldownSeconds < 0m ||
            mainHandWeapon.SwingIntervalSeconds <= 0m)
            throw new ArgumentException("Invalid triggered melee extra attack configuration.");
        _executor = executor; _sourceActorKey = sourceActorKey;
        _weapon = mainHandWeapon; _procAbilityKey = procAbilityKey;
        _chancePercent = chancePercent; _bonusAttackPower = bonusAttackPower;
        _attacksPerProc = attacksPerProc; _internalCooldownSeconds = internalCooldownSeconds;
        _grantsAutoAttackResources = grantsAutoAttackResources; _isEnabled = isEnabled;
        _additionalMainHandSpecialAbilityKeys = new HashSet<string>(
            additionalMainHandSpecialAbilityKeys ?? [], StringComparer.OrdinalIgnoreCase);
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        var isMainHand = string.Equals(ev.WeaponHandKey, WeaponHandKeys.MainHand,
            StringComparison.OrdinalIgnoreCase);
        var isExplicitlyEligibleSpecial = ev.WeaponHandKey is null &&
            ev.AbilityKey is not null &&
            _additionalMainHandSpecialAbilityKeys.Contains(ev.AbilityKey);
        if (ev.Type != CombatEventType.Damage || ev.IsPeriodic || ev.IsChannelTick ||
            !string.Equals(ev.SourceActorKey, _sourceActorKey, StringComparison.OrdinalIgnoreCase) ||
            (!isMainHand && !isExplicitlyEligibleSpecial) ||
            ev.Amount is not > 0m || ev.TargetActorKey is null ||
            // Never chain a Windfury proc from an extra swing (even from a distinct provider).
            ev.AbilityKey is "windfury-totem-extra" or "windfury-weapon-extra" or "weaponmaster-sword-extra" ||
            string.Equals(ev.AbilityKey, _procAbilityKey, StringComparison.OrdinalIgnoreCase) ||
            context.CurrentTimeSeconds < _nextEligibleAt ||
            (_isEnabled is not null && !_isEnabled(context)) ||
            (decimal)context.Random.NextDouble() * 100m >= _chancePercent)
            return;
        var actor = context.GetActor(_sourceActorKey);
        var target = context.GetActor(ev.TargetActorKey);
        if (actor is null || target is null || !actor.IsAlive || !target.IsAlive) return;
        _nextEligibleAt = context.CurrentTimeSeconds + _internalCooldownSeconds;
        ProcCount++;
        var effect = CreateExtraSwingEffect(_weapon, _bonusAttackPower);
        for (var i = 0; i < _attacksPerProc && target.IsAlive; i++)
        {
            ExtraAttackCount++;
            var roll = _executor.ExecuteBackgroundDirectDamage(context, actor, target,
                _procAbilityKey, _procAbilityKey, effect, _weapon.DamageMultiplier);
            if (!roll.Landed || !_grantsAutoAttackResources) continue;
            foreach (var generation in _weapon.ResourceGenerations)
            {
                if (!actor.Resources.TryGetValue(generation.ResourceKey, out var resource)) continue;
                var desired = generation.AmountPerLandedSwing *
                    (roll.IsCritical ? generation.CriticalMultiplier : 1m) +
                    (roll.IsCritical ? generation.CriticalBonusAmountPerLandedSwing : 0m);
                var gained = resource.Gain(desired);
                if (gained <= 0m) continue;
                context.EmitEvent(new CombatEvent
                {
                    TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.ResourceChanged,
                    SourceActorKey = actor.Key, TargetActorKey = actor.Key,
                    AbilityKey = _procAbilityKey, ResourceKey = generation.ResourceKey,
                    Amount = gained, ResourceTotalAfter = resource.Current,
                    Description = $"{_procAbilityKey} granted {gained:0.##} {generation.ResourceKey}."
                });
            }
        }
    }

    private static AbilityEffectDefinition CreateExtraSwingEffect(AutoAttackDefinition weapon, decimal bonusAp)
    {
        var from = weapon.DamageEffect;
        var flat = bonusAp * weapon.SwingIntervalSeconds / 14m;
        return new AbilityEffectDefinition
        {
            Key = "extra-swing", EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = from.TargetType, SchoolKey = from.SchoolKey,
            WeaponHandKey = WeaponHandKeys.MainHand,
            ResolutionType = from.ResolutionType,
            AttackSkillStatKey = from.AttackSkillStatKey,
            TargetDefenseSkillStatKey = from.TargetDefenseSkillStatKey,
            MitigationType = from.MitigationType,
            CanMiss = from.CanMiss, CanBeDodged = from.CanBeDodged,
            CanBeParried = from.CanBeParried, CanBeBlocked = from.CanBeBlocked,
            CanGlance = from.CanGlance, CanCrush = from.CanCrush, CanCrit = from.CanCrit,
            MinimumValue = from.MinimumValue + flat, MaximumValue = from.MaximumValue + flat,
            ScalingStatKey = from.ScalingStatKey,
            ScalingCoefficient = from.ScalingCoefficient,
            DamageMultiplier = from.DamageMultiplier,
            DamageMultiplierStatKey = from.DamageMultiplierStatKey
        };
    }
}
