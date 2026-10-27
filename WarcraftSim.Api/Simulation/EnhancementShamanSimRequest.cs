using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Simulation.Engine;
using WarcraftSim.Core.Stats;
using WarcraftSim.Data.Forever.Shaman;

namespace WarcraftSim.Api.Simulation;

public sealed class EnhancementShamanSimRequest
{
    public int Seed { get; set; } = 130001;
    public decimal DurationSeconds { get; set; } = 120m;
    public bool CaptureTimeline { get; set; } = true;
    public decimal TargetMaximumHealth { get; set; } = 200000m;
    public decimal MaximumHealth { get; set; } = 3500m;
    public decimal TargetArmor { get; set; } = 3500m;
    public decimal WeaponMinDamage { get; set; } = 250m;
    public decimal WeaponMaxDamage { get; set; } = 390m;
    public decimal WeaponSpeedSeconds { get; set; } = 3.6m;
    // Opt-in real dual-wield streams. Existing two-handed requests retain their
    // historical behavior; off-hand starts only with explicit configuration.
    public bool DualWield { get; set; }
    public decimal OffHandWeaponMinDamage { get; set; } = 130m;
    public decimal OffHandWeaponMaxDamage { get; set; } = 200m;
    public decimal OffHandWeaponSpeedSeconds { get; set; } = 2.6m;
    public decimal BaseMeleeHitPercent { get; set; } = 87m;
    public decimal BaseSpellHitPercent { get; set; } = 83m;
    public decimal CriticalChancePercent { get; set; } = 15m;
    public decimal ProvisionalGlancingChancePercent { get; set; } = 24m;
    public CombatUseItemsOptions CombatItems { get; set; } = new();
    public bool UseWindfuryWeapon { get; set; } = true;
    public bool UseWindfuryTotem { get; set; } = true;
    public bool UseStormstrike { get; set; } = true;
    public bool UseEarthShock { get; set; } = true;
    public bool UseMaelstromLightningBolt { get; set; } = true;
    public bool UseLightningBoltFiller { get; set; }
    public ForeverEnhancementShamanTalents Talents { get; set; } = new();
    public ForeverEnhancementShamanTuning Tuning { get; set; } = new();
    public CharacterProfile Character { get; set; } = new()
    {
        Name = "Illustrative Enhancement Shaman", ClassKey = "shaman",
        SpecializationKey = "enhancement", Level = 60,
        RulesetKey = "wow-forever",
        BaseStats = new StatCollection { Values = new Dictionary<string, decimal>
        {
            ["strength"] = 155m, ["agility"] = 120m, ["intellect"] = 155m,
            ["attack-power"] = 1000m, ["spell-power"] = 100m,
            ["mp5"] = 15m
        } }
    };
}

public sealed class EnhancementShamanSimResponse
{
    public decimal Dps { get; init; }
    public decimal DamageDone { get; init; }
    public decimal EndingMana { get; init; }
    public decimal MaximumMana { get; init; }
    public decimal AttackPower { get; init; }
    public decimal SpellPower { get; init; }
    public int WindfuryWeaponProcs { get; init; }
    public int WindfuryWeaponExtraAttacks { get; init; }
    public bool DualWieldActive { get; init; }
    public decimal OffHandAutoDamage { get; init; }
    public int WindfuryTotemProcs { get; init; }
    public Dictionary<string, int> CombatItemUses { get; init; } = new();
    public int MaelstromInstantBolts { get; init; }
    public int MaxMaelstromStacks { get; init; }
    public Dictionary<string, decimal> DamageByAbility { get; init; } = new();
    public SimulationRunResult Run { get; init; } = new();
    public List<string> ModelingNotes { get; init; } = [];
}
