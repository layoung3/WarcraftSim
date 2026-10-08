namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Defines resource generation produced by one landed basic auto-attack.
/// The amount is attached to the base swing definition so attack-speed haste
/// changes swing frequency without changing the resource chunk per swing.
/// </summary>
public sealed class AutoAttackResourceGenerationDefinition
{
    public string ResourceKey { get; set; } = "";

    public decimal AmountPerLandedSwing { get; set; }

    public decimal CriticalMultiplier { get; set; } = 1m;
}
