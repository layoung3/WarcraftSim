namespace WarcraftSim.Core.Simulation.Engine;

/// <summary>
/// A time-driven health-percent script for single-actor DPS studies. Combat damage
/// continues to affect actual HP, but does not change this virtual phase schedule.
/// The script is opt-in; ordinary raid encounters still read actual actor health.
/// Checkpoints are discrete (not interpolated) and their times are inclusive.
/// </summary>
public sealed record ScriptedTargetHealthCheckpoint(decimal TimeSeconds, decimal HealthPercent);

public sealed class ScriptedTargetHealthTimeline
{
    public IReadOnlyList<ScriptedTargetHealthCheckpoint> Checkpoints { get; }

    public ScriptedTargetHealthTimeline(IEnumerable<ScriptedTargetHealthCheckpoint> checkpoints)
    {
        ArgumentNullException.ThrowIfNull(checkpoints);
        var ordered = checkpoints.ToArray();
        if (ordered.Length == 0 || ordered[0].TimeSeconds != 0m)
            throw new ArgumentException("A scripted health timeline must start at time zero.", nameof(checkpoints));
        decimal previousTime = -1m;
        decimal previousPercent = 100m;
        foreach (var point in ordered)
        {
            if (point.TimeSeconds <= previousTime || point.HealthPercent is <= 0m or > 100m ||
                point.HealthPercent > previousPercent)
                throw new ArgumentException("Scripted health checkpoints must be ordered by distinct times, have positive nonincreasing percentages, and stay at or below 100%.", nameof(checkpoints));
            previousTime = point.TimeSeconds;
            previousPercent = point.HealthPercent;
        }
        Checkpoints = Array.AsReadOnly(ordered);
    }

    public decimal GetHealthPercentAt(decimal timeSeconds)
    {
        var value = Checkpoints[0].HealthPercent;
        foreach (var checkpoint in Checkpoints)
        {
            if (checkpoint.TimeSeconds > timeSeconds) break;
            value = checkpoint.HealthPercent;
        }
        return value;
    }
}
