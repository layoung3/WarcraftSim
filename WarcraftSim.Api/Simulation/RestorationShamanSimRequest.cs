using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

/// <summary>Deterministic level-60 healing preview; no hidden full-health dummy.</summary>
public sealed class RestorationShamanSimRequest
{
    public int Seed { get; set; } = 143001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; }
    public decimal AllyMaximumHealth { get; set; } = 11000m;
    public decimal AllyInitialHealthPercent { get; set; } = 65m;
    public int AdditionalAllies { get; set; } = 3;
    // 0 preserves the CP143 unmitigated scenario. Set a positive value to evaluate
    // actual armor mitigation and critical-heal Ancestral Healing uptime.
    public decimal AllyPhysicalArmor { get; set; } = 0m;
    // Explicit model for nearby friendly bounce targets. Empty = all allies eligible.
    public List<string> ChainHealEligibleTargetKeys { get; set; } = [];
    public decimal IncomingDamage { get; set; } = 850m;
    public decimal DamageEverySeconds { get; set; } = 3m;
    public decimal FirstDamageAtSeconds { get; set; } = 2m;
    public bool DamageAdditionalAllies { get; set; } = true;
    // Optional controlled caster damage to verify Water Shield's incoming-hit
    // trigger independently from the attacker's own healing criticals.
    public decimal CasterIncomingDamage { get; set; }
    public decimal CasterDamageEverySeconds { get; set; } = 5m;
    public decimal FirstCasterDamageAtSeconds { get; set; } = 1m;
    public ForeverRestorationShamanTalents Talents { get; set; } = new();
    public ForeverRestorationShamanTuning Tuning { get; set; } = new();
}

public sealed class RestorationShamanSimResponse
{
    public decimal HealingDone { get; init; }
    public decimal OverhealingDone { get; init; }
    public decimal EndingMana { get; init; }
    public decimal MaximumMana { get; init; }
    public decimal ManaRestoredFromWaterShield { get; init; }
    public decimal ManaRestoredFromManaTide { get; init; }
    public int ChainHealBounces { get; init; }
    public decimal ArmorPreventedDamage { get; init; }
    public Dictionary<string, decimal> HealingByAbility { get; init; } = new();
    public Dictionary<string, decimal> AllyEndingHealth { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
