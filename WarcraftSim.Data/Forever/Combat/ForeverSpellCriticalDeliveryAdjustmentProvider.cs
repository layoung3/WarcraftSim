using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;
using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Applies delivery-specific spell critical chance bonuses for WoW: Forever.
///
/// General spell critical chance still comes from the normal spell critical
/// stat. These two additive stats are for effects whose bonuses explicitly
/// include or exclude periodic effects. Forever currently distinguishes
/// periodic effects from channeled spells; channel ticks therefore use the
/// non-periodic bonus path.
/// </summary>
public sealed class ForeverSpellCriticalDeliveryAdjustmentProvider :
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
        return GetAdjustment(
            context,
            source,
            target,
            ability,
            effect,
            rule,
            CombatEffectDeliveryType.Direct
        );
    }

    public CombatRollContextAdjustment GetAdjustment(
        SimulationContext context,
        SimulationActorState source,
        SimulationActorState target,
        AbilityDefinition ability,
        AbilityEffectDefinition effect,
        CombatRollRuleDefinition rule,
        CombatEffectDeliveryType deliveryType)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            source
        );

        ArgumentNullException.ThrowIfNull(
            target
        );

        ArgumentNullException.ThrowIfNull(
            ability
        );

        ArgumentNullException.ThrowIfNull(
            effect
        );

        ArgumentNullException.ThrowIfNull(
            rule
        );

        if (!string.Equals(
                effect.ResolutionType,
                ForeverCombatResolutionTypes.PlayerSpell,
                StringComparison.OrdinalIgnoreCase))
        {
            return CombatRollContextAdjustment.None;
        }

        var statKey =
            deliveryType ==
                CombatEffectDeliveryType.Periodic
                ? ForeverCombatStatKeys
                    .PeriodicSpellCriticalChancePercent
                : ForeverCombatStatKeys
                    .NonPeriodicSpellCriticalChancePercent;

        var criticalChanceDelta =
            source.Stats.Get(
                statKey
            );

        if (criticalChanceDelta == 0m)
        {
            return CombatRollContextAdjustment.None;
        }

        return new CombatRollContextAdjustment
        {
            CriticalChancePercentDelta =
                criticalChanceDelta
        };
    }
}
