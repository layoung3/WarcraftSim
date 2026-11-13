namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Admission evidence is separate from the item and talent allocation. A pending
/// record remains available for editing/review; it is not a permanent class ban.
/// Existing legacy adapters retain their previous catalog boundary.
/// </summary>
public static class ForeverEquipmentEligibility
{
    public static ForeverEquipmentEligibilityAssessment Assess(
        ForeverEquipmentItem item,string classKey)
    {
        ArgumentNullException.ThrowIfNull(item);
        if(string.IsNullOrWhiteSpace(classKey))throw new ArgumentException("Equipment needs a class.");
        classKey=classKey.Trim().ToLowerInvariant();
        var reasons=new List<string>();
        var evidence=item.ClassRestrictionEvidence;
        var restriction=evidence?.IsConsistentWith(item)==true
            ? evidence.SourceAllowedClasses.Contains(classKey,StringComparer.OrdinalIgnoreCase)?"proven":"denied"
            : "pending";
        if(restriction=="pending")
            reasons.Add("Class eligibility is pending consistent pinned restriction evidence; a historical class list or an omitted class field is not unrestricted eligibility.");
        else if(restriction=="denied")
            reasons.Add("The reviewed item restriction excludes this class.");

        var classified=item.AllowedSlots is {Count:>0} && (item.ItemClass switch {
            "armor" => item.ItemSubclass is "cloth" or "leather" or "mail" or "plate" or "shield" or "miscellaneous",
            "weapon" => item.Weapon is { } weapon && item.ItemSubclass==weapon.Type &&
                weapon.Type is "axe" or "mace" or "sword" or "dagger" or "fist" or "staff" or
                    "polearm" or "bow" or "gun" or "crossbow" or "wand" or "thrown" &&
                weapon.Hand is "one-hand" or "two-hand" or "ranged" && weapon.SpeedSeconds>0m &&
                weapon.MinimumDamage>=0m && weapon.MaximumDamage>=weapon.MinimumDamage,
            _ => false
        });
        var knownClass=classKey is "warrior" or "paladin" or "hunter" or "rogue" or
            "priest" or "shaman" or "mage" or "warlock" or "druid";
        var proficiency=!classified || !knownClass || item.ItemClass=="weapon" ? "pending" :
            !ForeverSharedEquipmentResolver.CanEquipArmorClass(classKey,item.ItemSubclass)?"denied":"proven";
        if(evidence?.SourceKind==ForeverPaladinEquipmentCatalog.EvidenceKind)
            proficiency=ForeverPaladinEquipmentCatalog.HasReviewedProficiency(item,classKey)?"proven":"pending";
        if(proficiency=="pending")
            reasons.Add(!classified || !knownClass
                ? "Equipment classification or the selected class is unresolved; proficiency is pending."
                : evidence?.SourceKind==ForeverPaladinEquipmentCatalog.EvidenceKind && classKey!="paladin"
                ? "This small published-item review establishes Paladin proficiency only; other class admission remains pending evidence."
                : "Weapon proficiency requires separate reviewed evidence; the item restriction mask alone does not establish it.");
        else if(proficiency=="denied")reasons.Add("This class cannot equip this armor category.");

        var known=ForeverEquipmentEffectsCatalog.GetCatalog().Effects.Where(x=>x.ItemId==item.Id).ToArray();
        var linked=item.Effects.All(reference=>reference.ExecutionStatus=="modeled" &&
            known.Count(effect=>effect.Modeled && effect.Kind==reference.Kind &&
                (!reference.SpellId.HasValue || effect.SpellId==reference.SpellId))==1);
        var effects=linked && known.All(x=>x.Modeled)?"supported":
            known.Any(x=>x.Modeled)?"partial":"unimplemented";
        if(effects!="supported")
            reasons.Add("One or more equipment effects do not have an executable shared implementation.");
        var sourced=(item.SourceStatus is "forever-client-verified-tooltip" or
            "forever-client-db2-reviewed" or "reviewed-published-forever-tooltip" or "reviewed-published-forever-client-record") &&
            (item.VerificationStatus is "client-tooltip" or "client-db2-reviewed" or "published-client-tooltip" or "published-client-record") &&
            !string.IsNullOrWhiteSpace(item.SourceBuild) &&
            Uri.TryCreate(item.SourceUrl,UriKind.Absolute,out var uri) && uri.Scheme==Uri.UriSchemeHttps;
        if(!sourced)reasons.Add("The catalog value source has not passed the existing prepared-item evidence gate.");
        return new(item.Id,classKey,restriction,proficiency,effects,
            restriction=="proven" && proficiency=="proven" && effects=="supported" && sourced,
            item.SourceBuild,item.SourceUrl,evidence?.SourceBuild,evidence?.SourceUrl,
            evidence?.SourceSha256,evidence?.SourceKind,evidence?.RawMask,
            evidence?.DataConfidence??"unknown",reasons.ToArray()) {
                ProficiencySourceUrl=evidence?.SourceKind==ForeverPaladinEquipmentCatalog.EvidenceKind?
                    item.ItemSubclass=="shield"?ForeverPaladinEquipmentCatalog.ShieldProficiencyUrl:
                        ForeverPaladinEquipmentCatalog.ProficiencyUrl:null,
                ProficiencySourceBuild=evidence?.SourceKind==ForeverPaladinEquipmentCatalog.EvidenceKind?
                    item.ItemSubclass=="shield"?ForeverPaladinEquipmentCatalog.ShieldProficiencyBuild:
                        ForeverPaladinEquipmentCatalog.ProficiencyBuild:null,
                ProficiencyConfidence=evidence?.SourceKind==ForeverPaladinEquipmentCatalog.EvidenceKind?
                    item.ItemSubclass=="shield"?ForeverPaladinEquipmentCatalog.ShieldProficiencyConfidence:
                        "published-client-extraction-provisional; independent from item70888 values":null
            };
    }
}

public sealed record ForeverEquipmentEligibilityAssessment(
    int ItemId,string ClassKey,string RestrictionStatus,string ProficiencyStatus,
    string EffectStatus,bool CanExecute,string ValueSourceBuild,string ValueSourceUrl,
    string? RestrictionSourceBuild,string? RestrictionSourceUrl,string? RestrictionSourceSha256,string? RestrictionSourceKind,
    long? RawClassMask,string RestrictionConfidence,IReadOnlyList<string> Reasons)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ProficiencySourceUrl {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ProficiencySourceBuild {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ProficiencyConfidence {get;init;}
}
