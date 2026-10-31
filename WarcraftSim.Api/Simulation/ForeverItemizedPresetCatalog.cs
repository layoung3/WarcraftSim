using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// CP174 curated source-identified level-60 starting allocations.
/// Historical-only talents are valid point records, not executable effects.
/// Embedded with the API so tests and endpoints use exactly the same snapshot.
/// </summary>
public static class ForeverItemizedPresetCatalog
{
    public sealed class Preset
    {
        public string Schema { get; set; } = "";
        public int Version { get; set; }
        public string ClassKey { get; set; } = "";
        public string Specialization { get; set; } = "";
        public int Level { get; set; }
        public string AllocationStatus { get; set; } = "";
        public JsonElement Provenance { get; set; }
        public Dictionary<string,int> Ranks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public int TotalRankedPoints { get; set; }
    }

    public static Preset Load(string classKey,string specialization)
    {
        if(classKey is not ("mage" or "warlock" or "hunter") ||
            !System.Text.RegularExpressions.Regex.IsMatch(specialization??"", "^[a-z-]+$"))
            throw new ArgumentException("Unsupported itemized preset identity.");
        var suffix=$"itemized-presets.{classKey}-{specialization}.json";
        var assembly=typeof(ForeverItemizedPresetCatalog).Assembly;
        var name=assembly.GetManifestResourceNames()
            .SingleOrDefault(x=>x.EndsWith(suffix,StringComparison.Ordinal));
        // Web SDKs may classify wwwroot assets as static web assets rather than
        // manifest resources. Use the same reviewed file in source checkouts,
        // local API runs and published output; never substitute synthetic ranks.
        var fileName=$"{classKey}-{specialization}.json";
        Stream? stream=name is null?null:assembly.GetManifestResourceStream(name);
        if(stream is null)
        {
            var locations=new[]{
                Path.Combine(AppContext.BaseDirectory,"wwwroot","itemized-presets",fileName),
                Path.Combine(Directory.GetCurrentDirectory(),"wwwroot","itemized-presets",fileName),
                Path.Combine(Directory.GetCurrentDirectory(),"WarcraftSim.Api","wwwroot","itemized-presets",fileName)
            };
            var existing=locations.FirstOrDefault(File.Exists);
            if(existing is not null)stream=File.OpenRead(existing);
        }
        if(stream is null)throw new ArgumentException($"No source-backed itemized preset exists for {classKey}/{specialization}. Check static assets.");
        using(stream)
        {
        var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var preset=JsonSerializer.Deserialize<Preset>(stream,options) ??
            throw new InvalidOperationException("Itemized preset data is empty.");
        if(preset.Schema!="warcraftsim.cp174-itemized-preset" ||
            preset.Version!=1 || preset.ClassKey!=classKey ||
            preset.Specialization!=specialization || preset.Level!=60 ||
            preset.Ranks.Count==0 || preset.Ranks.Values.Any(rank=>rank<1) ||
            preset.Ranks.Values.Sum()!=preset.TotalRankedPoints ||
            preset.TotalRankedPoints!=51)
            throw new InvalidOperationException($"Itemized preset integrity mismatch for {classKey}/{specialization}.");
        return preset;
        }
    }
}
