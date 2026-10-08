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

    public int MaxCharges { get; set; } = 1;

    public decimal? ChargeRecoverySeconds { get; set; }

    public List<AbilityResourceCost> ResourceCosts { get; set; } = [];

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