namespace WarcraftSim.Core.Abilities;

public sealed class AbilityUseResult
{
    public bool Success { get; init; }

    public string? FailureReason { get; init; }

    public static AbilityUseResult Succeeded()
    {
        return new AbilityUseResult
        {
            Success = true
        };
    }

    public static AbilityUseResult Failed(string reason)
    {
        return new AbilityUseResult
        {
            Success = false,
            FailureReason = reason
        };
    }
}