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

    // Multiplicative critical adjustment retained for generic rulesets.
    public decimal CriticalMultiplier { get; set; } = 1m;

    // Optional additive critical resource share. This supports Forever's
    // normalized Warrior rule where a two-handed crit adds one one-hand base
    // Rage share instead of multiplying the entire two-hand Rage chunk.
    public decimal CriticalBonusAmountPerLandedSwing { get; set; }
}
