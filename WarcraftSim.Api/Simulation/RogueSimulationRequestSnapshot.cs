using System.Text.Json;

namespace WarcraftSim.Api.Simulation;

/// <summary>Isolate normalization and baseline preparation without changing map semantics.</summary>
public static class RogueSimulationRequestSnapshot
{
    public static RogueSimRequest Create(RogueSimRequest source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy=JsonSerializer.Deserialize<RogueSimRequest>(JsonSerializer.Serialize(source))!;
        if(source.Talents?.Ranks is { } ranks)
            copy.Talents.Ranks=new(ranks,ranks.Comparer);
        CasterSimulationRequestSnapshot.PreserveStatComparers(source.Character,copy.Character);
        return copy;
    }
}
