using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Rogue;

namespace WarcraftSim.Api.Simulation;

/// <summary>CP142 functional Rogue preview for level 60; equipment is still
/// represented by synthetic aggregate stats until real gear import.</summary>
public sealed class RogueSimRequest
{
    public int Seed { get; set; } = 142001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; }
    // Opt-in utility preview (control auras only; no invented PvP AI).
    public bool UseUtilityAbilities { get; set; }
    public string Specialization { get; set; } = "assassination";
    public decimal TargetMaximumHealth { get; set; } = 250000m;
    public decimal RogueMaximumHealth { get; set; } = 4000m;
    public decimal StartingEnergyPercent { get; set; } = 100m;
    public bool StartInStealth { get; set; } = true;
    public int AdditionalTargets { get; set; } = 0;
    // Optional incoming-attack scenario: disabled by default to retain CP142 DPS fixtures.
    public bool EnableBossAttacks { get; set; }
    public decimal BossSwingSeconds { get; set; } = 2m;
    public decimal BossHitDamage { get; set; } = 1200m;
    public bool UseExposeArmor { get; set; }
    public decimal TargetArmor { get; set; } = 0m;
    // Backward compatible toggle for CP142 presets. New simulations can configure
    // each weapon independently; charges decrement on successful applications.
    public bool UseInstantPoison { get; set; } = true;
    public string MainHandPoison { get; set; } = "instant";
    public string OffHandPoison { get; set; } = "instant";
    public int PoisonChargesPerWeapon { get; set; } = 100;
    public bool BothWeaponsDaggers { get; set; } = true;
    // Explicit training-target positioning. No implied pathfinding/movement AI.
    public bool IsBehindTarget { get; set; } = true;
    // Weapon types have actual effect on Hack and Slash. Kept separate from the
    // legacy BothWeaponsDaggers flag for backwards-compatible test presets.
    public string MainHandWeaponType { get; set; } = "dagger";
    public string OffHandWeaponType { get; set; } = "dagger";
    // Set non-zero to test Weapon Expertise against synthetic avoidance.
    public decimal TargetDodgePercent { get; set; }
    public decimal TargetParryPercent { get; set; }
    public ForeverRogueTalentBuild Talents { get; set; } = ForeverRogueTalentBuild.Default("assassination");
    public ForeverRogueTuning Tuning { get; set; } = new();
    public CharacterProfile Character { get; set; } = new()
    {
        Name = "Illustrative Rogue", ClassKey = "rogue", SpecializationKey = "assassination",
        RulesetKey = "wow-forever", Level = 60,
        BaseStats = new StatCollection { Values = new Dictionary<string, decimal>
        { ["agility"] = 250m, ["strength"] = 110m, ["attack-power"] = 300m,
          ["hit-chance-percent"] = 4m, ["attack-critical-chance-percent"] = 5m } }
    };
    public static RogueSimRequest CreatePreset(string spec)
    {
        var r = new RogueSimRequest
        {
            Specialization = spec,
            Talents = ForeverRogueTalentBuild.Default(spec),
            Seed = spec switch { "assassination" => 142001, "combat" => 142002,
                "subtlety" => 142003, _ => 142004 },
            BothWeaponsDaggers = spec != "combat",
            MainHandWeaponType = spec == "combat" ? "sword" : "dagger",
            OffHandWeaponType = spec == "combat" ? "sword" : "dagger"
        };
        r.Character.SpecializationKey = spec;
        r.Character.Name = $"Illustrative {spec} Rogue";
        return r;
    }
}

public sealed class RogueSimResponse
{
    public string Specialization { get; init; } = "";
    public decimal Dps { get; init; }
    public decimal DamageDone { get; init; }
    public decimal EndingEnergy { get; init; }
    public decimal MaximumEnergy { get; init; }
    public int EndingComboPoints { get; init; }
    public int PoisonProcs { get; init; }
    public int FinishersUsed { get; init; }
    public int StealthOpeners { get; init; }
    public Dictionary<string, decimal> DamageByAbility { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
