using System.Text.Json;
using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Stats;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Class-wide adapters prepare one isolated raw character through the existing
/// catalog/modifier pipeline. Derived stats and talent multipliers remain the
/// runner's responsibility and must run once after this preparation.
/// </summary>
public static class ForeverSharedCharacterInputs
{
    public static ForeverSharedCharacterInputPreparation Prepare(CharacterProfile source,
        ForeverLevel60BaselineObservation? baseline,ForeverExternalModifierSelection modifiers,
        decimal duration,IReadOnlyCollection<string>? supportedTargetDebuffs=null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(modifiers);
        if(source.Level!=60 || string.IsNullOrWhiteSpace(source.ClassKey) ||
           duration is <=0m or >1200m)
            throw new ArgumentException("Shared character inputs require a level-60 class and bounded encounter duration.");
        ValidateStatSources(source);
        var profile=baseline is null
            ? JsonSerializer.Deserialize<CharacterProfile>(JsonSerializer.Serialize(source))!
            : CasterSimulationRequestSnapshot.GenerateBaseline(source,baseline);
        if(baseline is null)CasterSimulationRequestSnapshot.PreserveStatComparers(source,profile);

        var selected=ForeverExternalModifierCatalog.Resolve(modifiers,profile.Level,duration);
        // Only the server-owned adapter supplies this allowlist. Admission
        // requires a tested live mitigation consumer and an effect processor;
        // target stats are never folded into the character's self stats.
        if(selected.Any(x=>x.Category=="target-debuff" &&
            (supportedTargetDebuffs?.Contains(x.Key,StringComparer.OrdinalIgnoreCase)!=true ||
             x.ImplementationStatus!="fully-implemented")))
            throw new ArgumentException("Selected target debuff has no complete live target mitigation adapter in this workflow.");
        if(selected.Any(x=>!x.Modeled || x.Stats.Count==0))
            throw new ArgumentException("Shared character preparation requires modeled static self modifiers.");
        foreach(var buff in profile.Buffs.Selections.Where(x=>x is not null && x.IsEnabled))
        {
            var key=(buff.BuffKey??"").Trim();
            if(key.StartsWith("cp173-",StringComparison.OrdinalIgnoreCase))key=key[6..];
            var known=ForeverExternalModifierCatalog.Entries.FirstOrDefault(x=>
                x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
            if(known is not null && selected.Any(x=>x.Key==known.Key ||
                !string.IsNullOrEmpty(known.ExclusiveGroup) && x.ExclusiveGroup==known.ExclusiveGroup))
                throw new ArgumentException($"Modifier '{known.Key}' already contributes or conflicts with a selected modifier group.");
        }
        profile.Buffs.Selections.RemoveAll(x=>x is not null && !x.IsEnabled && selected.Any(m=>
            string.Equals((x.BuffKey??"").Trim(),"cp173-"+m.Key,StringComparison.OrdinalIgnoreCase)));
        var withModifiers=ForeverExternalModifierCatalog.ApplyToProfile(profile,modifiers,duration);
        CasterSimulationRequestSnapshot.PreserveStatComparers(profile,withModifiers);
        profile=withModifiers;

        var notes=new List<string>();
        var eligibility=new List<ForeverEquipmentEligibilityAssessment>();
        var weapons=new Dictionary<string,ForeverResolvedWeapon>(StringComparer.OrdinalIgnoreCase);
        var catalogSelected=profile.Equipment.Items.Any(x=>x.ItemId.HasValue);
        if(catalogSelected)
        {
            if(profile.Equipment.Items.Any(x=>!x.ItemId.HasValue))
                throw new ArgumentException("Catalog equipment cannot be mixed with authored stat-budget equipment.");
            foreach(var entry in profile.Equipment.Items)
            {
                var item=ForeverEquipmentCatalog.FindItem(entry.ItemId!.Value);
                var assessment=ForeverEquipmentEligibility.Assess(item,profile.ClassKey);
                eligibility.Add(assessment);
                if(!assessment.CanExecute)
                    throw new ArgumentException($"Item {item.Id} is not executable in shared preparation: {string.Join(" ",assessment.Reasons)}");
                foreach(var id in new[]{entry.EnchantSpellId,entry.TemporaryEnchantSpellId})
                {
                    if(id is not int spellId)continue;
                    var enchant=ForeverEquipmentCatalog.FindEnchant(spellId);
                    if(!enchant.Modeled || string.IsNullOrWhiteSpace(enchant.SourceBuild) ||
                       !Uri.TryCreate(enchant.SourceUrl,UriKind.Absolute,out var uri) || uri.Scheme!=Uri.UriSchemeHttps ||
                       enchant.SourceStatus.Contains("classic",StringComparison.OrdinalIgnoreCase) ||
                       enchant.IsTemporary && enchant.DurationSeconds<duration)
                        throw new ArgumentException($"Enchant {spellId} lacks sourced full-duration executable data.");
                    var effects=ForeverEquipmentEffectsCatalog.GetCatalog().Effects.Where(x=>x.EnchantSpellId==spellId).ToArray();
                    if(effects.Any(x=>!x.Modeled) || enchant.Stats.Count==0 &&
                       enchant.WeaponDamageBonus==0m && effects.Length==0)
                        throw new ArgumentException($"Enchant {spellId} has an unimplemented effect.");
                }
            }
            var sets=profile.Equipment.Items.Select(x=>ForeverEquipmentCatalog.FindItem(x.ItemId!.Value))
                .Where(x=>!string.IsNullOrWhiteSpace(x.SetKey)).GroupBy(x=>x.SetKey)
                .ToDictionary(x=>x.Key!,x=>x.Count(),StringComparer.OrdinalIgnoreCase);
            if(ForeverEquipmentCatalog.GetCatalog().Sets.Any(x=>!x.Modeled &&
                sets.GetValueOrDefault(x.Key)>=x.RequiredPieces))
                throw new ArgumentException("An equipped set bonus is unimplemented; shared execution cannot silently omit it.");
            // Resolve once. The resolver's equipment-only derived totals are
            // never added; the normalized raw contributions enter the runner's
            // complete character pipeline once, including its talent modifiers.
            var resolved=ForeverSharedEquipmentResolver.Resolve(profile.Equipment,
                profile.ClassKey,profile.Level,profile.Race);
            profile.Equipment=resolved.Loadout;
            foreach(var pair in resolved.Weapons)weapons.Add(pair.Key,pair.Value);
            foreach(var entry in eligibility)
            {
                notes.Add($"Shared catalog item {entry.ItemId}: restriction {entry.RestrictionStatus}; proficiency {entry.ProficiencyStatus}; effects {entry.EffectStatus}; value source {entry.ValueSourceBuild} {entry.ValueSourceUrl}; restriction source {entry.RestrictionSourceKind} {entry.RestrictionSourceBuild} {entry.RestrictionSourceUrl} SHA-256 {entry.RestrictionSourceSha256} ({entry.RestrictionConfidence}).");
                if(entry.ProficiencySourceUrl is not null)
                    notes.Add($"Item {entry.ItemId} independent proficiency evidence: {entry.ProficiencySourceBuild}; {entry.ProficiencySourceUrl}; {entry.ProficiencyConfidence}. Item values and current acquisition availability are separate questions.");
            }
        }
        else
        {
            if(profile.Equipment.Items.Any(x=>x.EnchantSpellId.HasValue || x.TemporaryEnchantSpellId.HasValue))
                throw new ArgumentException("Catalog enchants require a catalog item ID.");
            // The same slot and contributor validation also governs manual
            // budgets; it does not turn authored values into catalog evidence.
            profile.Equipment=ForeverSharedEquipmentResolver.Resolve(profile.Equipment,
                profile.ClassKey,profile.Level,profile.Race).Loadout;
            notes.Add("Authored equipment budgets remain manual inputs, distinct from reviewed catalog values and weapon assumptions.");
        }
        foreach(var effect in selected)
            notes.Add(effect.Category=="target-debuff"
                ? $"Shared target modifier {effect.Key}: {modifiers.TargetDebuffMode}; {effect.DurationSeconds}s natural lifetime; applied by the encounter's external target aura processor, not character stats; {effect.ImplementationStatus}; {effect.DataConfidence}; {effect.SourceBuild}; {effect.SourceUrl}."
                : $"Shared modifier {effect.Key}: {effect.ImplementationStatus}; {effect.DataConfidence}; {effect.SourceBuild}; {effect.SourceUrl}.");
        NormalizeManaRegen(profile.BaseStats);
        foreach(var item in profile.Equipment.Items)
        {
            NormalizeManaRegen(item.Stats);
            foreach(var modifier in item.Modifiers)NormalizeManaRegen(modifier.Stats);
        }
        foreach(var talent in profile.Talents.Selections)NormalizeManaRegen(talent.Stats);
        foreach(var buff in profile.Buffs.Selections)NormalizeManaRegen(buff.Stats);
        notes.Add("Raw equipment, enchant, set and selected self-modifier contributions enter the character pipeline exactly once before class talent/derived stats. Dynamic effects require the shared per-run equipment processor and live stat consumers; no effect is included in precombat stats twice.");
        return new(profile,weapons,ForeverEquipmentEffectsCatalog.Select(profile.Equipment),notes,eligibility);
    }

    private static void ValidateStatSources(CharacterProfile profile)
    {
        static bool Valid(StatCollection? stats)=>stats?.Values is not null;
        if(!Valid(profile.BaseStats) || profile.Equipment?.Items is null ||
           profile.Equipment.Items.Any(x=>x is null || !Valid(x.Stats) || x.Modifiers is null ||
               x.Modifiers.Any(m=>m is null || !Valid(m.Stats))) ||
           profile.Buffs?.Selections is null || profile.Buffs.Selections.Any(x=>x is null || !Valid(x.Stats)) ||
           profile.Talents?.Selections is null || profile.Talents.Selections.Any(x=>x is null || !Valid(x.Stats)))
            throw new ArgumentException("Shared character stat sources cannot be null.");
    }

    private static void NormalizeManaRegen(StatCollection stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var aliases=stats.Values.Where(x=>x.Key.Trim().Equals("mana-per-5",StringComparison.OrdinalIgnoreCase) ||
            x.Key.Trim().Equals("mp5",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(aliases.Length==0)return;
        if(aliases.GroupBy(x=>x.Key.Trim(),StringComparer.OrdinalIgnoreCase).Any(x=>x.Count()>1) ||
           aliases.Any(x=>x.Value!=aliases[0].Value))
            throw new ArgumentException("Conflicting or duplicate canonical Mana regeneration stat key.");
        foreach(var entry in aliases)stats.Values.Remove(entry.Key);
        stats.Set("mp5",aliases[0].Value);
    }
}

public sealed record ForeverSharedCharacterInputPreparation(CharacterProfile Character,
    IReadOnlyDictionary<string,ForeverResolvedWeapon> Weapons,
    IReadOnlyList<ForeverExecutableEquipmentEffect> Effects,IReadOnlyList<string> Notes,
    IReadOnlyList<ForeverEquipmentEligibilityAssessment> Eligibility);
