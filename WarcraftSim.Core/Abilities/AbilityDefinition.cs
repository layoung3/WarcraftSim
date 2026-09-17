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

    public bool IsOffGlobalCooldown { get; set; }

    public int MaxCharges { get; set; } = 1;

    public decimal? ChargeRecoverySeconds { get; set; }

    public List<AbilityResourceCost> ResourceCosts { get; set; } = [];

    public List<AbilityEffectDefinition> Effects { get; set; } = [];

    public List<string> Tags { get; set; } = [];

    public GameDataRecordMetadata Metadata { get; set; } = new();
}