using WarcraftSim.Core.Abilities;
using WarcraftSim.Core.Rulesets;

namespace WarcraftSim.Core.Simulation.Engine;

public sealed class CompositeCombatRollContextAdjustmentProvider :
    ICombatRollContextAdjustmentProvider
{
    private readonly IReadOnlyList<ICombatRollContextAdjustmentProvider>
        _providers;

    public CompositeCombatRollContextAdjustmentProvider(
        IEnumerable<ICombatRollContextAdjustmentProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(
            providers
        );

        _providers =
            providers.ToArray();
    }

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
        var adjustments =
            _providers
                .Select(
                    provider =>
                        provider.GetAdjustment(
                            context,
                            source,
                            target,
                            ability,
                            effect,
                            rule,
                            deliveryType
                        )
                )
                .ToArray();

        return CombatRollContextAdjustment.Combine(
            adjustments
        );
    }
}
