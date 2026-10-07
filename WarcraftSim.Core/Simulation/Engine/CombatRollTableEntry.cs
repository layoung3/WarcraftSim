namespace WarcraftSim.Core.Simulation.Engine;

public sealed class CombatRollTableEntry
{
    public decimal ChancePercent { get; init; }

    public CombatRollResult Result { get; init; } =
        CombatRollResult.Hit();
}
