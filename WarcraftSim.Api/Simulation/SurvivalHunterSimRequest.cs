using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Characters.Equipment;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

public sealed class SurvivalHunterSimRequest
{
    public CharacterEquipmentLoadout Equipment { get; set; } = new();
    // CP173 opt-in verified/provisional external buff and target-debuff IDs.
    public ForeverExternalModifierSelection ExternalModifiers { get; set; } = new();
    // Optional direct/provisional naked level-60 observation; never an implicit
    // fallback to unrelated Classic statistics.
    public ForeverLevel60BaselineObservation? Level60Baseline { get; set; }
    public bool UseReviewedLevel60Baseline { get; set; }
    public bool UseProvisionalLevel60Baseline { get; set; } // explicit WoWSims Classic historical proxy; never Forever-verified // fails closed when no approved race/class observation exists
    public bool UseRacialActive { get; set; } // opt in to sourced Orc Blood Fury
    public string? Race { get; set; } // opt-in; omitted preserves numeric-only previews
    public int Seed { get; set; } = 159001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public decimal TargetMaximumHealth { get; set; } = 1000000m;
    public int AdditionalTargets { get; set; } = 2;
    // "weave" (melee first) is the default. Legacy hybrid one-way mode remains
    // available through advanced JSON only for deterministic regression tests.
    public string CombatMode { get; set; } = "weave";
    // Explicit owner priority override; stance, cast and target gates remain intact.
    public List<string> CustomPriority { get; set; } = [];
    public decimal SwitchAtSeconds { get; set; } = 60m;
    // Movement travel is applied in each direction; .75s = 1.5s round trip.
    public decimal WeaveTravelSeconds { get; set; } = .75m;
    public decimal WeaveMinimumMeleeSeconds { get; set; } = 18m;
    public decimal WeaveManaReserve { get; set; } = 125m;
    public bool TargetIsTracked { get; set; } = true;
    public bool ApplyHuntersMark { get; set; } = true;
    public bool TriggerTraps { get; set; } = true;
    public decimal TrapTriggerDelaySeconds { get; set; } = .25m;
    public bool TriggerRangedTraps { get; set; } = false;
    public bool BossImmuneToRoots { get; set; } = true;
    public decimal? ScriptedAvoidanceEverySeconds { get; set; } = 12m;
    public string FireTrapKind { get; set; } = "explosive";
    public string FrostTrapKind { get; set; } = "frost";
    public bool UseFireTraps { get; set; } = true;
    // Frost Trap is control/utility and should not consume DPS GCDs by default.
    public bool UseFrostTraps { get; set; } = false;
    // Wing Clip is similarly utility unless specifically requested by the encounter.
    public bool UseWingClip { get; set; } = false;
    public bool UseAimedShot { get; set; } = true;
    public bool UseAspectOfTheBeast {get;set;}
    public bool UseAspectOfTheHawk {get;set;}
    public bool UseVolley {get;set;}
    public bool UseRapidFire { get; set; } = true;
    public bool UseMultiShot { get; set; } = true;
    public bool UseOffhandWeapon { get; set; }
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
    public ForeverSurvivalHunterTalents Talents { get; set; } = new();
    public ForeverSurvivalHunterTuning Tuning { get; set; } = new();
}

public sealed class SurvivalHunterSimResponse
{
    public string Specialization { get; init; } = "survival";
    public decimal DamageDone { get; init; }
    public decimal HunterDamage { get; init; }
    public decimal PetDamage { get; init; }
    public decimal HawkDamage { get; init; }
    public decimal HawkThreat { get; init; }
    public int HawkSummons { get; init; }
    public decimal PetThreat { get; init; }
    public decimal PetFocus { get; init; }
    public decimal PetUptimeSeconds { get; init; }
    public bool PetDied { get; init; }
    public decimal Dps { get; init; }
    public decimal RangedDamage { get; init; }
    public decimal MeleeDamage { get; init; }
    public decimal TrapDamage { get; init; }
    public decimal EndingMana { get; init; }
    public Dictionary<string, decimal> DamageByAbility { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
