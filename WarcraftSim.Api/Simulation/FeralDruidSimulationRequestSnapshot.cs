using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>Copy mutable inputs without repairing invalid values or changing map lookup semantics.</summary>
public static class FeralDruidSimulationRequestSnapshot
{
    public static FeralDruidSimRequest Create(FeralDruidSimRequest source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy = JsonSerializer.Deserialize<FeralDruidSimRequest>(JsonSerializer.Serialize(source))!;
        if (source.Talents?.Ranks is { } ranks)
            copy.Talents.Ranks = new(ranks, ranks.Comparer);
        if (source.InitialComboPointsByTarget is { } points)
            copy.InitialComboPointsByTarget = new(points, points.Comparer);
        return copy;
    }
}
