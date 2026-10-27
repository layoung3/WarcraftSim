using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Combat;

namespace WarcraftSim.Api.Simulation;

/// <summary>Per-iteration state for event-based Warrior weapon talents.
/// Uses the combat engine's seeded RNG. No permanent "average proc" stat
/// is substituted for a conditional talent effect.</summary>
public sealed class WarriorWeaponTalentProcessor : ICombatEventProcessor
{
    public const string FlurryHasteStat = "warrior-flurry-haste-percent";
    public const string SwordExtraAttackKey = "weaponmaster-sword-extra";
    private readonly AutoAttackProcessor _autoAttacks;
    private readonly AbilityExecutor _executor;
    private readonly WarriorSimRequest _request;
    private readonly int _flurryRank, _weaponmasterRank, _goreRank;
    private readonly AutoAttackDefinition? _mainHand, _offHand;
    private int _flurryCharges, _goreCharges;

    public WarriorWeaponTalentProcessor(WarriorSimRequest request,
        AutoAttackProcessor autoAttacks, AbilityExecutor executor,
        AutoAttackDefinition mainHand, AutoAttackDefinition? offHand)
    {
        _request = request;
        _autoAttacks = autoAttacks;
        _executor = executor;
        _mainHand = mainHand;
        _offHand = offHand;
        _flurryRank = ForeverWarriorTalentCatalog.Rank(request, "flurry");
        _weaponmasterRank = ForeverWarriorTalentCatalog.Rank(request, "weaponmaster");
        _goreRank = ForeverWarriorTalentCatalog.Rank(request, "gore-drinker");
    }

    public void Process(SimulationContext context, CombatEvent ev)
    {
        if (ev.SourceActorKey != "warrior") return;
        var actor = context.GetActor("warrior");
        if (actor is null || !actor.IsAlive) return;

        if (ev.Type == CombatEventType.AbilityCastCompleted && _goreRank > 0 &&
            (ev.AbilityKey is "bloodrage" or "bloodthirst" or "death-wish" or "berserker-rage" or "enrage"))
            _goreCharges = 3;

        if (ev.Type == CombatEventType.AutoAttackSwing && _flurryCharges > 0 &&
            (ev.AbilityKey is "main-hand-auto" or "off-hand-auto") &&
            ev.AutoAttackInstanceId.HasValue &&
            actor.AutoAttacks.TryGetValue(ev.AbilityKey, out var state) &&
            state.InstanceId == ev.AutoAttackInstanceId)
        {
            _flurryCharges--;
            if (_flurryCharges == 0) UpdateFlurry(context, actor);
        }

        if (ev.Type != CombatEventType.Damage || ev.IsPeriodic ||
            ev.WeaponHandKey is not ("main-hand" or "off-hand") ||
            (ev.Amount ?? 0m) <= 0m) return;

        if (_flurryRank > 0 && ev.IsCritical)
        {
            _flurryCharges = 3;
            UpdateFlurry(context, actor);
        }

        if (_goreCharges > 0)
        {
            _goreCharges--;
            var healed = actor.Heal(actor.MaximumHealth * _goreRank * 0.005m);
            if (healed > 0m)
                context.EmitEvent(new CombatEvent
                {
                    TimeSeconds = context.CurrentTimeSeconds, Type = CombatEventType.Healing,
                    SourceActorKey = actor.Key, TargetActorKey = actor.Key,
                    AbilityKey = "gore-drinker", Amount = healed,
                    Description = "Gore Drinker healed the Warrior."
                });
        }

        if (_weaponmasterRank == 0 || ev.AbilityKey == SwordExtraAttackKey) return;
        var weapon = ev.WeaponHandKey == "off-hand" ? _offHand : _mainHand;
        var weaponType = ev.WeaponHandKey == "off-hand" ? _request.OffHandWeaponType : _request.MainHandWeaponType;
        if (weapon is null || !string.Equals(weaponType, "sword", StringComparison.OrdinalIgnoreCase) ||
            context.Random.NextDouble() >= 0.01d * _weaponmasterRank) return;
        var target = ev.TargetActorKey is null ? null : context.GetActor(ev.TargetActorKey);
        if (target is null || !target.IsAlive) return;
        // A real extra melee roll, rather than a damage multiplier. This can
        // crit and trigger Deep Wounds, but cannot recursively trigger itself.
        var roll = _executor.ExecuteBackgroundDirectDamage(context, actor, target,
            SwordExtraAttackKey, "Weaponmaster (Sword)", weapon.DamageEffect,
            weapon.DamageMultiplier);
        if (roll.Landed)
        {
            foreach (var generation in weapon.ResourceGenerations)
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
                    AbilityKey = SwordExtraAttackKey, Amount = gained,
                     ResourceKey = generation.ResourceKey, ResourceTotalAfter = resource.Current,
                    Description = "Weaponmaster sword extra attack generated Rage."
                });
            }
        }
    }

    private void UpdateFlurry(SimulationContext context, SimulationActorState actor)
    {
        var oldFlurry = actor.Stats.Get(FlurryHasteStat);
        var nextFlurry = _flurryCharges > 0 ? _flurryRank * 5m : 0m;
        if (oldFlurry == nextFlurry) return;
        var oldHaste = actor.Stats.Get(ForeverCombatStatKeys.HastePercent);
        actor.Stats.Set(FlurryHasteStat, nextFlurry);
        actor.Stats.Set(ForeverCombatStatKeys.HastePercent, oldHaste - oldFlurry + nextFlurry);
        _autoAttacks.RescheduleForHasteChange(context, actor.Key,
            oldHaste, actor.Stats.Get(ForeverCombatStatKeys.HastePercent));
    }
}
