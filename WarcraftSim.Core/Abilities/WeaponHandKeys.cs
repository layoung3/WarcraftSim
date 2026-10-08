namespace WarcraftSim.Core.Abilities;

/// <summary>
/// Identifies which weapon hand produced an attack effect. This stays on the
/// effect itself so hand-specific rules can also apply to weapon specials, not
/// only background auto-attacks.
/// </summary>
public static class WeaponHandKeys
{
    public const string MainHand =
        "main-hand";

    public const string OffHand =
        "off-hand";

    public const string Ranged =
        "ranged";
}
