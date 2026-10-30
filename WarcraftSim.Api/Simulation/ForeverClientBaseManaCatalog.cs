using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Direct 70707 PlayerExpectedStat BaseMana rows at class ID/level 1–60.
/// No race primary attributes or health growth are encoded in this client
/// table; do not use this subset as a complete character baseline.
/// </summary>
public static class ForeverClientBaseManaCatalog
{
    private sealed class Record
    {
        public string SourceBuild { get; set; }="";
        public string SourceSha256 { get; set; }="";
        public int ContentSetId { get; set; }
        public string SourceStatus { get; set; }="";
        public Dictionary<string,int> ClassIds { get; set; }=[];
        public Dictionary<string,int[]> BaseManaByClassId { get; set; }=[];
    }
    private static readonly Lazy<Record> Source=new(Load);
    private static Record Load()
    {
        var assembly=typeof(ForeverClientBaseManaCatalog).Assembly;
        var name=assembly.GetManifestResourceNames().Single(x=>
            x.EndsWith("forever-cp173-base-mana-70707.json",
                StringComparison.OrdinalIgnoreCase));
        using var stream=assembly.GetManifestResourceStream(name) ??
            throw new InvalidOperationException("CP173 base Mana resource missing.");
        var data=JsonSerializer.Deserialize<Record>(stream,
            new JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ??
            throw new InvalidOperationException("CP173 base Mana source is empty.");
        if(data.SourceBuild!="1.60.1.70707" ||
           data.ContentSetId!=0 ||
           data.SourceSha256.Length!=64 ||
           !data.SourceSha256.All(Uri.IsHexDigit) ||
           data.ClassIds.Count!=9 ||
           data.BaseManaByClassId.Count!=9 ||
           data.ClassIds.Any(x=>!data.BaseManaByClassId.TryGetValue(
               x.Value.ToString(),out var values) ||
               values.Length!=60 || values.Any(n=>n<0)))
            throw new InvalidOperationException("Pinned CP173 base Mana catalog has invalid source fields.");
        return data;
    }
    public static decimal GetBaseMana(string classKey,int level)
    {
        if(level is <1 or >60)throw new ArgumentException("Forever level must be 1–60.");
        if(!Source.Value.ClassIds.TryGetValue(classKey.Trim().ToLowerInvariant(),out var id))
            throw new ArgumentException($"Unsupported Forever class '{classKey}'.");
        return Source.Value.BaseManaByClassId[id.ToString()][level-1];
    }
    public static string SourceBuild=>Source.Value.SourceBuild;
    public static string SourceSha256=>Source.Value.SourceSha256;
    public static string SourceStatus=>Source.Value.SourceStatus;
}
