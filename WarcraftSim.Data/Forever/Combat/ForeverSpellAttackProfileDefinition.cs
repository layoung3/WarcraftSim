namespace WarcraftSim.Data.Forever.Combat;

public sealed class ForeverSpellAttackProfileDefinition
{
    public string Key { get; init; } = "";

    public string ResolutionType { get; init; } = "";

    public bool CanMiss { get; init; }

    public bool CanCrit { get; init; }

    /// <summary>
    /// True only when the full target-level spell miss progression has been
    /// verified directly for Forever. The current +1/+2/+3 progression uses
    /// the Classic model while retaining Forever's exposed 4% equal-level and
    /// 17% raid-boss hit requirements.
    /// </summary>
    public bool LevelMissProgressionVerified { get; init; }

    /// <summary>
    /// True only when the spell critical damage multiplier is verified
    /// directly for Forever. The current 150% value remains the Classic
    /// fallback used until the beta establishes otherwise.
    /// </summary>
    public bool CriticalMultiplierVerified { get; init; }
}
