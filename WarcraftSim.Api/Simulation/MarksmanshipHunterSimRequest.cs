using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Data.Forever.Hunter;

namespace WarcraftSim.Api.Simulation;

/// <summary>Fixed-position level-60 no-pet Marksmanship preview (no movement/raid buffs).</summary>
public sealed class MarksmanshipHunterSimRequest
{
    public int Seed { get; set; } = 157001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public decimal TargetMaximumHealth { get; set; } = 1000000m;
    public int AdditionalTargets { get; set; }
    // Optional independent add health for repeatable kill-proc tests. Defaults to boss health.
    public decimal? AdditionalTargetMaximumHealth { get; set; }
    public bool UseSerpentSting { get; set; } = true;
    public bool UseAimedShot { get; set; } = true;
    public bool UseMultiShot { get; set; } = true;
    public bool UseArcaneShot { get; set; } = true;
    public bool UseSniperShot { get; set; } = true;
    public bool UseRapidFire { get; set; } = true;
    // Pet support will be added with Beast Mastery; reject it instead of silently
    // awarding Lone Wolf with a fictional active pet.
    public bool HasPet { get; set; }
    public ForeverMarksmanshipHunterTalents Talents { get; set; } = new();
    public ForeverMarksmanshipHunterTuning Tuning { get; set; } = new();
}

public sealed class MarksmanshipHunterSimResponse
{
    public string Specialization { get; init; } = "marksmanship";
    public decimal Dps { get; init; }
    public decimal DamageDone { get; init; }
    public decimal EndingMana { get; init; }
    public decimal MaximumMana { get; init; }
    public decimal RangedAttackPower { get; init; }
    public int RapidFireCasts { get; init; }
    public int SerpentStingApplications { get; init; }
    public Dictionary<string, decimal> DamageByAbility { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
