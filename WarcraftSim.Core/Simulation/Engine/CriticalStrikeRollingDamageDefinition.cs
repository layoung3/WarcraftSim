using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// A passive periodic-damage proc driven by an eligible direct critical strike.
/// The proc's weapon damage inputs are deliberately independent of AP-scaled
/// damage, mitigation, critical multipliers, and off-hand damage multipliers.
/// </summary>
public sealed class CriticalStrikeRollingDamageDefinition
{
    public string Key { get; set; } = "";

    // This ability represents a passive, periodic effect for event attribution
    // and ordinary aura ticks. It is never an action in the rotation.
    public AbilityDefinition PeriodicAbility { get; set; } = new();

    public string PeriodicEffectKey { get; set; } = "damage";

    public decimal DamageFractionOfWeaponAverage { get; set; }

    public Dictionary<string, decimal> AverageBaseWeaponDamageByHand { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    // The ruleset decides which damage-resolution types count as melee.
    public HashSet<string> EligibleResolutionTypes { get; } =
        new(StringComparer.OrdinalIgnoreCase);
}
