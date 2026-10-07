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
                            rule
                        )
                )
                .ToArray();

        return CombatRollContextAdjustment.Combine(
            adjustments
        );
    }
}
