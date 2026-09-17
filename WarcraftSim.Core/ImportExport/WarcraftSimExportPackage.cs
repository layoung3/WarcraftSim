namespace WarcraftSim.Core.ImportExport;

public sealed class WarcraftSimExportPackage<T>
{
    public ExportMetadata Metadata { get; set; } = new();

    public T Data { get; set; } = default!;
}