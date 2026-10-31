using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Only code-reviewed records from the embedded, versioned source ledger may
/// be selected by identifier without an inline observation. The ledger begins
/// empty rather than silently using Classic stats for unknown Forever races.
/// </summary>
public static class ForeverLevel60BaselineCatalog
{
    private sealed class Document
    {
        public string CatalogVersion { get; set; }="";
        public string ClientBuild { get; set; }="";
        public string SourceStatus { get; set; }="";
        public List<ForeverLevel60BaselineObservation> Observations { get; set; }=[];
    }

    private static readonly Lazy<Document> Source=new(Load);
    private static Document Load()
    {
        var assembly=typeof(ForeverLevel60BaselineCatalog).Assembly;
        var resource=assembly.GetManifestResourceNames().Single(x=>
            x.EndsWith("forever-cp173-level60-baselines.json",
                StringComparison.OrdinalIgnoreCase));
        using var stream=assembly.GetManifestResourceStream(resource) ??
            throw new InvalidOperationException("CP173 level60 observation catalog missing.");
        var data=JsonSerializer.Deserialize<Document>(stream,
            new JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ??
            throw new InvalidOperationException("Empty level60 observation catalog.");
        if(data.CatalogVersion!="cp173-level60-observations-v2" ||
           data.ClientBuild!="1.60.1.70707" || string.IsNullOrWhiteSpace(data.SourceStatus) ||
           data.Observations is null)
            throw new InvalidOperationException("CP173 observation catalog has unrecognized schema.");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var observation in data.Observations)
        {
            ForeverLevel60Baseline.Validate(observation);
            if(observation.VerificationStatus is not (
                "reviewed-forever-in-game" or "reviewed-forever-provisional" or
                "provisional-documented-proxy"))
                throw new InvalidOperationException(
                    "Catalog entries must have a reviewed or explicitly provisional status.");
            if(!seen.Add(observation.Race+"/"+observation.ClassKey))
                throw new InvalidOperationException("Duplicate approved level60 race/class observation.");
        }
        return data;
    }

    public static string CatalogVersion=>Source.Value.CatalogVersion;
    public static string SourceStatus=>Source.Value.SourceStatus;
    public static IReadOnlyList<ForeverLevel60BaselineObservation> Entries=>
        Source.Value.Observations.AsReadOnly();

    private static ForeverLevel60BaselineObservation? Match(string race,string classKey,
        string status)=>Entries.FirstOrDefault(x=>
            string.Equals(x.Race,race,StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ClassKey,classKey,StringComparison.OrdinalIgnoreCase) &&
            x.VerificationStatus==status);
    // Strict mode never silently returns a historical proxy.
    public static ForeverLevel60BaselineObservation Find(string race,string classKey)=>
        Match(race,classKey,"reviewed-forever-in-game") ??
        throw new ArgumentException(
            $"No verified Forever level-60 baseline for {race}/{classKey}. Strict mode cannot use historical Classic values.");
    public static ForeverLevel60BaselineObservation FindProvisional(string race,string classKey)=>
        Match(race,classKey,"reviewed-forever-in-game") ??
        Match(race,classKey,"reviewed-forever-provisional") ??
        Match(race,classKey,"provisional-documented-proxy") ??
        throw new ArgumentException(
            $"No defensible historical level-60 proxy for {race}/{classKey}. Skyborne or unsupported combinations require direct Forever observations.");
}
