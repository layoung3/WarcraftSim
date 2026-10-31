using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Optional, historical visual information sourced from a pinned WoWSims Forever
/// talent-tree export. Never establishes combat rank caps, eligibility or the
/// current 70707 tree. Engine metadata must match key, tree and rank cap before
/// any visual is attached. Executable validation remains authoritative.
/// </summary>
public static class ForeverTalentVisualReferenceCatalog
{
    private sealed class Document
    {
        public string SourceCommit { get; set; } = "";
        public List<VisualRecord> Records { get; set; } = [];
    }

    public sealed class VisualRecord
    {
        public string ClassKey { get; set; } = "";
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public string Tree { get; set; } = "";
        public int MaxRank { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public string? IconKey { get; set; }
        public string? DescriptionTemplate { get; set; }
        public string? PrerequisiteKey { get; set; }
        public List<List<decimal>> RankValues { get; set; } = [];
    }

    public const string SourceUrl = "https://github.com/gunba/wow-forever-sim";
    public const string VerificationStatus = "historical-reference-not-current-70707-verified";
    private static readonly Lazy<Document> Data = new(Load);

    private static Document Load()
    {
        var assembly = typeof(ForeverTalentVisualReferenceCatalog).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(x=>
            x.EndsWith("forever-talent-visual-reference-v1.json",StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource) ??
            throw new InvalidOperationException("Embedded CP174 talent visual reference is missing.");
        return JsonSerializer.Deserialize<Document>(stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ??
            throw new InvalidOperationException("Embedded talent visual reference is invalid.");
    }

    public static string SourceCommit => Data.Value.SourceCommit;

    public static IReadOnlyList<string> HistoricalRankDescriptions(VisualRecord visual)
    {
        if(string.IsNullOrWhiteSpace(visual.DescriptionTemplate))return [];
        return visual.RankValues.Select(values=>
            System.Text.RegularExpressions.Regex.Replace(visual.DescriptionTemplate,
                @"\{(\d+)\}",match=>int.TryParse(match.Groups[1].Value,out var index) &&
                    index>=0 && index<values.Count ?
                    values[index].ToString(System.Globalization.CultureInfo.InvariantCulture) : match.Value)).ToArray();
    }

    public static VisualRecord? Match(string classKey,string tree,string key,int maxRank)
    {
        return Data.Value.Records.FirstOrDefault(x=>
            x.ClassKey.Equals(classKey,StringComparison.OrdinalIgnoreCase) &&
            x.Tree.Equals(tree,StringComparison.OrdinalIgnoreCase) &&
            x.Key.Equals(key,StringComparison.OrdinalIgnoreCase) &&
            x.MaxRank==maxRank &&
            x.Row is >=1 and <=7 && x.Column is >=1 and <=4);
    }
}
