using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>Two live class controllers in a shared encounter context.</summary>
public sealed class WarriorPriestIntegratedRequest
{
    public WarriorSimRequest Warrior { get; set; } = new();
    public HolyPriestPreviewRequest Priest { get; set; } = new();
}

public sealed class WarriorPriestIntegratedResponse
{
    public bool TankSurvived { get; init; }
    public decimal? TankDeathTimeSeconds { get; init; }
    public decimal TankEndingHealth { get; init; }
    public decimal TankMaximumHealth { get; init; }
    public decimal TankDamageTaken { get; init; }
    public decimal TankBlockedDamage { get; init; }
    public decimal TankDamageDone { get; init; }
    public decimal TankThreat { get; init; }
    public decimal BossMeleeDamageDone { get; init; }
    public bool PriestDied { get; init; }
    public decimal PriestHealingDone { get; init; }
    public decimal PriestOverhealingDone { get; init; }
    public decimal PriestAbsorptionDone { get; init; }
    public decimal PriestThreat { get; init; }
    public decimal PriestEndingMana { get; init; }
    public decimal PriestMaximumMana { get; init; }
    public Dictionary<string, int> PriestSpellCasts { get; init; } = new();
    public int RaidMemberDeaths { get; init; }
    public List<string> ModelingNotes { get; init; } = [];
    public SimulationRunResult Run { get; init; } = new();
}
