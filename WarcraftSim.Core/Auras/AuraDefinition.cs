using WarcraftSim.Core.GameData;

namespace WarcraftSim.Core.Auras;

public sealed class AuraDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public decimal DurationSeconds { get; set; }

    public decimal? PeriodicTickIntervalSeconds { get; set; }

    public bool IncludeExpirationBoundaryTick { get; set; }

    public bool IsPeriodic =>
        PeriodicTickIntervalSeconds.HasValue &&
        PeriodicTickIntervalSeconds.Value > 0m;

    public AuraStackingMode StackingMode { get; set; } =
        AuraStackingMode.Refresh;

    public int MaxStacks { get; set; } = 1;

    // Generic, per-aura healing-received modifier. A value of 1 has no effect;
    // a value of .5 halves eligible healing. Resolve only active auras when a
    // healing event lands, allowing duration/expiry to affect real outcomes.
    public decimal HealingReceivedMultiplier { get; set; } = 1m;

    // Opt-in school-specific damage received from this aura's source only.
    // Resolved at impact against this target; null school or multiplier 1 is neutral.
    public string? SourceOwnedDamageSchoolKey { get; set; }

    public decimal SourceOwnedDamageMultiplier { get; set; } = 1m;

    // Separate cast-time slow. Applied at cast start, not by mutating an
    // ability definition, so instant casts and unrelated actors are unaffected.
    public decimal CastTimeMultiplier { get; set; } = 1m;

    public List<string> Tags { get; set; } = [];

    public GameDataRecordMetadata Metadata { get; set; } = new();
}