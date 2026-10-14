namespace WarcraftSim.Api.Simulation;

/// <summary>Per-actor assigned responsibility. A future raid sim can assign it to one or more roster members.</summary>
public sealed class WarriorMaintenancePolicy
{
    public string AbilityKey { get; set; } = "";
    public int DesiredStacks { get; set; } = 1;
    public decimal RefreshWhenRemainingSeconds { get; set; } = 2m;
}

/// <summary>
/// Time-aware major cooldown policy. Never invents uses of an unselected racial or talent.
/// FinalWindowSeconds with HoldLastUse=true holds an otherwise-ready second use
/// when its remaining cooldown would prevent alignment with the final window.
/// </summary>
public sealed class WarriorMajorCooldownPolicy
{
    public string AbilityKey { get; set; } = "";
    public string Mode { get; set; } = "on-cooldown"; // on-cooldown | at-times
    public decimal OpeningDelaySeconds { get; set; }
    public bool HoldLastUse { get; set; }
    public decimal FinalWindowSeconds { get; set; } = 35m;
    public List<decimal> PlannedUseSeconds { get; set; } = [];
    // CP123: Only for Mode = on-boss-cast. Empty matches all boss casts.
    public List<string> TriggerBossAbilityKeys { get; set; } = [];
    // Expected raw damage from cast + channel + DoT, relative to maximum HP.
    public decimal TriggerMinimumRawDamagePercentOfMaxHealth { get; set; } = 0m;
}
