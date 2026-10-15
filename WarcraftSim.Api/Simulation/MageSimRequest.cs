using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Mage;

namespace WarcraftSim.Api.Simulation;

public sealed class MageSimRequest
{
    public int Seed { get; set; } = 132001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public string Specialization { get; set; } = "arcane";
    public decimal TargetMaximumHealth { get; set; } = 200000m;
    public decimal MageMaximumHealth { get; set; } = 3000m;
    public decimal StartingManaPercent { get; set; } = 100m;
    public decimal BaseSpellHitPercent { get; set; } = 83m;
    public decimal SpellCritPercent { get; set; } = 12m;
    public bool UseFrostfireBolt { get; set; }
    public bool UseIgnite { get; set; } = true;
    public bool UseHeatingUp { get; set; } = true;
    public bool UseMissileBarrage { get; set; } = true;
    public bool UseFingersOfFrost { get; set; } = true;
    public bool UseWintersChill { get; set; } = true;
    public int ArcaneBlastsBeforeRelease { get; set; } = 3;
    public ForeverMageTalentBuild Talents { get; set; } = ForeverMageTalentBuild.Default("arcane");
    public ForeverMageTuning Tuning { get; set; } = new();
    public CharacterProfile Character { get; set; } = new()
    {
        Name = "Illustrative Mage", RulesetKey = "wow-forever", ClassKey = "mage",
        SpecializationKey = "arcane", Level = 60,
        BaseStats = new StatCollection { Values = new Dictionary<string, decimal>
        { ["intellect"] = 200m, ["spirit"] = 180m,
          ["spell-power"] = 200m, ["mp5"] = 20m } }
    };
    public static MageSimRequest CreatePreset(string spec)
    {
        var r = new MageSimRequest {
            Specialization = spec, Talents = ForeverMageTalentBuild.Default(spec),
            Seed = spec switch { "arcane" => 132001, "fire" => 132002, "frost" => 132003, _ => 132004 }
        };
        r.Character.Name = $"Illustrative {spec} Mage";
        r.Character.SpecializationKey = spec;
        return r;
    }
}
public sealed class MageSimResponse
{
    public string Specialization { get; init; } = "";
    public decimal Dps { get; init; }
    public decimal DamageDone { get; init; }
    public decimal EndingMana { get; init; }
    public decimal MaximumMana { get; init; }
    public decimal SpellPower { get; init; }
    public int MissileBarrageProcs { get; init; }
    public int ArcaneBlastCasts { get; init; }
    public int HeatingUpPyroblasts { get; init; }
    public int FingersOfFrostProcs { get; init; }
    public int FingersOfFrostIceLances { get; init; }
    public decimal IgniteDamage { get; init; }
    public Dictionary<string, decimal> DamageByAbility { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
