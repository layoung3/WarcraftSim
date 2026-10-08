using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Defines one independently-timed background weapon swing stream.
/// The damage effect is resolved through the same combat-roll,
/// mitigation, absorb, threat, and reporting pipeline as ability damage.
/// </summary>
public sealed class AutoAttackDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public decimal SwingIntervalSeconds { get; set; }

    public string WeaponHandKey { get; set; } =
        WeaponHandKeys.MainHand;

    // Multiplicative damage applied before critical/block/mitigation. The
    // optional stat is interpreted as an additive percentage to this base
    // multiplier, allowing hand-specific talents to update live.
    public decimal DamageMultiplier { get; set; } = 1m;

    public string? DamageMultiplierStatKey { get; set; }

    public AbilityEffectDefinition DamageEffect { get; set; } =
        new()
        {
            Key = "damage",
            EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy
        };
}
