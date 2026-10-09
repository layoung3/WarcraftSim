namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Classic/Forever normalized speeds used by instant weapon-damage attacks.
/// These values affect only the Attack Power contribution; the weapon's
/// listed minimum/maximum damage remains unchanged.
/// </summary>
public static class ForeverNormalizedWeaponSpeeds
{
    public const decimal Dagger = 1.7m;

    public const decimal OneHanded = 2.4m;

    public const decimal TwoHanded = 3.3m;

    public const decimal Ranged = 2.8m;
}
