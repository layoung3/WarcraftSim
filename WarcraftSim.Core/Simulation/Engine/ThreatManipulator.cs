using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Simulation.Engine;

public static class ThreatManipulator
{
    public static ThreatManipulationResult Apply(
        SimulationContext context,
        SimulationActorState threatOwner,
        SimulationActorState threatSource,
        string operation,
        decimal amount = 0m)
    {
        ArgumentNullException.ThrowIfNull(
            context
        );

        ArgumentNullException.ThrowIfNull(
            threatOwner
        );

        ArgumentNullException.ThrowIfNull(
            threatSource
        );

        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Threat manipulation amount cannot be negative."
            );
        }

        var previousThreat =
            threatOwner.ThreatTable.GetThreat(
                threatSource.Key
            );

        decimal currentThreat;

        if (string.Equals(
                operation,
                ThreatManipulationOperationTypes.Add,
                StringComparison.OrdinalIgnoreCase))
        {
            currentThreat =
                threatOwner.ThreatTable.AddThreat(
                    threatSource.Key,
                    amount
                );
        }
        else if (string.Equals(
                     operation,
                     ThreatManipulationOperationTypes.Set,
                     StringComparison.OrdinalIgnoreCase))
        {
            currentThreat =
                threatOwner.ThreatTable.SetThreat(
                    threatSource.Key,
                    amount
                );
        }
        else if (
            string.Equals(
                operation,
                ThreatManipulationOperationTypes.MatchHighest,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                operation,
                ThreatManipulationOperationTypes.MatchHighestPlus,
                StringComparison.OrdinalIgnoreCase)
        )
        {
            var eligibleActors =
                SimulationActorSemanticSelector
                    .ResolveMatching(
                        context,
                        threatOwner,
                        SimulationActorRelationshipTypes.Enemy,
                        includeSourceActor:
                            false
                    );

            var highestThreat =
                threatOwner.ThreatTable
                    .GetHighestThreatValue(
                        eligibleActors.Select(
                            actor =>
                                actor.Key
                        )
                    );

            var matchedThreat =
                string.Equals(
                    operation,
                    ThreatManipulationOperationTypes.MatchHighestPlus,
                    StringComparison.OrdinalIgnoreCase)
                    ? highestThreat +
                      amount
                    : highestThreat;

            currentThreat =
                threatOwner.ThreatTable.SetThreat(
                    threatSource.Key,
                    matchedThreat
                );
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown threat manipulation operation '{operation}'."
            );
        }

        return new ThreatManipulationResult
        {
            PreviousThreat =
                previousThreat,

            CurrentThreat =
                currentThreat
        };
    }
}
