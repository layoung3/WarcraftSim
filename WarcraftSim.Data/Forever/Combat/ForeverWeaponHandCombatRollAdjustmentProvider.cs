using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Applies Forever weapon-hand-only roll modifiers. Keeping this contextual
/// prevents off-hand talent bonuses from leaking into main-hand or unrelated
/// melee attacks.
/// </summary>
public sealed class ForeverWeaponHandCombatRollAdjustmentProvider :
    ICombatRollContextAdjustmentProvider
{
    public CombatRollContextAdjustment GetAdjustment(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(rule);

        if (!string.Equals(
                effect.WeaponHandKey,
                WeaponHandKeys.OffHand,
                StringComparison.OrdinalIgnoreCase))
        {
            return CombatRollContextAdjustment.None;
        }

        var offHandHitBonus =
            source.Stats.Get(
                ForeverCombatStatKeys.OffHandHitChancePercent
            );

        if (offHandHitBonus == 0m)
        {
            return CombatRollContextAdjustment.None;
        }

        return new CombatRollContextAdjustment
        {
            HitChancePercentDelta =
                offHandHitBonus
        };
    }
}
