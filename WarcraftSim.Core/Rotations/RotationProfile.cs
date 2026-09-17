using WarcraftSim.Core.Simulation;

namespace WarcraftSim.Core.Rotations;

public sealed class RotationProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New Rotation";

    public string RulesetKey { get; set; } = "";

    public string ClassKey { get; set; } = "";

    public SimulationType SimulationType { get; set; }

    public List<RotationEntry> Entries { get; set; } = [];
}