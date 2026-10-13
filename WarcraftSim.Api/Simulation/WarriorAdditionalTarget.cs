namespace WarcraftSim.Api.Simulation;

/// <summary>Scripted, temporary enemy in the same melee cluster as the boss.
/// Duration controls encounter availability, not health-based death.</summary>
public sealed class WarriorAdditionalTarget
{
    public string Key { get; set; } = "add-1";
    public string Name { get; set; } = "Encounter Add";
    public decimal SpawnTimeSeconds { get; set; } = 0m;
    public decimal DespawnTimeSeconds { get; set; } = 30m;
    public int Level { get; set; } = 63;
    public decimal Armor { get; set; } = 3500m;
    public decimal DodgePercent { get; set; } = 5m;
    public decimal ParryPercent { get; set; }
    public decimal BlockPercent { get; set; }
    public bool InCleaveRange { get; set; } = true;
}
