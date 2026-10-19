using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Druid;

public enum ForeverDruidForm { Caster, Cat, Bear, DireBear, Moonkin }

/// <summary>
/// Reusable, mutable per-actor shapeshift state; this is not an equipment model.
/// Weapon DPS carries through animal forms at 1.0/2.5-second fixed swing speeds
/// (Forever official Druid deep dive). Furor prevents free Energy from re-shifting;
/// at five ranks Energy continues regenerating outside Cat unless in Bear.
/// </summary>
public sealed class ForeverDruidFormState
{
    public ForeverDruidForm Form { get; private set; } = ForeverDruidForm.Caster;
    public int FurorRank { get; }
    public decimal ShiftManaCost { get; }
    public int Shifts { get; private set; }
    public decimal NextShiftAvailableAtSeconds { get; private set; }
    public ForeverDruidFormState(int furorRank = 0, decimal shiftManaCost = 250m)
    {
        if (furorRank is < 0 or > 5 || shiftManaCost < 0m)
            throw new ArgumentException("Invalid Druid form configuration.");
        FurorRank = furorRank;
        ShiftManaCost = shiftManaCost;
    }
    public static string AuraKey(ForeverDruidForm form) => form switch
    {
        ForeverDruidForm.Cat => "druid-cat-form",
        ForeverDruidForm.Bear => "druid-bear-form",
        ForeverDruidForm.DireBear => "druid-dire-bear-form",
        ForeverDruidForm.Moonkin => "druid-moonkin-form",
        _ => "druid-caster-form"
    };
    public static decimal SwingSeconds(ForeverDruidForm form) => form switch
    {
        ForeverDruidForm.Cat => 1m,
        ForeverDruidForm.Bear or ForeverDruidForm.DireBear => 2.5m,
        _ => 0m
    };
    public static decimal WeaponDamage(decimal equippedWeaponDps, ForeverDruidForm form)
    {
        if (equippedWeaponDps < 0m) throw new ArgumentOutOfRangeException(nameof(equippedWeaponDps));
        return equippedWeaponDps * SwingSeconds(form);
    }
    public bool Shift(SimulationActorState actor, ForeverDruidForm next, decimal now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (Form == next || now < NextShiftAvailableAtSeconds) return false;
        // Resource restoration must run before the affordability check, not
        // after payment (which could otherwise erase regenerating Mana).
        actor.RefreshResources(now);
        if (!actor.Resources.TryGetValue("mana", out var mana) || !mana.CanSpend(ShiftManaCost))
            return false;
        mana.Spend(ShiftManaCost);
        if (actor.Resources.TryGetValue("energy", out var energy))
        {
            if (next is ForeverDruidForm.Bear or ForeverDruidForm.DireBear)
                energy.SetCurrent(0m);
            else if (next == ForeverDruidForm.Cat && FurorRank < 5)
                energy.SetCurrent(0m);
            energy.RegenerationPerSecond = next == ForeverDruidForm.Cat ||
                next is not (ForeverDruidForm.Bear or ForeverDruidForm.DireBear) && FurorRank == 5
                ? 10m : 0m;
        }
        Form = next;
        NextShiftAvailableAtSeconds = now + 6m;
        Shifts++;
        return true;
    }
}
