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

    public AbilityEffectDefinition DamageEffect { get; set; } =
        new()
        {
            Key = "damage",
            EffectType = AbilityEffectTypes.DirectDamage,
            TargetType = AbilityTargetTypes.Enemy,
            WeaponHandKey = WeaponHandKeys.MainHand
        };
}
