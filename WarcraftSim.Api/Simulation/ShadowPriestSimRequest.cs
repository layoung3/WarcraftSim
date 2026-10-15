using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Priest;

namespace WarcraftSim.Api.Simulation;

public sealed class ShadowPriestSimRequest
{
    public int Seed { get; set; } = 129001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public decimal TargetMaximumHealth { get; set; } = 200000m;
    public decimal TargetInitialHealthPercent { get; set; } = 100m;
    public decimal PriestMaximumHealth { get; set; } = 3500m;
    public decimal BaseSpellHitPercent { get; set; } = 83m; // provisional vs level 63
    public decimal SpellCritOverridePercent { get; set; } = -1m; // -1 => derived
    public decimal SpellPowerOverride { get; set; } = -1m; // -1 => derived
    public decimal ManaRegenerationWhileCastingFraction { get; set; } = 0m;
    public bool UseShadowWordPain { get; set; } = true;
    public bool UseDevouringPlague { get; set; } = true;
    public bool UseVampiricEmbrace { get; set; } = true;
    public bool UseShadowWordDeathExecute { get; set; } = true;
    public bool UseMindBlast { get; set; } = true;
    public bool UseMindFlay { get; set; } = true;
    public ForeverPriestStatAssumptions StatAssumptions { get; set; } = new();
    public ForeverPriestShadowTalentBuild Talents { get; set; } = new();
    public ForeverPriestShadowTuning Tuning { get; set; } = new();
    public CharacterProfile Character { get; set; } = new()
    {
        Name = "Illustrative level 60 Shadow Priest", RulesetKey = "wow-forever",
        ClassKey = "priest", SpecializationKey = "shadow", Level = 60,
        BaseStats = new StatCollection { Values = new Dictionary<string, decimal>
        {
            ["intellect"] = 190m, ["spirit"] = 210m,
            ["spell-power"] = 150m, ["spell-hit-percent"] = 0m, ["mp5"] = 15m
        } }
    };
}

public sealed class ShadowPriestSimResponse
{
    public decimal Dps { get; init; }
    public decimal DamageDone { get; init; }
    public decimal HealingDone { get; init; }
    public decimal EndingMana { get; init; }
    public decimal MaximumMana { get; init; }
    public decimal SpellPower { get; init; }
    public decimal SpellCritPercent { get; init; }
    public decimal BossEndingHealthPercent { get; init; }
    public int MaximumShadowWeavingStacks { get; init; }
    public Dictionary<string, decimal> DamageByAbility { get; init; } = new();
    public bool PriestDied { get; init; }
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
