namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Explicit, ordered single-target priority rule. Conditions use an AND relationship.
/// Future class simulators should share the Core RotationProfile and condition engine,
/// not reuse this Warrior-specific API transport object.
/// </summary>
public sealed class WarriorRotationRule
{
    public string Action { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public decimal? MinimumRage { get; set; }
    public decimal? MaximumRage { get; set; }
    public decimal? TargetHealthAtOrBelowPercent { get; set; }
    public decimal? MinimumAttackPower { get; set; }
    public decimal? MaximumAttackPower { get; set; }
    public string? OnlyWhenAbilityOnCooldown { get; set; }
}
