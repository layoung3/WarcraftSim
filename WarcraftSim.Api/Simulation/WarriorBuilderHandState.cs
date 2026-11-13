namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Builder bookkeeping only, ignored by combat. The original manual inputs and
/// last resolved outputs make catalog removal reversible without inventing stats.
/// Top-level edits since the last preparation update the manual inputs before
/// the current catalog selection is resolved. To rebase completely, submit a
/// fresh manual request with empty Gear and null BuilderHandState.
/// An unchanged value equal to the prior catalog output is not an explicit edit;
/// use that reset/rebase path to adopt catalog numbers as manual assumptions.
/// </summary>
public sealed record WarriorBuilderHandState
{
    public required WarriorBuilderHandInputs ManualInputs { get; init; }
    public required WarriorBuilderHandInputs LastResolvedInputs { get; init; }
}

public sealed record WarriorBuilderHandInputs
{
    public required decimal MainHandMinimumDamage { get; init; }
    public required decimal MainHandMaximumDamage { get; init; }
    public required decimal MainHandSpeedSeconds { get; init; }
    public required bool IsTwoHanded { get; init; }
    public required string MainHandWeaponType { get; init; }
    public required bool DualWield { get; init; }
    public required decimal OffHandMinimumDamage { get; init; }
    public required decimal OffHandMaximumDamage { get; init; }
    public required decimal OffHandSpeedSeconds { get; init; }
    public required string OffHandWeaponType { get; init; }
    public required bool ShieldEquipped { get; init; }
    public required decimal ShieldIntrinsicBlockValue { get; init; }
    public required decimal PlayerBlockValue { get; init; }

    internal static WarriorBuilderHandInputs Capture(WarriorSimRequest r) => new()
    {
        MainHandMinimumDamage=r.MainHandMinimumDamage,MainHandMaximumDamage=r.MainHandMaximumDamage,
        MainHandSpeedSeconds=r.MainHandSpeedSeconds,IsTwoHanded=r.IsTwoHanded,MainHandWeaponType=r.MainHandWeaponType,
        DualWield=r.DualWield,OffHandMinimumDamage=r.OffHandMinimumDamage,OffHandMaximumDamage=r.OffHandMaximumDamage,
        OffHandSpeedSeconds=r.OffHandSpeedSeconds,OffHandWeaponType=r.OffHandWeaponType,
        ShieldEquipped=r.ShieldEquipped,ShieldIntrinsicBlockValue=r.ShieldIntrinsicBlockValue,PlayerBlockValue=r.PlayerBlockValue
    };

    internal WarriorBuilderHandInputs WithEditsSince(WarriorBuilderHandInputs prior,WarriorBuilderHandInputs current) => this with
    {
        MainHandMinimumDamage=current.MainHandMinimumDamage!=prior.MainHandMinimumDamage?current.MainHandMinimumDamage:MainHandMinimumDamage,
        MainHandMaximumDamage=current.MainHandMaximumDamage!=prior.MainHandMaximumDamage?current.MainHandMaximumDamage:MainHandMaximumDamage,
        MainHandSpeedSeconds=current.MainHandSpeedSeconds!=prior.MainHandSpeedSeconds?current.MainHandSpeedSeconds:MainHandSpeedSeconds,
        IsTwoHanded=current.IsTwoHanded!=prior.IsTwoHanded?current.IsTwoHanded:IsTwoHanded,
        MainHandWeaponType=current.MainHandWeaponType!=prior.MainHandWeaponType?current.MainHandWeaponType:MainHandWeaponType,
        DualWield=current.DualWield!=prior.DualWield?current.DualWield:DualWield,
        OffHandMinimumDamage=current.OffHandMinimumDamage!=prior.OffHandMinimumDamage?current.OffHandMinimumDamage:OffHandMinimumDamage,
        OffHandMaximumDamage=current.OffHandMaximumDamage!=prior.OffHandMaximumDamage?current.OffHandMaximumDamage:OffHandMaximumDamage,
        OffHandSpeedSeconds=current.OffHandSpeedSeconds!=prior.OffHandSpeedSeconds?current.OffHandSpeedSeconds:OffHandSpeedSeconds,
        OffHandWeaponType=current.OffHandWeaponType!=prior.OffHandWeaponType?current.OffHandWeaponType:OffHandWeaponType,
        ShieldEquipped=current.ShieldEquipped!=prior.ShieldEquipped?current.ShieldEquipped:ShieldEquipped,
        ShieldIntrinsicBlockValue=current.ShieldIntrinsicBlockValue!=prior.ShieldIntrinsicBlockValue?current.ShieldIntrinsicBlockValue:ShieldIntrinsicBlockValue,
        PlayerBlockValue=current.PlayerBlockValue!=prior.PlayerBlockValue?current.PlayerBlockValue:PlayerBlockValue
    };

    internal void ApplyTo(WarriorSimRequest r)
    {
        r.MainHandMinimumDamage=MainHandMinimumDamage;r.MainHandMaximumDamage=MainHandMaximumDamage;
        r.MainHandSpeedSeconds=MainHandSpeedSeconds;r.IsTwoHanded=IsTwoHanded;r.MainHandWeaponType=MainHandWeaponType;
        r.DualWield=DualWield;r.OffHandMinimumDamage=OffHandMinimumDamage;r.OffHandMaximumDamage=OffHandMaximumDamage;
        r.OffHandSpeedSeconds=OffHandSpeedSeconds;r.OffHandWeaponType=OffHandWeaponType;
        r.ShieldEquipped=ShieldEquipped;r.ShieldIntrinsicBlockValue=ShieldIntrinsicBlockValue;r.PlayerBlockValue=PlayerBlockValue;
    }
}
