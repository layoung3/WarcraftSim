using WarcraftSim.Core.Simulation.Engine;

namespace WarcraftSim.Data.Forever.Combat;

/// <summary>
/// Forever's incoming Rage normalization applies to Warriors and Druids.
/// Reuse the Warrior factory's shared mathematical contract instead of the
/// old fixed five-Rage-per-hit Bear preview. The reference-health/expected
/// Armor calibration is not yet supplied by Blizzard and stays configurable.
/// </summary>
public static class ForeverDruidDamageTakenRageFactory
{
    public const string DefinitionKey="forever-druid-damage-taken-rage";

    public static void Configure(SimulationActorState druid,
        decimal expectedCreatureHealth,decimal expectedArmorReductionPercent)
    {
        ArgumentNullException.ThrowIfNull(druid);
        var basis=ForeverWarriorDamageTakenRageFactory.Create(
            expectedCreatureHealth,expectedArmorReductionPercent);
        druid.AddDamageTakenResourceGeneration(
            new DamageTakenResourceGenerationDefinition {
                Key=DefinitionKey,Name="Forever Druid Rage from Damage Taken",
                ResourceKey=basis.ResourceKey,
                ReferenceHealth=basis.ReferenceHealth,
                ResourcePerReferenceHealthOfEligibleDamage=
                    basis.ResourcePerReferenceHealthOfEligibleDamage,
                IgnoreArmorMitigation=basis.IgnoreArmorMitigation,
                IgnoredArmorReplacementReductionPercent=
                    basis.IgnoredArmorReplacementReductionPercent,
                IgnoreAbsorbs=basis.IgnoreAbsorbs,
                BlockReducesEligibleDamage=basis.BlockReducesEligibleDamage,
                RequiresExternalSourceActor=basis.RequiresExternalSourceActor
            });
    }
}
