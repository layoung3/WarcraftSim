using WarcraftSim.Data.Forever.Items;

namespace WarcraftSim.Api.Simulation;

/// <summary>Opt-in, limited-inventory consumables used by any compatible solo runner.
/// A zero charge count always disables the item. No automatic pre-pull conjuring.
/// </summary>
public sealed class CombatUseItemsOptions
{
    /// <summary>Default CP175 policy will never spend a recovery item unless its
    /// maximum possible restore fits the deficit. Legacy policy is explicit.</summary>
    public string RestorationPolicy { get; set; } = "conservative-no-overcap";
    // An item may be in the inventory yet explicitly disabled in the builder.
    // A disabled item is never registered with the combat executor.
    public List<string> DisabledItemKeys { get; set; } = [];
    public bool AllowEmergencyHealingOvercap { get; set; }
    public int ManaRubyCharges { get; set; }
    public int ManaCitrineCharges { get; set; }
    public int ManaJadeCharges { get; set; }
    public int ManaAgateCharges { get; set; }
    public decimal ManaRubyBelowManaPercent { get; set; } = 65m;
    public int MajorManaPotionCharges { get; set; }
    public decimal MajorManaPotionBelowManaPercent { get; set; } = 55m;
    public int MinorHealthstoneCharges { get; set; }
    public int LesserHealthstoneCharges { get; set; }
    public int HealthstoneCharges { get; set; }
    public int GreaterHealthstoneCharges { get; set; }
    public int MajorHealthstoneCharges { get; set; }
    public decimal HealthstoneBelowHealthPercent { get; set; } = 75m;
    public int DarkRuneCharges { get; set; }
    public int DemonicRuneCharges { get; set; }
    public decimal RuneBelowManaPercent { get; set; } = 40m;
    public decimal MinimumHealthToUseRune { get; set; } = 1200m;
    public int MightyRagePotionCharges { get; set; }
    public int MajorHealingPotionCharges { get; set; }
    public decimal MajorHealingPotionBelowHealthPercent { get; set; } = 55m;
    public decimal MightyRageBelowRage { get; set; } = 35m;
    public int EngineeringSkill { get; set; }
    public int DenseDynamiteCharges { get; set; }
    public int GoblinSapperCharges { get; set; }
    public decimal FirstExplosiveUseSeconds { get; set; } = 0m;
    public decimal MinimumHealthToUseSapper { get; set; } = 1000m;

    public void Validate(string classKey)
    {
        var knownItems = ForeverCombatItemCatalog.CreateAll()
            .Select(item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (DisabledItemKeys is null ||
            DisabledItemKeys.Any(key => string.IsNullOrWhiteSpace(key) || !knownItems.Contains(key)) ||
            DisabledItemKeys.Count != DisabledItemKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new ArgumentException("Disabled combat items must be distinct, supported item keys.");
        if (RestorationPolicy is not ("conservative-no-overcap" or "legacy-half-mean"))
            throw new ArgumentException("RestorationPolicy must be conservative-no-overcap or legacy-half-mean.");
        if (new[] { ManaRubyCharges, ManaCitrineCharges, ManaJadeCharges, ManaAgateCharges, MajorManaPotionCharges,
                MinorHealthstoneCharges, LesserHealthstoneCharges, HealthstoneCharges,
                GreaterHealthstoneCharges, MajorHealthstoneCharges,
                DarkRuneCharges, DemonicRuneCharges,
                MightyRagePotionCharges,MajorHealingPotionCharges, DenseDynamiteCharges, GoblinSapperCharges }
            .Any(x => x is < 0 or > 20) || EngineeringSkill is < 0 or > 300 ||
            ManaRubyBelowManaPercent is < 0m or > 100m ||
            MajorManaPotionBelowManaPercent is < 0m or > 100m ||
            HealthstoneBelowHealthPercent is < 0m or > 100m ||
            RuneBelowManaPercent is < 0m or > 100m ||
            MinimumHealthToUseRune < 0m ||
            MightyRageBelowRage is < 0m or > 100m ||
            MajorHealingPotionBelowHealthPercent is < 0m or > 100m ||
            FirstExplosiveUseSeconds < 0m || MinimumHealthToUseSapper < 0m)
            throw new ArgumentException("Combat item charges, thresholds or skill are invalid.");
        // Each named conjured gem is unique; represent its one-use inventory,
        // not an infinitely refillable charge stack.
        if(new[]{ManaRubyCharges,ManaCitrineCharges,ManaJadeCharges,ManaAgateCharges}.Any(x=>x>1))
            throw new ArgumentException("Each Mana gem type has at most one available use.");
        if(new[]{MinorHealthstoneCharges,LesserHealthstoneCharges,HealthstoneCharges,
                GreaterHealthstoneCharges,MajorHealthstoneCharges}.Any(x=>x>1))
            throw new ArgumentException("Each prepared Healthstone type has at most one use.");
        if ((ManaRubyCharges > 0 || ManaCitrineCharges > 0 || ManaJadeCharges > 0 || ManaAgateCharges > 0) && classKey != "mage")
            throw new ArgumentException("Only Mages may use conjured mana gems.");
        if (MightyRagePotionCharges > 0 && classKey is not ("warrior" or "druid"))
            throw new ArgumentException("Mighty Rage Potion requires a Warrior or Druid.");
        if (DenseDynamiteCharges > 0 && EngineeringSkill < 250)
            throw new ArgumentException("Dense Dynamite requires Engineering 250.");
        if (GoblinSapperCharges > 0 && EngineeringSkill < 205)
            throw new ArgumentException("Goblin Sapper Charge requires Engineering 205.");
    }
}
