using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Four reviewed, published client records, distinct from direct ItemSparse
/// evidence and the legacy catalog. Missing masks and Classic fallback records
/// never enter this collection. All values remain provisional.
/// </summary>
public static class ForeverPublishedEquipmentCatalog
{
    public const string CatalogVersion="forever-equipment-cp175-published-70888-v1";
    public const string EvidenceKind="published-client-item-record";
    public const string SourceBuild="1.60.1.70888";
    public const string SourceUrl="https://theforeverera.com/data/items/chunk-9.json";
    public const string SourceSha256="079016994c9ee6f4076dcf4ecf3cbd8ccedb33567c99242e12b0ed703eafe7a3";
    public const string MetadataSha256="7b4d624114501a0a1698ed01c7abf32267902cb15760b4eef19dc70f71a9de75";
    public const string SetsSourceUrl="https://theforeverera.com/data/items/sets.json";
    public const string SetsSourceSha256="82c958386f60395c370c43daed83963abd315b6aacbdcb8a329450f09c05b1bd";
    public const string SetKey="augurs-regalia";
    private const string SetRecordSha256="756457d4a4e628dfff0c108f0e4a35475341fcdc4854ca83e4d7d5110ff3d527";
    private static readonly IReadOnlyDictionary<int,(long Mask,int Slot,int Subclass,int Level,string Sha256)> Pins=
        new Dictionary<int,(long,int,int,int,string)> {
            [18434]=(1024,9,2,58,"4c47257b11bfc5130c31e0f1c4155055f9e76704d5e299e609f3dc033ecf87b5"),
            [18452]=(8,9,2,58,"8ffbbb426bb26edc13bbe34b3883d714adb5b0a5272f25da44bd4fa9eacd90d5"),
            [19829]=(64,6,3,0,"d723c9f370ff5661e35aaf7ed40bcb8332a989d0887108d98a2d33edfaa894ba"),
            [19830]=(64,9,3,0,"d8349fcaada49524139ce81939dc62dd8a7fe4896e5a79406320888ca9b49e1b")
        };
    private static readonly Lazy<JsonElement> Source=new(Load);
    public static IReadOnlyList<int> ReviewedItemIds=>Pins.Keys.Order().ToArray();
    public static IReadOnlyList<JsonElement> ReviewedRecords=>Source.Value.GetProperty("records")
        .EnumerateArray().Select(x=>x.Clone()).ToArray();

