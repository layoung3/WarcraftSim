using WarcraftSim.Core.Characters;
using WarcraftSim.Core.Rotations;

namespace WarcraftSim.Core.ImportExport;

public sealed class CharacterExportData
{
    public CharacterProfile Character { get; set; } = new();

    public List<RotationProfile> Rotations { get; set; } = [];
}