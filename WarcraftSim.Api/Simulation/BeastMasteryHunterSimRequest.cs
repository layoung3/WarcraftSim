using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

public sealed class BeastMasteryHunterSimRequest
{
    public int Seed { get; set; } = 163001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public decimal TargetMaximumHealth { get; set; } = 1000000m;
    public int AdditionalTargets { get; set; }
    public bool PetEnabled { get; set; } = true;
    public string PetFamily { get; set; } = "cat";
    public decimal PetEngagementDelaySeconds { get; set; } = .75m;
    public decimal PetUptimePercent { get; set; } = 100m;
    public decimal PetIncomingHitDamage { get; set; }
    public decimal PetIncomingHitEverySeconds { get; set; } = 3m;
    public bool UsePetSpecial { get; set; } = true;
    public bool UsePetBite { get; set; } = true;
    public bool UsePetClaw { get; set; } = true;
    public bool UseSerpentSting { get; set; } = true;
    public bool UseAimedShot { get; set; } = true;
    public bool UseMultiShot { get; set; } = true;
    public bool UseArcaneShot { get; set; } = true;
    public bool UseSummonHawk { get; set; } = true;
    public bool UseRapidFire { get; set; } = true;
    public bool UseBestialWrath { get; set; } = true;
    public bool UseIntimidation { get; set; } = true;
    public bool UseMendPet { get; set; } = true;
    public bool UseRevivePet { get; set; } = false;
    public bool UseAspectOfTheMonkey { get; set; } = false;
    public ForeverBeastMasteryHunterTalents Talents { get; set; } = new();
    public ForeverBeastMasteryHunterTuning Tuning { get; set; } = new();
}

public sealed class BeastMasteryHunterSimResponse
{
    public string Specialization { get; init; } = "beast-mastery";
    public string PetFamily { get; init; } = "cat";
    public decimal HunterDamage { get; init; }
    public decimal PetDamage { get; init; }
    public decimal HawkDamage { get; init; }
    public decimal DamageDone { get; init; }
    public decimal Dps { get; init; }
    public decimal PetDps { get; init; }
    public decimal PetAutoDamage { get; init; }
    public decimal PetSpecialDamage { get; init; }
    public decimal PetThreat { get; init; }
    public decimal HunterThreat { get; init; }
    public decimal PetFocus { get; init; }
    public decimal EndingMana { get; init; }
    public decimal PetActiveSeconds { get; init; }
    public decimal PetInactiveSeconds { get; init; }
    public bool PetDied { get; init; }
    public int PetCriticalHits { get; init; }
    public int PetMisses { get; init; }
    public Dictionary<string,decimal> DamageByAbility { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
