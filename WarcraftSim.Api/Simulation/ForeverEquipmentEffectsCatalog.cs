using System.Text.Json;
using WarcraftSim.Core.Characters.Equipment;

namespace WarcraftSim.Api.Simulation;

/// <summary>Build-pinned, offline equipment-effect definitions. This augments,
/// never replaces, the CP170/171 shared item catalog and stat resolver.</summary>
public static class ForeverEquipmentEffectsCatalog
{
    private static readonly Lazy<ForeverEquipmentEffectsDocument> Source = new(Load);
    public static ForeverEquipmentEffectsDocument GetCatalog() => Source.Value;

    private static ForeverEquipmentEffectsDocument Load()
    {
        var assembly=typeof(ForeverEquipmentEffectsCatalog).Assembly;
        var resource=assembly.GetManifestResourceNames().Single(x=>
            x.EndsWith("forever-equipment-cp172-effects.json",StringComparison.OrdinalIgnoreCase));
        using var stream=assembly.GetManifestResourceStream(resource)!;
        var result=JsonSerializer.Deserialize<ForeverEquipmentEffectsDocument>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive=true }) ??
            throw new InvalidOperationException("CP172 equipment effect definitions missing.");
        if(string.IsNullOrWhiteSpace(result.CatalogVersion) ||
           result.Enchants.Any(x=>x.SpellId<=0 || string.IsNullOrWhiteSpace(x.SourceUrl) ||
               string.IsNullOrWhiteSpace(x.SourceBuild) || string.IsNullOrWhiteSpace(x.VerificationStatus)) ||
           result.Items.Any(x=>x.Id<=0 || string.IsNullOrWhiteSpace(x.SourceUrl)) ||
           result.Effects.Any(x=>string.IsNullOrWhiteSpace(x.Key) ||
               !x.SourceUrl.StartsWith("https://",StringComparison.OrdinalIgnoreCase) ||
               x.DurationSeconds<0m || x.CooldownSeconds<0m ||
               x.SharedCooldownSeconds<0m || x.ProcsPerMinute<0m ||
               x.RealProcsPerMinute<0m || x.InternalCooldownSeconds<0m ||
               x.MaxCharges<0 || x.ChargeRecoverySeconds<0m || x.MaxStacks<1 ||
               x.ProcChancePercent is <0m or >100m || x.Damage<0m || x.ResourceMinimum<0m || x.ResourceMaximum<x.ResourceMinimum || x.ActivationMissingResourceMinimum<0m ||
               x.HealingMinimum<0m || x.HealingMaximum<x.HealingMinimum ||
               x.StatBonuses.Any(v=>v.Value<0m)) ||
           result.Effects.Select(x=>x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=
               result.Effects.Count)
            throw new InvalidOperationException("CP172 effect source contains invalid IDs, duplicate keys or values.");
        foreach(var effect in result.Effects)
        {
            if((effect.ItemId is null)==(effect.EnchantSpellId is null))
                throw new InvalidOperationException($"Effect {effect.Key} must reference exactly one item or enchant.");
            // Referenced item may be in the CP170/171 baseline; merged catalog validates IDs.
            if(effect.EnchantSpellId.HasValue &&
               result.Enchants.All(x=>x.SpellId!=effect.EnchantSpellId.Value) &&
               effect.EnchantSpellId!=20034)
                throw new InvalidOperationException($"Effect {effect.Key} references an unknown enchant.");
            if(effect.Kind is not ("on-use" or "proc-damage" or "proc-stats") ||
               effect.Trigger is not ("manual" or "melee-hit" or "melee-crit" or
                   "ranged-hit" or "ranged-crit" or "spell-hit" or "spell-crit" or
                   "periodic-damage" or "periodic-crit" or "periodic-healing" or
                   "any-hit" or "critical-hit" or "damage-taken" or "healing" or
                   "resource-gain") ||
               effect.ProcRateMode is not ("" or "chance" or "ppm" or "rppm") ||
               effect.Kind.StartsWith("proc",StringComparison.Ordinal) &&
                   effect.ProcChancePercent==0m && effect.ProcsPerMinute<=0m &&
                   effect.RealProcsPerMinute<=0m ||
               effect.ProcRateMode=="rppm" && effect.RealProcsPerMinute<=0m ||
               effect.ProcRateMode=="ppm" && effect.ProcsPerMinute<=0m ||
               effect.ProcRateMode=="chance" && effect.ProcChancePercent<=0m ||
               effect.Kind=="on-use" && effect.CooldownSeconds<=0m ||
               effect.ChargeRecoverySeconds>0m && effect.MaxCharges<=0)
                throw new InvalidOperationException($"Effect {effect.Key} has no supported trigger policy.");
        }
        return result;
    }

    public static IReadOnlyList<ForeverExecutableEquipmentEffect> Select(
        CharacterEquipmentLoadout loadout)
    {
        ArgumentNullException.ThrowIfNull(loadout);
        ArgumentNullException.ThrowIfNull(loadout.Items);
        var catalog=GetCatalog();
        var effects=new List<ForeverExecutableEquipmentEffect>();
        foreach(var item in loadout.Items)
        {
            if(item.ItemId is int itemId)
                effects.AddRange(catalog.Effects.Where(x=>x.Modeled && x.ItemId==itemId));
            if(item.EnchantSpellId is int enchantId)
                effects.AddRange(catalog.Effects.Where(x=>x.Modeled && x.EnchantSpellId==enchantId));
            if(item.TemporaryEnchantSpellId is int tempId)
                effects.AddRange(catalog.Effects.Where(x=>x.Modeled && x.EnchantSpellId==tempId));
        }
        return effects;
    }
}

