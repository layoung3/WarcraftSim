using WarcraftSim.Core.Abilities;

namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// Defines a one-shot attack that replaces the next swing from one specific
/// weapon-hand stream. The replacement owns its own combat profile so a queued
/// main-hand special never changes the rules used by an independent off-hand
/// white swing.
/// </summary>
public sealed class NextSwingReplacementDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public string WeaponHandKey { get; set; } =
        WeaponHandKeys.MainHand;

    public decimal DamageMultiplier { get; set; } = 1m;

    public string? DamageMultiplierStatKey { get; set; }

    /// <summary>
    /// Resources required by the queued attack. Availability is checked when
    /// the replacement is queued, but the cost is only paid when the matching
    /// weapon swing actually consumes the replacement.
    /// </summary>
    public List<AbilityResourceCost> ResourceCosts { get; set; } = [];

    /// <summary>
    /// Optional outcome-sensitive refunds for resources paid when this queued
    /// replacement is consumed. Refund rules are evaluated from the resolved
    /// combat result after the resource costs have been paid.
    /// </summary>
    public List<NextSwingResourceRefundDefinition> ResourceRefunds { get; set; } = [];

    public AbilityEffectDefinition DamageEffect { get; set; } =
        new()
        {
            Key = "damage",
            EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy,
            WeaponHandKey = WeaponHandKeys.MainHand
        };
}
