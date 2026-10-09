using WarcraftSim.Core.GameData;

namespace WarcraftSim.Core.Abilities;

public sealed class AbilityDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public string? ClassKey { get; set; }

    public string? SpecializationKey { get; set; }

    public int RequiredLevel { get; set; }

    public decimal CooldownSeconds { get; set; }

    public decimal GlobalCooldownSeconds { get; set; } = 1.5m;

    public decimal CastTimeSeconds { get; set; }

    public decimal ChannelDurationSeconds { get; set; }

    public decimal ChannelTickIntervalSeconds { get; set; }

    public bool IsChanneled =>
        ChannelDurationSeconds > 0m;

    public bool IsOffGlobalCooldown { get; set; }

    // Passive proc abilities are valid sources of aura ticks but must never
    // be directly cast by a player or rotation executor.
    public bool IsPassive { get; set; }

    public int MaxCharges { get; set; } = 1;

    public decimal? ChargeRecoverySeconds { get; set; }

    public List<AbilityResourceCost> ResourceCosts { get; set; } = [];

    // Optional source-side aura requirements evaluated when an ability starts.
    // This is useful for state-gated abilities such as Whirlwind requiring
    // Berserker Stance, while remaining generic for future form/buff gates.
    public List<string> RequiredSourceAuraKeys { get; set; } = [];

    // Optional execution gate for finisher-style abilities. When set, the
    // target must be at or below this percentage of maximum health when the
    // action starts. Null means no health-threshold restriction.
    public decimal? MaximumTargetHealthPercent { get; set; }

    // Optional source-owned, target-specific reaction window (e.g. Overpower).
    // Opportunities are granted by a combat-event processor, and spent only
    // on successfully completed casts; unrelated targets cannot share them.
    public string? RequiredTargetOpportunityKey { get; set; }

    // Resource consumed after ordinary costs are paid at cast completion.
    // This supports abilities such as Execute that spend a fixed base cost,
    // then convert some or all remaining resource into additional effect.
    public List<AbilityAdditionalResourceConsumption>
        AdditionalResourceConsumptions { get; set; } = [];

    // Cast-time abilities such as Forever Slam can temporarily interrupt one
    // or more background weapon-swing streams. Matching active auto-attacks
    // are suspended when the cast starts and restart with a fresh full swing
    // interval when the cast completes or is cancelled. Empty preserves the
    // existing swing timers.
    public List<string> DelayedAutoAttackWeaponHandKeys { get; set; } = [];

    public List<AbilityEffectDefinition> Effects { get; set; } = [];

    public List<string> Tags { get; set; } = [];

    public GameDataRecordMetadata Metadata { get; set; } = new();
}