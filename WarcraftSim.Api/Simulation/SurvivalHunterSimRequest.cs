using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

public sealed class SurvivalHunterSimRequest
{
    public int Seed { get; set; } = 159001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public decimal TargetMaximumHealth { get; set; } = 1000000m;
    public int AdditionalTargets { get; set; } = 2;
    // "melee", "ranged" or "hybrid"; hybrid changes from melee to ranged once at SwitchAtSeconds.
    public string CombatMode { get; set; } = "hybrid";
    public decimal SwitchAtSeconds { get; set; } = 60m;
    public bool TargetIsTracked { get; set; } = true;
    public bool ApplyHuntersMark { get; set; } = true;
    public bool TriggerTraps { get; set; } = true;
    public decimal TrapTriggerDelaySeconds { get; set; } = .25m;
    // Optional scripted incoming dodges/parries for Mongoose Bite and Counterattack testing.
    public decimal? ScriptedAvoidanceEverySeconds { get; set; } = 12m;
    // Fire: explosive or immolation. Frost: frost or freezing. Each category shares its own cooldown.
    public string FireTrapKind { get; set; } = "explosive";
    public string FrostTrapKind { get; set; } = "frost";
    public bool UseFireTraps { get; set; } = true;
    public bool UseFrostTraps { get; set; } = true;
    public bool UseWingClip { get; set; } = true;
    public bool UseAimedShot { get; set; } = true;
    public bool UseMultiShot { get; set; } = true;
    public bool UseOffhandWeapon { get; set; }
    public bool HasPet { get; set; }
    public ForeverSurvivalHunterTalents Talents { get; set; } = new();
    public ForeverSurvivalHunterTuning Tuning { get; set; } = new();
}

public sealed class SurvivalHunterSimResponse
{
    public string Specialization { get; init; } = "survival";
    public decimal DamageDone { get; init; }
    public decimal Dps { get; init; }
    public decimal RangedDamage { get; init; }
    public decimal MeleeDamage { get; init; }
    public decimal TrapDamage { get; init; }
    public decimal EndingMana { get; init; }
    public Dictionary<string, decimal> DamageByAbility { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
