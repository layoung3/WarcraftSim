namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Forever's character-sheet ceiling is 75% damage prevented by Armor.
/// Class-specific mitigation resolvers may use distinct provisionally
/// calibrated denominators, but must share this combat-math boundary.
/// This is not a generic cap on non-Armor reductions or damage multipliers.
/// </summary>
public static class ForeverArmorMitigationMath
{
    public const decimal MaximumArmorReductionFraction=.75m;

    public static decimal ClampArmorReductionFraction(decimal requestedFraction) =>
        Math.Clamp(requestedFraction,0m,MaximumArmorReductionFraction);
}