    private static JsonElement Load()
    {
        var assembly=typeof(ForeverPublishedEquipmentCatalog).Assembly;
        var name=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("forever-equipment-cp175-published.json",StringComparison.Ordinal));
        using var stream=assembly.GetManifestResourceStream(name)!;
        using var document=JsonDocument.Parse(stream);
        var root=document.RootElement;
        if(root.GetProperty("catalogVersion").GetString()!=CatalogVersion ||
           root.GetProperty("sourceKind").GetString()!=EvidenceKind ||
           root.GetProperty("sourceBuild").GetString()!=SourceBuild ||
           root.GetProperty("sourceUrl").GetString()!=SourceUrl ||
           root.GetProperty("sourceSha256").GetString()!=SourceSha256 ||
           root.GetProperty("metadataUrl").GetString()!="https://theforeverera.com/data/items/meta.json" ||
           root.GetProperty("metadataSha256").GetString()!=MetadataSha256 ||
           root.GetProperty("setsSourceUrl").GetString()!=SetsSourceUrl ||
           root.GetProperty("setsSourceSha256").GetString()!=SetsSourceSha256 ||
           root.GetProperty("dataConfidence").GetString()!="provisional")
            throw new InvalidOperationException("Published equipment source pins do not match the reviewed source.");
        var ids=new HashSet<int>();
        foreach(var record in root.GetProperty("records").EnumerateArray())
            if(!ids.Add(ReadPinnedRecord(record,SourceBuild,SourceSha256).Id))
                throw new InvalidOperationException("Duplicate published equipment record.");
        if(!ids.SetEquals(Pins.Keys))throw new InvalidOperationException("Published equipment review must contain its exact four records.");
        var set=root.GetProperty("setRecords").EnumerateArray().Single();
        if(set.GetProperty("id").GetInt32()!=476 || CanonicalDigest(set)!=SetRecordSha256)
            throw new InvalidOperationException("Published set record does not match its reviewed source.");
        return root.Clone();
    }

    /// <summary>
    /// Converts only an exact reviewed record. This is not a public import route:
    /// no caller-selected hash, omitted mask or unreviewed ID grants admission.
    /// </summary>
    public static ForeverEquipmentItem ReadPinnedRecord(JsonElement record,string build,string sourceHash)
    {
        if(build!=SourceBuild || sourceHash!=SourceSha256)
            throw new ArgumentException("Published equipment build or full source-file digest is not reviewed.");
        if(!record.TryGetProperty("id",out var idValue) || !idValue.TryGetInt32(out var id) ||
           !Pins.TryGetValue(id,out var pin))
            throw new ArgumentException("Published equipment ID is not in the reviewed subset.");
        if(record.TryGetProperty("fb",out var fallback) && fallback.GetInt32()!=0)
            throw new ArgumentException("Classic fallback records cannot establish Forever equipment eligibility.");
        if(!record.TryGetProperty("cl",out var maskValue) || !maskValue.TryGetInt64(out var mask) || mask<=0 || mask!=pin.Mask)
            throw new ArgumentException("An explicit matching positive class mask is required; omitted or sentinel masks remain pending.");
        if(!record.TryGetProperty("rl",out var levelValue) || !levelValue.TryGetInt32(out var level) ||
           level!=pin.Level || level is <0 or >60 || record.GetProperty("c").GetInt32()!=4 ||
           record.GetProperty("sc").GetInt32()!=pin.Subclass || record.GetProperty("s").GetInt32()!=pin.Slot)
            throw new ArgumentException("Published required level and equipment classification must match the reviewed record.");
        if(CanonicalDigest(record)!=pin.Sha256)
            throw new ArgumentException("Published equipment record differs from its reviewed source values or metadata.");
        var classKey=mask switch {8=>"rogue",64=>"shaman",1024=>"druid",_=>throw new InvalidOperationException()};
        var stats=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase) {
            ["armor"]=record.GetProperty("armor").GetDecimal()
        };
        foreach(var stat in record.GetProperty("stats").EnumerateObject())
        {
            var key=stat.Name switch {
                "sta"=>"stamina","agi"=>"agility","int"=>"intellect","sp"=>"spell-power","mp5"=>"mp5",
                _=>throw new InvalidOperationException($"Unreviewed published stat '{stat.Name}'.")
            };
            stats.Add(key,stat.Value.GetDecimal());
        }
        var hasSet=record.TryGetProperty("set",out var set);
        if(hasSet && set.GetProperty("id").GetInt32()!=476)
            throw new InvalidOperationException("Unreviewed published equipment set.");
        return new ForeverEquipmentItem {
            Id=id,Name=record.GetProperty("n").GetProperty("en").GetString()!,RequiredLevel=level,
            AllowedSlots=[pin.Slot==9?"wrist":"waist"],AllowedClasses=[classKey],
            UniqueEquipped=record.TryGetProperty("unique",out var unique) && unique.GetBoolean(),
            ItemClass="armor",ItemSubclass=pin.Subclass==2?"leather":"mail",
            Quality=record.GetProperty("q").GetInt32()==3?"rare":"epic",BindType="bind-on-pickup",
            Stats=stats,SetKey=hasSet?SetKey:null,
            SourceBuild=SourceBuild,SourceUrl=$"https://theforeverera.com/en/items/#item-{id}",
            SourceStatus="reviewed-published-forever-client-record",VerificationStatus="published-client-record",
            DataConfidence="provisional",ImplementationStatus=hasSet?"partially-implemented":"fully-implemented",
            SnapshotDate="2026-11-12",IconKey=record.GetProperty("i").GetString(),
            IconSourceStatus="published-client-item-record-provisional",
            ClassRestrictionEvidence=new() {
                ItemId=id,SourceKind=EvidenceKind,RawMask=mask,Status="restricted",DataConfidence="provisional",
                SourceAllowedClasses=[classKey],SourceBuild=SourceBuild,SourceUrl=SourceUrl,SourceSha256=SourceSha256
            },
            Notes=[
                "Published client-derived values and explicit positive class mask; not independently verified in game or raw ItemSparse DB2.",
                $"Recorded required level is {level}; no higher requirement is invented. Acquisition/vendor/quest metadata marked Classic is not used to certify current availability.",
                hasSet?"Augur's Regalia: the two-piece +4 MP5 bonus executes; three-/five-piece utility bonuses remain unimplemented and are rejected if their thresholds are reached. Only two pieces are in this curated subset.":"Static stats are executable; weapon proficiency is a separate evidence gate.",
                $"Full source-file SHA-256 {SourceSha256}; canonical selected-record SHA-256 {pin.Sha256}."
            ]
        };
    }

    internal static bool MatchesReviewedItem(ForeverEquipmentItem item,ForeverEquipmentClassRestrictionEvidence evidence)
    {
        if(!Pins.ContainsKey(item.Id) || evidence.SourceKind!=EvidenceKind || evidence.SourceBuild!=SourceBuild ||
           evidence.SourceUrl!=SourceUrl || evidence.SourceSha256!=SourceSha256)return false;
        var record=Source.Value.GetProperty("records").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==item.Id);
        var expected=ReadPinnedRecord(record,SourceBuild,SourceSha256);
        return item.Name==expected.Name && item.RequiredLevel==expected.RequiredLevel &&
            evidence.RawMask==expected.ClassRestrictionEvidence!.RawMask &&
            evidence.SourceAllowedClasses is not null && evidence.SourceAllowedClasses.SequenceEqual(expected.AllowedClasses) &&
            item.AllowedClasses is not null && item.AllowedClasses.SequenceEqual(expected.AllowedClasses) &&
            item.ItemClass==expected.ItemClass && item.ItemSubclass==expected.ItemSubclass &&
            item.SourceBuild==expected.SourceBuild && item.SourceUrl==expected.SourceUrl &&
            item.SourceStatus==expected.SourceStatus && item.VerificationStatus==expected.VerificationStatus &&
            item.DataConfidence=="provisional" && item.SetKey==expected.SetKey &&
            item.UniqueEquipped==expected.UniqueEquipped && item.Weapon is null && item.Effects is {Count:0} &&
            item.AllowedFactions is {Count:0} && item.AllowedSlots is not null && item.AllowedSlots.SequenceEqual(expected.AllowedSlots) &&
            item.Stats is not null && item.Stats.Count==expected.Stats.Count &&
            expected.Stats.All(x=>item.Stats.TryGetValue(x.Key,out var value) && value==x.Value);
    }

    internal static ForeverEquipmentCatalogDocument CreateCatalog()
    {
        var source=Source.Value;
        var result=new ForeverEquipmentCatalogDocument {
            CatalogVersion=CatalogVersion,
            Items=source.GetProperty("records").EnumerateArray().Select(x=>ReadPinnedRecord(x,SourceBuild,SourceSha256)).ToList()
        };
        foreach(var bonus in source.GetProperty("setRecords")[0].GetProperty("bonuses").EnumerateArray())
        {
            var pieces=bonus.GetProperty("n").GetInt32();
            result.Sets.Add(new() {
                Key=SetKey,Name="Augur's Regalia",RequiredPieces=pieces,Modeled=pieces==2,
                Stats=pieces==2?new(){["mp5"]=4m}:new(),
                SourceBuild=SourceBuild,SourceUrl=SetsSourceUrl,DataConfidence="provisional",
                ImplementationStatus=pieces==2?"fully-implemented":"not-implemented",
                Notes=$"Published client set record: {bonus.GetProperty("t").GetProperty("en").GetString()} Full source-file SHA-256 {SetsSourceSha256}; selected-record SHA-256 {SetRecordSha256}."
            });
        }
        return result;
    }

    private static string CanonicalDigest(JsonElement element)
    {
        using var stream=new MemoryStream();
        using(var writer=new Utf8JsonWriter(stream,new(){Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping}))
        {
            void Write(JsonElement value)
            {
                if(value.ValueKind==JsonValueKind.Object)
                {
                    writer.WriteStartObject();
                    foreach(var property in value.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal))
                    {writer.WritePropertyName(property.Name);Write(property.Value);}
                    writer.WriteEndObject();
                }
                else if(value.ValueKind==JsonValueKind.Array)
                {writer.WriteStartArray();foreach(var child in value.EnumerateArray())Write(child);writer.WriteEndArray();}
                else value.WriteTo(writer);
            }
            Write(element);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }
}
