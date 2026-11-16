using System.Security.Cryptography;
using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Three exact published client records reviewed for the Paladin witnesses.
/// Explicit positive class bits, item values and independent proficiency
/// evidence are separate gates. This never interprets an omitted class mask.
/// </summary>
public static class ForeverPaladinEquipmentCatalog
{
    public const string EvidenceKind="published-client-reviewed-class-bit";
    public const string SourceBuild="1.60.1.70888";
    public const string CatalogVersion="forever-paladin-equipment-70888-v1";
    public const string InputSha256="7d4533affc0faa1a3ca3a6de5b072f4fa18b1ce011ae2a7979d8f6d814eaf0cd";
    public const string ProficiencyUrl="https://foreverdb.net/de/trainers";
    public const string ProficiencyBuild="1.60.1.70938";
    public const string ProficiencyInputSha256="4659f80cf98f4d0094fc928960676e7af2c9fbd9604656226aa4a58602b4c37f";
    public const string ShieldProficiencyUrl="https://github.com/wowsims/forever/blob/8c7b5a5f5ae85b6222c16d1780c342992cd74f71/sim/core/spelldata/spells_auto_gen.go";
    public const string ShieldProficiencyBuild="pinned extractor 8c7b5a5f5ae85b6222c16d1780c342992cd74f71; separate 1.60.1.70888 Paladin spell reference";
    public const string ShieldProficiencyConfidence="current Paladin equipped-shield prerequisites support a provisional reviewed proficiency inference; shield-learning SkillLineAbility row unavailable";
    public static readonly int[] ReviewedItemIds=[12782,21395,21269];

    // New records must not widen older adapters merely because their raw class
    // mask contains another class. Preserve every pre-existing catalog policy.
    public static void ValidateReviewedExecutionScope(ForeverEquipmentItem item,string classKey)
    {
        if(!ReviewedItemIds.Contains(item.Id))return;
        var eligibility=ForeverEquipmentEligibility.Assess(item,classKey);
        if(!eligibility.CanExecute)
            throw new ArgumentException($"Item {item.Id} execution for {classKey} is pending this separate published-item review: {string.Join(" ",eligibility.Reasons)}");
    }
    private static readonly IReadOnlyDictionary<string,long> ClassBits=new Dictionary<string,long> {
        ["warrior"]=1,["paladin"]=2,["hunter"]=4,["rogue"]=8,["priest"]=16,
        ["shaman"]=64,["mage"]=128,["warlock"]=256,["druid"]=1024};
    private static readonly Lazy<JsonElement> Source=new(Load);

