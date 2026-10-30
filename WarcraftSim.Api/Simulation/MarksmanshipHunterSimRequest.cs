using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>Fixed-position level-60 no-pet Marksmanship preview (no movement/raid buffs).</summary>
public sealed class MarksmanshipHunterSimRequest
{
    public CharacterEquipmentLoadout Equipment { get; set; } = new();
    public int Seed { get; set; } = 157001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public decimal TargetMaximumHealth { get; set; } = 1000000m;
    public int AdditionalTargets { get; set; }
    // Optional independent add health for repeatable kill-proc tests. Defaults to boss health.
    public decimal? AdditionalTargetMaximumHealth { get; set; }
    // Optional full priority override; Auto Shot remains its independent swing stream.
    public List<string> CustomPriority { get; set; } = [];
    public bool UseSerpentSting { get; set; } = true;
    public bool UseAimedShot { get; set; } = true;
    public bool UseMultiShot { get; set; } = true;
    public bool UseArcaneShot { get; set; } = true;
    public bool UseSniperShot { get; set; } = true;
    public bool UseRapidFire { get; set; } = true;
    public bool UseAspectOfTheHawk { get; set; } // opt-in, preserves CP166 baseline
    public bool UseVolley { get; set; } // target-set AoE opt-in
    // Pet support will be added with Beast Mastery; reject it instead of silently
    // awarding Lone Wolf with a fictional active pet.
    public bool HasPet { get; set; }
    public string PetFamily { get; set; } = "cat";
    // Existing explicit encounter actor ("target" or "add-N").
    public string PetTargetKey { get; set; } = "target";
    public ForeverBeastMasteryHunterTuning PetTuning { get; set; } = new();
    // Actual off-tree BM points, independent from full BM default preset.
    public ForeverBeastMasteryHunterTalents PetTalents { get; set; } = new() {
        Ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase)
    };
    public decimal PetEngagementDelaySeconds { get; set; } = .75m;
    public decimal PetIncomingHitDamage { get; set; }
    public decimal PetIncomingHitEverySeconds { get; set; } = 3m;
    public bool UsePetSpecial { get; set; } = true;
    public bool UsePetBite { get; set; } = true;
    public bool UsePetClaw { get; set; } = true;
    public bool UseRevivePet { get; set; }
    public List<string> PetAbilityPriority { get; set; } = [];
    public ForeverMarksmanshipHunterTalents Talents { get; set; } = new();
    public ForeverMarksmanshipHunterTuning Tuning { get; set; } = new();
}

public sealed class MarksmanshipHunterSimResponse
{
    public string Specialization { get; init; } = "marksmanship";
    public decimal Dps { get; init; }
    public decimal DamageDone { get; init; }
    public decimal HunterDamage { get; init; }
    public decimal PetDamage { get; init; }
    public decimal PetThreat { get; init; }
    public decimal PetFocus { get; init; }
    public decimal PetUptimeSeconds { get; init; }
    public bool PetDied { get; init; }
    public decimal EndingMana { get; init; }
    public decimal MaximumMana { get; init; }
    public decimal RangedAttackPower { get; init; }
    public int RapidFireCasts { get; init; }
    public int SerpentStingApplications { get; init; }
    public Dictionary<string, decimal> DamageByAbility { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
