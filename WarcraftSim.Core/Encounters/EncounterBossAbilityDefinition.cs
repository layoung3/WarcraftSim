using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Encounters;

/// <summary>Class-independent, serializable boss ability. An encounter runner
/// supplies actual actors; abilities never name a Warrior, Druid or Paladin.
/// All numbers are scenario inputs, not claimed Forever spell coefficients.</summary>
public sealed class EncounterBossAbilityDefinition
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "Boss Ability";
    public string SourceActorKey { get; set; } = "";
    public decimal FirstUseSeconds { get; set; }
    // Zero: use once. Positive: repeat from cast START at this interval.
    public decimal CooldownSeconds { get; set; }
    public decimal CastTimeSeconds { get; set; }
    // CP123: evaluation at each scheduled cast start, not at initial scheduling.
    // An empty list has no phase requirement. Only one listed phase must be active.
    public List<string> RequiredPhaseKeys { get; set; } = [];
    // Percent of this ability's source actor (0..100), using scripted health if present.
    public decimal? SourceHealthPercentAtOrBelow { get; set; }
    public decimal? SourceHealthPercentAtOrAbove { get; set; }
    // A caller may emit EncounterBossInterruptRequested for the active cast.
    public bool Interruptible { get; set; }

    // Selection resolves on impact by default, so threat swaps during the cast matter.
    public bool LockTargetsOnCastStart { get; set; }
    public string TargetActorKey { get; set; } = "";
    public EncounterTargetSelectionDefinition? TargetSelection { get; set; }
    public decimal DirectDamage { get; set; }
    public string SchoolKey { get; set; } = "physical";
    public string MitigationType { get; set; } = DamageMitigationTypes.None;
    // Static exposure coefficient, not a movement simulator.
    public decimal AreaExposurePercent { get; set; } = 100m;
    // Optional channel: ticks begin AFTER cast completion; no direct damage required.
    public int ChannelTickCount { get; set; }
    public decimal ChannelTickIntervalSeconds { get; set; }
    public decimal ChannelTickDamage { get; set; }
    // Optional timed aura / DoT, tagged for later dispel and interrupt mechanics.
    public string? DebuffKey { get; set; }
    public decimal DebuffDurationSeconds { get; set; }
    public string? DispelType { get; set; }
    public decimal DotTickIntervalSeconds { get; set; }
    public decimal DotTickDamage { get; set; }
}
