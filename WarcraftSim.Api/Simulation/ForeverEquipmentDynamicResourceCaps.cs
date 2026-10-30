using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Api.Simulation;

/// <summary>
/// Keeps temporary resource capacity in the same units as the current level-60
/// Mage/Warlock/Hunter equipment bridges: Intellect grants 15 maximum Mana,
/// Stamina grants 10 maximum Health. Class talent multipliers are supplied
/// by the caller, never invented here. Gains do not create "free" Mana on
/// expiration; available resources are clamped to the restored maximum.
/// </summary>
public static class ForeverEquipmentDynamicResourceCaps
{
    public static void Apply(SimulationContext context,SimulationActorState actor,
        IReadOnlyDictionary<string,decimal> delta,
        decimal intellectManaMultiplier=1m,decimal staminaHealthMultiplier=1m)
    {
        var manaChange=delta.GetValueOrDefault("mana")+
            delta.GetValueOrDefault("intellect")*15m*intellectManaMultiplier;
        if(manaChange!=0m && actor.Resources.TryGetValue("mana",out var mana))
        {
            mana.AdvanceTo(context.CurrentTimeSeconds);
            var oldMaximum=mana.Maximum;
            mana.Maximum=Math.Max(0m,oldMaximum+manaChange);
            // A capacity increase does not also grant an unreported resource
            // restoration; however a decrease must cap the current balance.
            mana.SetCurrent(mana.Current);
        }
        var healthChange=(delta.GetValueOrDefault("health")+
            delta.GetValueOrDefault("stamina")*10m)*staminaHealthMultiplier;
        if(healthChange!=0m)
        {
            var newMaximum=Math.Max(1m,actor.MaximumHealth+healthChange);
            // Preserves current damage deficit when capacity is added, and
            // clamps down when a temporary Stamina/Health effect expires.
            var newCurrent=Math.Clamp(actor.CurrentHealth+healthChange,
                0m,newMaximum);
            actor.InitializeHealth(newMaximum,newCurrent);
        }
    }
}
