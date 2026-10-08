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

    public AbilityEffectDefinition DamageEffect { get; set; } =
        new()
        {
            Key = "damage",
            EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy
        };
}