public sealed class ForeverEquipmentEffectsDocument
{
    public string CatalogVersion { get; set; }="";
    public string SourceBuild { get; set; }="";
    public string Source { get; set; }="";
    public List<ForeverEquipmentEnchant> Enchants { get; set; }=[];
    public List<ForeverEquipmentItem> Items { get; set; }=[];
    public List<ForeverExecutableEquipmentEffect> Effects { get; set; }=[];
}

public sealed class ForeverExecutableEquipmentEffect
{
    public string Key { get; set; }="";
    public int? ItemId { get; set; }
    public int? SpellId { get; set; }
    public int? EnchantSpellId { get; set; }
    public string Kind { get; set; }="";
    public string Trigger { get; set; }="";
    public bool Modeled { get; set; }
    public bool Provisional { get; set; }
    public string SourceBuild { get; set; }="";
    public string SourceUrl { get; set; }="";
    public string Description { get; set; }="";
    public Dictionary<string,decimal> StatBonuses { get; set; }=new(StringComparer.OrdinalIgnoreCase);
    public decimal DurationSeconds { get; set; }
    public decimal CooldownSeconds { get; set; }
    public string SharedCooldownGroup { get; set; }="";
    public decimal SharedCooldownSeconds { get; set; }
    // Default (empty) preserves legacy CP172 chance/PPM selection. RPPM is
    // time-since-last-eligible-event without an invented server BLP multiplier.
    public string ProcRateMode { get; set; }="";
    public decimal ProcsPerMinute { get; set; }
    public decimal RealProcsPerMinute { get; set; }
    public decimal ProcChancePercent { get; set; }
    public decimal InternalCooldownSeconds { get; set; }
    // Zero means unlimited. Positive charges refill singly when recovery > 0.
    public int MaxCharges { get; set; }
    public decimal ChargeRecoverySeconds { get; set; }
    public int MaxStacks { get; set; }=1;
    public decimal Damage { get; set; }
    public string ResourceKey { get; set; }="";
    public decimal ResourceMinimum { get; set; }
    public decimal ResourceMaximum { get; set; }
    public decimal ActivationMissingResourceMinimum { get; set; }
    public string School { get; set; }="physical";
    public decimal HealingMinimum { get; set; }
    public decimal HealingMaximum { get; set; }
    public bool AutoUse { get; set; }
}
