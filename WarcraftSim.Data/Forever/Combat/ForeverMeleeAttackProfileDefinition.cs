namespace WarcraftSim.Data.Forever.Combat;

public sealed class ForeverMeleeAttackProfileDefinition
{
    public string Key { get; init; } = "";

    public string ResolutionType { get; init; } = "";

    public bool UsesWeaponSkill { get; init; }

    public bool UsesTargetDefenseSkill { get; init; }

    public bool CanMiss { get; init; }

    public bool CanBeDodged { get; init; }

    public bool CanBeParried { get; init; }

    public bool CanGlance { get; init; }

    public bool CanBeBlocked { get; init; }

    public bool CanCrit { get; init; }

    public bool CanCrush { get; init; }

    public bool GlancingDamageModelVerified { get; init; }

    public bool CrushingChanceModelVerified { get; init; }
}
