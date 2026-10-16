using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Warlock;

namespace WarcraftSim.Api.Simulation;

public sealed class WarlockSimRequest
{
    public int Seed { get; set; } = 131001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public string Specialization { get; set; } = "affliction";
    public decimal TargetMaximumHealth { get; set; } = 200000m;
    public decimal WarlockMaximumHealth { get; set; } = 3500m;
    public decimal BaseSpellHitPercent { get; set; } = 83m;
    public decimal SpellCritPercent { get; set; } = 10m;
    public decimal PetMeleeHitPercent { get; set; } = 85m;
    public bool EnablePet { get; set; } = true;
    // Pet type controls the applicable Master Demonologist school bonus.
    // The existing pet attack stream is still provisional generic melee.
    public string PetType { get; set; } = "succubus";
    public bool UseLifeTap { get; set; } = true;
    // Real combat-use inventory for the solo Warlock simulation.
    public CombatUseItemsOptions CombatItems { get; set; } = new();
    // SOLO ONLY: the healer's HP replacement is abstracted away; Life Tap
    // still takes a GCD and restores its real modeled Mana amount.
    // Set false to test actual health-limited Life Tap or later raid scenarios.
    public bool AssumeExternalHealingForLifeTap { get; set; } = true;
    public bool UseCorruption { get; set; } = true;
    public bool UseImmolate { get; set; } = true;
    public bool UseBaneOfAgony { get; set; } = true;
    public bool UseWrack { get; set; } = true;
    public bool UseConflagrate { get; set; } = true;
    public bool UseIncinerate { get; set; } = true;
    // Adaptive decisions use spell availability, remaining fight time and Mana.
    public string RotationPolicy { get; set; } = "adaptive"; // adaptive | legacy
    public List<string> CustomPriority { get; set; } = [];
    public decimal LifeTapBelowManaPercent { get; set; } = 30m;
    public decimal LifeTapAboveHealthPercent { get; set; } = 45m;
    public decimal StartingManaPercent { get; set; } = 100m;
    public ForeverWarlockTalentBuild Talents { get; set; } = ForeverWarlockTalentBuild.Default("affliction");
    public ForeverWarlockTuning Tuning { get; set; } = new();
    public CharacterProfile Character { get; set; } = new()
    {
        Name = "Illustrative Warlock", ClassKey = "warlock", SpecializationKey = "affliction",
        RulesetKey = "wow-forever", Level = 60,
        BaseStats = new StatCollection { Values = new Dictionary<string, decimal>
        {
            ["intellect"] = 190m, ["spirit"] = 180m, ["spell-power"] = 200m, ["mp5"] = 20m
        } }
    };

    public static WarlockSimRequest CreatePreset(string spec)
    {
        var request = new WarlockSimRequest
        {
            Specialization = spec, Talents = ForeverWarlockTalentBuild.Default(spec),
            Seed = spec switch { "affliction" => 131001, "demonology" => 131002,
                "destruction" => 131003, _ => 131004 },
            UseImmolate = spec is not "affliction", UseBaneOfAgony = spec is "affliction",
            UseWrack = spec is "affliction", UseConflagrate = spec is "destruction",
            UseIncinerate = spec is "destruction"
        };
        request.Character.Name = $"Illustrative {spec} Warlock";
        request.Character.SpecializationKey = spec;
        return request;
    }
}

public sealed class WarlockSimResponse
{
    public string Specialization { get; init; } = "";
    public decimal Dps { get; init; }
    public decimal DamageDone { get; init; }
    public string ResolvedRotation { get; init; } = "";
    public decimal PetDamage { get; init; }
    public decimal EndingMana { get; init; }
    public decimal MaximumMana { get; init; }
    public decimal EndingHealth { get; init; }
    public decimal SpellPower { get; init; }
    public int LifeTapCasts { get; init; }
    public Dictionary<string, int> CombatItemUses { get; init; } = new();
    public Dictionary<string, decimal> DamageByAbility { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
