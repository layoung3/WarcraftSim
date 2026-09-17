using WarcraftSim.Core.Encounters;

namespace WarcraftSim.Core.ImportExport;

public sealed class EncounterExportData
{
    public EncounterProfile Encounter { get; set; } = new();
}