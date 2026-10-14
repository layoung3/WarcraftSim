namespace WarcraftSim.Api.Simulation;

/// <summary>
/// CP125 controlled healing fixture. Values are CALIBRATION INPUTS, not
/// claimed level-60 Forever client spell ranks or talent calculations.
/// </summary>
public sealed class HolyPriestPreviewRequest
{
    public int Seed { get; set; } = 9924;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public bool EnableHealer { get; set; } = true;
    public decimal TankMaximumHealth { get; set; } = 3000m;
    public decimal BossSwingDamage { get; set; } = 360m;
    public decimal BossSwingIntervalSeconds { get; set; } = 2m;
    public decimal HealerMaximumHealth { get; set; } = 2000m;
    public decimal PriestMaximumMana { get; set; } = 5000m;
    public decimal PriestManaRegenPerSecond { get; set; } = 12m;
    public decimal FlashHealAmount { get; set; } = 680m;
    public decimal FlashHealManaCost { get; set; } = 180m;
    public decimal FlashHealCastSeconds { get; set; } = 1.5m;
    public decimal GreaterHealAmount { get; set; } = 1550m;
    public decimal GreaterHealManaCost { get; set; } = 410m;
    public decimal GreaterHealCastSeconds { get; set; } = 2.5m;
    public decimal GreaterHealHealthThresholdPercent { get; set; } = 50m;
    public decimal FlashHealHealthThresholdPercent { get; set; } = 85m;
}
