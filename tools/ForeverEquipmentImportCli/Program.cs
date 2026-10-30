using System.Text.Json;
using WarcraftSim.Api.Simulation;

if (args.Length != 5 || args[0] != "import")
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ForeverEquipmentImportCli -- import <CP170-baseline.json> <reviewed-manifest.json> <output-catalog.json> <report.json>");
    return 2;
}

try
{
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    var baseline = JsonSerializer.Deserialize<ForeverEquipmentCatalogDocument>(
        File.ReadAllText(args[1]), options) ??
        throw new InvalidOperationException("Baseline catalog is empty.");
    var result = ForeverEquipmentSnapshotPipeline.Import(
        File.ReadAllText(args[2]), baseline);
    var diff = ForeverEquipmentSnapshotPipeline.Compare(baseline, result.Catalog);
    var snapshot = ForeverEquipmentSnapshotPipeline.CanonicalCatalogJson(result.Catalog);

    // Complete all validation before opening either destination.
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[4]))!);
    File.WriteAllText(args[3], snapshot);
    File.WriteAllText(args[4], JsonSerializer.Serialize(new
    {
        result.Report,
        changes = diff,
        metadataOnly = result.MetadataOnly,
        snapshotSha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant()
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }) + "\n");
    Console.WriteLine($"Imported {result.Report.NewVerified} new reviewed combat items, " +
        $"{result.Report.ReviewedOverrides} reviewed overrides, " +
        $"{result.Report.MetadataOnly} metadata candidates (excluded); " +
        $"runtime-format catalog {result.Report.TotalCatalog} items.");
    return 0;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
    or IOException or JsonException)
{
    Console.Error.WriteLine("Import failed: " + ex.Message);
    return 1;
}
