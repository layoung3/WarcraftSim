namespace WarcraftSim.Core.Abilities;

/// <summary>
/// Controls how an effect's spell/stat scaling coefficient is applied when
/// the effect produces more than one delivered occurrence.
/// </summary>
public static class AbilityEffectScalingCoefficientModes
{
    /// <summary>
    /// The configured coefficient is applied independently to every delivered
    /// occurrence. This preserves the engine's historical behavior.
    /// </summary>
    public const string PerOccurrence =
        "per-occurrence";

    /// <summary>
    /// The configured coefficient represents the total contribution across
    /// all delivered periodic or channel ticks and is divided by the actual
    /// scheduled occurrence count.
    /// </summary>
    public const string TotalAcrossOccurrences =
        "total-across-occurrences";
}
