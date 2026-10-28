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
    // Explicit execute-fixture health. Normal simulations continue at 100%.
    public decimal TargetInitialHealthPercent { get; set; } = 100m;
    public int AdditionalTargets { get; set; } // explicit AoE target set, no geometry
    public decimal WarlockMaximumHealth { get; set; } = 3500m;
    public decimal? IncomingDamageAtSeconds { get; set; }
    public decimal IncomingDamageAmount { get; set; }
    public string IncomingDamageSchool { get; set; } = "fire";
    public decimal BaseSpellHitPercent { get; set; } = 83m;
    public decimal SpellCritPercent { get; set; } = 10m;
    // Explicit deterministic FINAL melee hit override (legacy). Null inherits
    // the owner's BONUS spell hit at 8/17, on the creature melee miss table.
    public decimal? PetMeleeHitPercent { get; set; }
    // Physical defenses against demon melee. Zero preserves older export
    // baselines; configure encounter-specific values for armored bosses.
    // These are not used for Warlock spells or demon magical attacks.
    public decimal TargetArmor { get; set; } = 0m;
    public decimal TargetDodgePercent { get; set; } = 0m;
    public decimal TargetParryPercent { get; set; } = 0m;
    public bool EnablePet { get; set; } = true;
    // Temporary summons require explicit encounter assumptions.
    public string PetType { get; set; } = "succubus";
    // A demon sacrificed before combat; separate from the active PetType.
    // Null leaves the encounter unchanged. Does not summon another actor.
    public string? PrePullSacrificedDemon { get; set; }
    public bool AllowTemporaryDemon { get; set; }
    public bool AllowDoomguardRitual { get; set; }
    // Enable utility targeting only when these encounter conditions are real.
    public bool DemonTargetIsCasting { get; set; }
    public bool DemonTargetHasMagicAura { get; set; }
    public bool DemonTargetSusceptibleToSeduction { get; set; }
    // Explicit incoming damage to the existing demon actor, not invented AI.
    // Allows survival/Health Funnel DPS comparisons under controlled hits.
    public decimal? DemonIncomingDamageAtSeconds { get; set; }
    public decimal DemonIncomingDamageAmount { get; set; }
    public decimal? DemonSecondIncomingDamageAtSeconds { get; set; }
    public decimal DemonSecondIncomingDamageAmount { get; set; }
    public decimal? DemonDeathAtSeconds { get; set; }
    public decimal? DemonSacrificeAtSeconds { get; set; }
    public decimal? DemonReplacementAtSeconds { get; set; }
    // Explicit opt-in replacement using a real owner summon ability.
    // Legacy scripted replacement remains unchanged until selected.
    public bool ModelReplacementSummon { get; set; }
    public bool UseFelDominationOnReplacement { get; set; }
    // Level-60 base Mana is not independently measured: adjustable input.
    public decimal SummonBaseMana { get; set; } = 1376m;
    public string? ReplacementDemonType { get; set; }
    public ForeverWarlockDemonTuning DemonTuning { get; set; } = new();
    public bool UseLifeTap { get; set; } = true;
    // Real combat-use inventory for the solo Warlock simulation.
    public CombatUseItemsOptions CombatItems { get; set; } = new();
    // SOLO ONLY: the healer's HP replacement is abstracted away; Life Tap
    // still takes a GCD and restores its real modeled Mana amount.
    // Set false to test actual health-limited Life Tap or later raid scenarios.
    public bool AssumeExternalHealingForLifeTap { get; set; } = true;
    public bool UseDeathCoil { get; set; }
    public bool UseBaneOfDoom { get; set; }
    public bool UseCurseOfTheElements { get; set; }
    public bool UseAmplifyCurse { get; set; }
    public bool UseRainOfFire { get; set; }
    public bool UseDrainLife { get; set; } = false;
    public bool UseHealthFunnel { get; set; }
    public decimal HealthFunnelBelowPetHealthPercent { get; set; } = 65m;
    public bool UseDrainSoul { get; set; }
    // Server chance of a shard on a non-lethal tick is unpublished.
    // Explicit encounters can set this probability without inventing a default.
    public decimal DrainSoulShardChancePercent { get; set; }
    public bool UseHellfire { get; set; }
    public bool UseSiphonLife { get; set; } = true;
    public bool UseSearingPain { get; set; } = false;
    public bool UseShadowburn { get; set; } = true;
    public int StartingSoulShards { get; set; } = 3;
    public bool TargetYieldsSoulShard { get; set; }
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
            UseIncinerate = spec is "destruction",
            // Demonology already allocates Sacrifice and Pact; this provides a
            // useful cross-family test without touching other specs' builds.
            PrePullSacrificedDemon = spec=="demonology" ? "imp" : null
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
    public string DemonType { get; init; } = "";
    public string? ActiveDemonAtPull { get; init; }
    public string? PrePullSacrificedDemon { get; init; }
    public string? SacrificeBuffAtPull { get; init; }
    public string? SacrificeBuffAtEnd { get; init; }
    public bool SacrificeCanceledBeforePull { get; init; }
    public bool DemonicPactTalented { get; init; }
    public bool PetSelectedAtPull { get; init; }
    public string? ReplacementDemonType { get; init; }
    public decimal ReplacementDemonDamage { get; init; }
    public decimal WarlockDamage { get; init; }
    public decimal DemonMeleeDamage { get; init; }
    public decimal DemonSpellDamage { get; init; }
    public decimal DemonThreat { get; init; }
    public decimal DemonMana { get; init; }
    public decimal DemonUptimeSeconds { get; init; }
    public bool DemonDied { get; init; }
    public bool DemonSacrificed { get; init; }
    public int DemonSpecialCasts { get; init; }
    public decimal DemonCriticalHits { get; init; }
    public decimal DemonMisses { get; init; }
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
