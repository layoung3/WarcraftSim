namespace WarcraftSim.Core.Encounters;

public sealed class EncounterPhaseDefinition
{
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    public decimal StartTimeSeconds { get; set; }

    public decimal? EndTimeSeconds { get; set; }

    public List<string> Tags { get; set; } = [];

    public bool IsActiveAt(
        decimal timeSeconds)
    {
        if (timeSeconds < StartTimeSeconds)
        {
            return false;
        }

        if (!EndTimeSeconds.HasValue)
        {
            return true;
        }

        return
            timeSeconds <
            EndTimeSeconds.Value;
    }
}