    private static JsonElement Load()
    {
        var assembly=typeof(ForeverPaladinEquipmentCatalog).Assembly;
        var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("forever-paladin-equipment-70888.json",StringComparison.Ordinal));
        using var stream=assembly.GetManifestResourceStream(resource)!;
        using var bytes=new MemoryStream();stream.CopyTo(bytes);
        if(Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant()!=InputSha256)
            throw new InvalidOperationException("Paladin equipment review bytes differ from their pinned input.");
        using var json=JsonDocument.Parse(bytes.ToArray());
        var root=json.RootElement;
        if(root.GetProperty("catalogVersion").GetString()!=CatalogVersion ||
           root.GetProperty("sourceKind").GetString()!=EvidenceKind ||
           root.GetProperty("sourceBuild").GetString()!=SourceBuild ||
           !root.GetProperty("records").EnumerateArray().Select(x=>x.GetProperty("record").GetProperty("id").GetInt32()).Order()
               .SequenceEqual(ReviewedItemIds.Order()))
            throw new InvalidOperationException("Paladin equipment review identity differs from its source.");
        return root.Clone();
    }

    public static ForeverEquipmentCatalogDocument CreateCatalog()
    {
        var catalog=new ForeverEquipmentCatalogDocument {CatalogVersion=CatalogVersion};
        foreach(var row in Source.Value.GetProperty("records").EnumerateArray())catalog.Items.Add(Read(row));
        // Only the sword is admitted from this set. Unsupported thresholds are
        // still represented and cannot silently execute if more pieces arrive.
        catalog.Sets.Add(new() {Key="battlegear-of-eternal-justice",Name="Battlegear of Eternal Justice",
            RequiredPieces=3,Modeled=false,SourceBuild=SourceBuild,
            SourceUrl="https://theforeverera.com/data/items/sets.json",DataConfidence="provisional",
            ImplementationStatus="not-implemented",Notes="Published set506 has a three-piece 20% chance to return 100 Mana on Judgement. Only one set piece is admitted, so this threshold cannot be reached. Source sets.json SHA-256 82c958386f60395c370c43daed83963abd315b6aacbdcb8a329450f09c05b1bd; the three-piece proc is not implemented."});
        return catalog;
    }

    private static ForeverEquipmentItem Read(JsonElement row)
    {
        var r=row.GetProperty("record");var id=r.GetProperty("id").GetInt32();
        var mask=r.GetProperty("cl").GetInt64();
        if(mask<=0 || (mask&2)==0 || r.TryGetProperty("fb",out var fallback)&&fallback.GetInt32()!=0 ||
           r.TryGetProperty("effects",out var effects)&&effects.GetArrayLength()!=0)
            throw new InvalidOperationException("A reviewed Paladin item needs an explicit positive Paladin class bit, current values and no omitted effects.");
        var shield=r.GetProperty("c").GetInt32()==4;
        var allowed=ClassBits.Where(x=>(mask&x.Value)!=0).Select(x=>x.Key).ToList();
        var stats=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
        if(r.TryGetProperty("armor",out var armor))stats["armor"]=armor.GetDecimal();
        foreach(var stat in r.GetProperty("stats").EnumerateObject())
            stats.Add(stat.Name switch {
                "str"=>"strength","sta"=>"stamina","int"=>"intellect","mp5"=>"mp5",
                "block"=>"block-chance-percent","bv"=>"block-value","def"=>"defense",
                _=>throw new InvalidOperationException("Unreviewed Paladin item stat: "+stat.Name)},stat.Value.GetDecimal());
        var result=new ForeverEquipmentItem {
            Id=id,Name=r.GetProperty("n").GetProperty("en").GetString()!,RequiredLevel=r.GetProperty("rl").GetInt32(),
            ItemClass=shield?"armor":"weapon",ItemSubclass=shield?"shield":"sword",
            AllowedSlots=[shield?"off-hand":"main-hand"],
            AllowedClasses=shield?allowed.Where(x=>ForeverSharedEquipmentResolver.CanEquipArmorClass(x,"shield")).ToList():allowed,
            Quality=r.GetProperty("q").GetInt32()==4?"epic":"rare",BindType="bind-on-pickup",
            UniqueEquipped=r.TryGetProperty("unique",out var unique)&&unique.GetBoolean(),
            Stats=stats,ShieldBlockValue=shield?r.GetProperty("block").GetDecimal():0m,
            SetKey=id==21395?"battlegear-of-eternal-justice":null,
            SourceBuild=SourceBuild,SourceUrl=$"https://theforeverera.com/en/items/{id}/",
            SourceStatus="reviewed-published-forever-client-record",VerificationStatus="published-client-record",
            DataConfidence="provisional",ImplementationStatus="fully-implemented",SnapshotDate="2026-11-13",
            IconKey=r.GetProperty("i").GetString(),IconSourceStatus="published-client-item-record-provisional",
            ClassRestrictionEvidence=new() {
                ItemId=id,SourceKind=EvidenceKind,RawMask=mask,Status="restricted",DataConfidence="provisional",
                SourceAllowedClasses=allowed,SourceBuild=SourceBuild,
                SourceUrl=row.GetProperty("sourceUrl").GetString()!,SourceSha256=row.GetProperty("sourceSha256").GetString()!},
            Notes=[
                "Exact published client-derived item record. Only Paladin execution was reviewed in this subset; values are provisional, not independent live measurements.",
                $"Explicit positive mask {mask}; the Paladin bit is set. Additional bits are retained without granting eligibility to unknown classes. Missing or sentinel masks remain pending.",
                shield
                    ? $"Separate shield proficiency: {ShieldProficiencyUrl}, Holy Shield 1239570, EquipClass 4 / EquipSubclass 64, corroborated by the 70888 Seal of Fury equipped-shield prerequisite. {ShieldProficiencyConfidence}. Source bytes and hashes are pinned in shieldProficiency."
                    : $"Separate sword proficiency: {ProficiencyUrl}, displayed client {ProficiencyBuild}; decoded retrieval SHA-256 {ProficiencyInputSha256}. Trainer locations labeled Classic are excluded.",
                "Item presence does not establish current acquisition/content availability. Quest/location data marked Classic is excluded from availability evidence.",
                $"Selected record SHA-256 {row.GetProperty("recordSha256").GetString()}; full input SHA-256 {row.GetProperty("sourceSha256").GetString()}."
            ]
        };
        if(!shield){var weapon=r.GetProperty("dmg");result.Weapon=new() {
            Type="sword",Hand=id==12782?"two-hand":"one-hand",
            MinimumDamage=weapon.GetProperty("min").GetDecimal(),MaximumDamage=weapon.GetProperty("max").GetDecimal(),
            SpeedSeconds=weapon.GetProperty("speed").GetDecimal()};}
        return result;
    }

    internal static bool MatchesReviewedItem(ForeverEquipmentItem item,ForeverEquipmentClassRestrictionEvidence evidence)
    {
        if(!ReviewedItemIds.Contains(item.Id) || evidence.SourceKind!=EvidenceKind)return false;
        var expected=Read(Source.Value.GetProperty("records").EnumerateArray()
            .Single(x=>x.GetProperty("record").GetProperty("id").GetInt32()==item.Id));
        // Compare all admission/runtime fields. Display-only artwork and notes
        // may be enriched by the existing cache without changing mechanics.
        object Snapshot(ForeverEquipmentItem x)=>new {x.Id,x.Name,x.RequiredLevel,x.AllowedSlots,x.AllowedClasses,
            x.UniqueEquipped,x.UniqueEquippedGroup,x.AllowedFactions,x.ItemClass,x.ItemSubclass,x.Stats,x.Weapon,
            x.ShieldBlockValue,x.SetKey,x.SourceBuild,x.SourceUrl,x.SourceStatus,x.VerificationStatus,x.DataConfidence,
            x.Effects,x.ClassRestrictionEvidence};
        return JsonSerializer.Serialize(evidence)==JsonSerializer.Serialize(expected.ClassRestrictionEvidence) &&
            JsonSerializer.Serialize(Snapshot(item))==JsonSerializer.Serialize(Snapshot(expected));
    }

    internal static bool HasReviewedProficiency(ForeverEquipmentItem item,string classKey) =>
        classKey=="paladin" && item.ClassRestrictionEvidence is { } evidence &&
        MatchesReviewedItem(item,evidence) &&
        (item.Weapon is {Type:"sword",Hand:"one-hand" or "two-hand"} ||
         item.ItemClass=="armor" && item.ItemSubclass=="shield");
}
