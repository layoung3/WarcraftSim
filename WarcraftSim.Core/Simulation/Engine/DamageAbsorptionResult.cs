namespace WarcraftSim.Core.Simulation.Engine;

public sealed class DamageAbsorptionResult
{
    public decimal IncomingDamage { get; init; }

    public decimal AbsorbedDamage { get; init; }

    public decimal HealthDamage { get; init; }
}
